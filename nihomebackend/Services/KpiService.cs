using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class KpiService(AppDbContext db, INotificationService notifications) : IKpiService
{
    public async Task<IReadOnlyList<KpiUserOptionResponse>> ListEligibleUsersAsync(CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking()
            .Where(user => user.IsActive)
            .Select(user => new
            {
                user.Id,
                Name = user.FullName ?? user.Email,
                RoleCode = user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString(),
            })
            .OrderBy(user => user.Name)
            .ToListAsync(ct);
        var activePositions = await db.KpiDefinitions.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => item.RoleCode)
            .Distinct()
            .ToListAsync(ct);
        return users
            .Select(user => new KpiUserOptionResponse
            {
                UserId = user.Id,
                UserName = user.Name,
                PositionCode = KpiPosition(user.RoleCode),
            })
            .Where(user => activePositions.Contains(user.PositionCode))
            .ToList();
    }

    private const string TimeZoneId = "Asia/Ho_Chi_Minh";

    public async Task<IReadOnlyList<KpiDefinitionResponse>> ListDefinitionsAsync(CancellationToken ct = default) =>
        await db.KpiDefinitions.AsNoTracking()
            .OrderBy(item => item.RoleCode)
            .ThenBy(item => item.Code)
            .Select(item => MapDefinition(item))
            .ToListAsync(ct);

    public async Task<KpiDefinitionResponse?> UpdateDefinitionAsync(
        int id,
        UpdateKpiDefinitionRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        if (request.Weight <= 0 || request.Weight > 1)
            throw new KpiOperationException("Trọng số KPI phải lớn hơn 0 và không vượt quá 1.");
        if (request.TargetValue.HasValue && request.TargetValue <= 0)
            throw new KpiOperationException("Mục tiêu KPI phải lớn hơn 0.");
        if (request.MinimumAcceptableScore is < 0 or > 100)
            throw new KpiOperationException("Ngưỡng cảnh báo KPI phải từ 0 đến 100.");
        var definition = await db.KpiDefinitions.FindAsync([id], ct);
        if (definition is null) return null;
        CrmConcurrency.Apply(db, definition, request.RowVersion);
        var otherActiveWeight = await db.KpiDefinitions.AsNoTracking()
            .Where(item => item.RoleCode == definition.RoleCode && item.Id != id && item.IsActive)
            .SumAsync(item => (decimal?)item.Weight, ct) ?? 0m;
        var totalActiveWeight = otherActiveWeight + (request.IsActive ? request.Weight : 0m);
        if (totalActiveWeight > 1m)
        {
            throw new KpiOperationException(
                $"Tổng trọng số KPI đang hiệu lực cho vị trí {definition.RoleCode} không được vượt quá 100%.");
        }
        definition.Weight = request.Weight;
        definition.TargetValue = request.TargetValue;
        definition.MinimumAcceptableScore = request.MinimumAcceptableScore;
        definition.TargetDirection = request.TargetDirection;
        definition.IsActive = request.IsActive;
        definition.Version++;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapDefinition(definition);
    }

    public async Task<KpiDashboardResponse> CalculateAsync(
        int year,
        int month,
        int userId,
        int callerUserId,
        CancellationToken ct = default)
    {
        ValidatePeriod(year, month);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var user = await UserAsync(userId, ct)
            ?? throw new KpiOperationException("Người dùng KPI không tồn tại hoặc đã ngừng hoạt động.");
        var period = await GetOrCreatePeriodAsync(year, month, userId, ct);
        if (period.Status == KpiPeriodStatus.Locked)
            throw new KpiOperationException("Kỳ KPI đã khóa và không thể tính lại.");
        var definitions = await db.KpiDefinitions
            .Where(item => item.IsActive && item.RoleCode == user.RoleCode)
            .OrderBy(item => item.Code)
            .ToListAsync(ct);
        if (definitions.Count == 0)
            throw new KpiOperationException($"Chưa có định nghĩa KPI cho vai trò {user.RoleCode}.");

        foreach (var definition in definitions)
        {
            var calculated = await CalculateMetricAsync(definition, userId, period, ct);
            var snapshot = await db.KpiScoreSnapshots.SingleOrDefaultAsync(item =>
                item.KpiPeriodId == period.Id && item.KpiDefinitionId == definition.Id && item.UserId == userId, ct);
            if (snapshot is null)
            {
                snapshot = new KpiScoreSnapshot
                {
                    KpiPeriodId = period.Id,
                    KpiDefinitionId = definition.Id,
                    UserId = userId,
                };
                db.KpiScoreSnapshots.Add(snapshot);
            }
            snapshot.DefinitionVersion = definition.Version;
            snapshot.DefinitionCode = definition.Code;
            snapshot.DefinitionNameKey = definition.NameKey;
            snapshot.SourceModule = definition.SourceModule;
            snapshot.MetricCode = definition.MetricCode;
            snapshot.DefinitionWeight = definition.Weight;
            snapshot.TargetValue = definition.TargetValue;
            snapshot.MinimumAcceptableScore = definition.MinimumAcceptableScore;
            snapshot.TargetDirection = definition.TargetDirection;
            snapshot.RawValue = calculated.RawValue;
            snapshot.Numerator = calculated.Numerator;
            snapshot.Denominator = calculated.Denominator;
            snapshot.Score = calculated.Score;
            snapshot.WeightedScore = calculated.Score.HasValue
                ? Math.Round(calculated.Score.Value * definition.Weight, 4)
                : null;
            snapshot.Status = calculated.Status;
            snapshot.EvidenceJson = calculated.EvidenceJson;
            snapshot.CalculatedAt = DateTime.UtcNow;
        }
        period.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var lowScoreCount = await db.KpiScoreSnapshots.CountAsync(item =>
            item.KpiPeriodId == period.Id && item.UserId == userId &&
            item.Status == KpiScoreStatus.Available &&
            item.MinimumAcceptableScore != null &&
            item.Score < item.MinimumAcceptableScore, ct);
        if (lowScoreCount > 0 && !await db.Notifications.AnyAsync(item =>
            item.UserId == userId && item.Module == "analytics.kpi" &&
            item.RefEntityType == "KpiPeriod" && item.RefEntityId == period.Id, ct))
        {
            await notifications.NotifyFromTemplateAsync(
                userId,
                "kpi.low-score",
                new Dictionary<string, string>
                {
                    ["period"] = $"{year:D4}-{month:D2}",
                    ["count"] = lowScoreCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                "KpiPeriod",
                period.Id,
                $"/admin/kpi?year={year}&month={month}");
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetDashboardAsync(year, month, userId, ct))!;
    }

    public async Task<KpiDashboardResponse?> GetDashboardAsync(
        int year,
        int month,
        int userId,
        CancellationToken ct = default)
    {
        ValidatePeriod(year, month);
        var user = await UserAsync(userId, ct);
        if (user is null) return null;
        var period = await db.KpiPeriods.AsNoTracking()
            .Include(item => item.LockedByUser)
            .SingleOrDefaultAsync(item => item.Year == year && item.Month == month && item.UserId == userId, ct);
        if (period is null) return null;
        var snapshots = await db.KpiScoreSnapshots.AsNoTracking()
            .Where(item => item.KpiPeriodId == period.Id && item.UserId == userId)
            .OrderBy(item => item.DefinitionCode)
            .ToListAsync(ct);
        var available = snapshots.Where(item => item.Status == KpiScoreStatus.Available).ToList();
        return new KpiDashboardResponse
        {
            PeriodId = period.Id,
            Year = year,
            Month = month,
            TimeZoneId = period.TimeZoneId,
            PeriodStatus = period.Status,
            PeriodRowVersion = CrmConcurrency.Encode(period.RowVersion),
            LockedAt = period.LockedAt,
            LockedByName = period.LockedByUser?.FullName ?? period.LockedByUser?.Email,
            LockNote = period.LockNote,
            UserId = userId,
            UserName = user.Name,
            RoleCode = user.RoleCode,
            TotalScore = available.Count == 0 ? null : Math.Round(available.Sum(item => item.WeightedScore ?? 0), 2),
            AvailableWeight = available.Sum(item => item.DefinitionWeight),
            IsComplete = snapshots.Count > 0 && snapshots.All(item => item.Status == KpiScoreStatus.Available),
            LastCalculatedAt = snapshots.Count == 0 ? null : snapshots.Max(item => item.CalculatedAt),
            Scores = snapshots.Select(MapScore).ToList(),
        };
    }

    public async Task<KpiDashboardResponse?> LockAsync(
        int year,
        int month,
        LockKpiPeriodRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        ValidatePeriod(year, month);
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length < 3)
            throw new KpiOperationException("Lý do khóa kỳ KPI phải có ít nhất 3 ký tự.");
        var targetUserId = request.UserId ?? callerUserId;
        var period = await db.KpiPeriods.SingleOrDefaultAsync(item =>
            item.Year == year && item.Month == month && item.UserId == targetUserId, ct);
        if (period is null) return null;
        if (period.Status == KpiPeriodStatus.Locked)
            throw new KpiOperationException("Kỳ KPI đã được khóa.");
        var snapshots = await db.KpiScoreSnapshots.AsNoTracking()
            .Where(item => item.KpiPeriodId == period.Id)
            .ToListAsync(ct);
        if (snapshots.Count == 0)
            throw new KpiOperationException("Không thể khóa kỳ KPI chưa có kết quả tính.");
        if (snapshots.Any(item => item.Status != KpiScoreStatus.Available))
            throw new KpiOperationException("Không thể khóa kỳ KPI khi còn chỉ số thiếu dữ liệu hoặc cấu hình.");
        if (Math.Abs(snapshots.Sum(item => item.DefinitionWeight) - 1m) > 0.0001m)
            throw new KpiOperationException("Không thể khóa kỳ KPI khi tổng trọng số khác 100%.");
        CrmConcurrency.Apply(db, period, request.RowVersion);
        period.Status = KpiPeriodStatus.Locked;
        period.LockedAt = DateTime.UtcNow;
        period.LockedByUserId = callerUserId;
        period.LockNote = request.Note.Trim();
        period.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetDashboardAsync(year, month, targetUserId, ct);
    }

    private async Task<MetricResult> CalculateMetricAsync(
        KpiDefinition definition,
        int userId,
        KpiPeriod period,
        CancellationToken ct)
    {
        return definition.MetricCode switch
        {
            "SalesLeadConversionRate" => await SalesConversionAsync(userId, period, ct),
            "SalesNewContractRevenue" => await SalesRevenueAsync(definition, userId, period, ct),
            "SalesFirstInteractionHours" => await SalesFirstResponseAsync(definition, userId, period, ct),
            "TenderWinRate" => await TenderWinRateAsync(userId, period, ct),
            "TenderOnTimePreparationRate" => await TenderOnTimeAsync(userId, period, ct),
            "DesignReleaseOnTimeRate" => await DesignOnTimeAsync(userId, period, ct),
            "DesignFirstPassRate" => await DesignFirstPassAsync(userId, period, ct),
            "DesignSiteErrorCount" => await DesignSiteErrorCountAsync(definition, userId, period, ct),
            "SiteProgressVariance" => await SiteProgressVarianceAsync(definition, userId, period, ct),
            "FirstAcceptanceRate" => await FirstAcceptanceAsync(userId, period, ct),
            _ => MissingData(definition, "The required source event is not implemented in the current module."),
        };
    }

    private async Task<MetricResult> SalesConversionAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var leads = await db.Leads.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.CreatedAt >= period.PeriodStartUtc && item.CreatedAt < period.PeriodEndUtc)
            .Select(item => new { item.Id, item.Status, item.ConvertedAt })
            .ToListAsync(ct);
        var converted = leads.Count(item => item.Status == LeadStatus.Converted && item.ConvertedAt < period.PeriodEndUtc);
        return Ratio("Lead", leads.Select(item => item.Id), converted, leads.Count);
    }

    private async Task<MetricResult> SalesRevenueAsync(KpiDefinition definition, int userId, KpiPeriod period, CancellationToken ct)
    {
        var rows = await db.Contracts.AsNoTracking()
            .Where(item => item.OwnerUserId == userId &&
                item.Direction == ContractDirection.Upstream &&
                item.SignedDate >= period.PeriodStartUtc &&
                item.SignedDate < period.PeriodEndUtc &&
                item.Status != ContractStatus.Cancelled)
            .Select(item => new { item.Id, item.Value })
            .ToListAsync(ct);
        var raw = rows.Sum(item => item.Value);
        return AgainstTarget(definition, raw, rows.Select(item => item.Id), "Contract");
    }

    private async Task<MetricResult> SalesFirstResponseAsync(KpiDefinition definition, int userId, KpiPeriod period, CancellationToken ct)
    {
        var leads = await db.Leads.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.CreatedAt >= period.PeriodStartUtc && item.CreatedAt < period.PeriodEndUtc)
            .Select(item => new
            {
                item.Id,
                item.CreatedAt,
                FirstInteraction = item.Activities.Where(activity => activity.Type != LeadActivityType.Note)
                    .OrderBy(activity => activity.CreatedAt).Select(activity => (DateTime?)activity.CreatedAt).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var responded = leads.Where(item => item.FirstInteraction.HasValue).ToList();
        if (responded.Count == 0)
            return Missing("Lead", leads.Select(item => item.Id), "No qualifying first interaction in the period.");
        var raw = (decimal)responded.Average(item => (item.FirstInteraction!.Value - item.CreatedAt).TotalHours);
        return AgainstTarget(definition, raw, responded.Select(item => item.Id), "Lead");
    }

    private async Task<MetricResult> TenderWinRateAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var tenders = await db.Tenders.AsNoTracking()
            .Where(item => item.PreparerUserId == userId && item.ClosedAt >= period.PeriodStartUtc && item.ClosedAt < period.PeriodEndUtc &&
                (item.Status == TenderStatus.Won || item.Status == TenderStatus.Lost))
            .Select(item => new { item.Id, item.Status })
            .ToListAsync(ct);
        return Ratio("Tender", tenders.Select(item => item.Id), tenders.Count(item => item.Status == TenderStatus.Won), tenders.Count);
    }

    private async Task<MetricResult> TenderOnTimeAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var items = await db.TenderChecklistItems.AsNoTracking()
            .Where(item => item.Tender.PreparerUserId == userId && item.UpdatedAt >= period.PeriodStartUtc && item.UpdatedAt < period.PeriodEndUtc && item.InternalDeadline != null &&
                (item.Status == TenderChecklistItemStatus.Done || item.Status == TenderChecklistItemStatus.Submitted))
            .Select(item => new { item.Id, item.InternalDeadline, item.UpdatedAt })
            .ToListAsync(ct);
        return Ratio("TenderChecklistItem", items.Select(item => item.Id), items.Count(item => item.UpdatedAt <= item.InternalDeadline), items.Count);
    }

    private async Task<MetricResult> DesignOnTimeAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var (localStart, localEnd) = LocalDateRange(period);
        var tasks = await db.DesignScheduleTasks.AsNoTracking()
            .Where(item => item.AssigneeMember.UserId == userId && item.ActualEnd >= localStart && item.ActualEnd < localEnd)
            .Select(item => new { item.Id, item.PlannedEnd, item.ActualEnd })
            .ToListAsync(ct);
        return Ratio("DesignScheduleTask", tasks.Select(item => item.Id),
            tasks.Count(item => item.ActualEnd <= item.PlannedEnd), tasks.Count);
    }

    private async Task<MetricResult> DesignFirstPassAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var basic = await db.BasicDesignDocs.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.UpdatedAt >= period.PeriodStartUtc && item.UpdatedAt < period.PeriodEndUtc &&
                (item.Status == BasicDesignDocStatus.InternallyApproved || item.Status == BasicDesignDocStatus.SubmittedForPermit || item.Status == BasicDesignDocStatus.PermitApproved))
            .Select(item => new
            {
                EntityType = DrawingRevisionTargetType.BasicDesignDoc,
                item.Id,
                RevisionCount = db.DrawingRevisions.Count(revision => revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc && revision.TargetId == item.Id),
            })
            .ToListAsync(ct);
        var shop = await db.ShopDrawings.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.UpdatedAt >= period.PeriodStartUtc && item.UpdatedAt < period.PeriodEndUtc &&
                (item.Status == ShopDrawingStatus.Approved || item.Status == ShopDrawingStatus.PendingIfc || item.Status == ShopDrawingStatus.Released))
            .Select(item => new
            {
                EntityType = DrawingRevisionTargetType.ShopDrawing,
                item.Id,
                RevisionCount = db.DrawingRevisions.Count(revision => revision.TargetType == DrawingRevisionTargetType.ShopDrawing && revision.TargetId == item.Id),
            })
            .ToListAsync(ct);
        var ids = basic.Select(item => $"BasicDesignDoc:{item.Id}")
            .Concat(shop.Select(item => $"ShopDrawing:{item.Id}"))
            .ToList();
        var count = basic.Count + shop.Count;
        if (count == 0) return Missing("Drawing", [], "No approved or released drawings in the period.");
        var firstPass = basic.Count(item => item.RevisionCount == 0) + shop.Count(item => item.RevisionCount == 0);
        var score = Math.Round((decimal)firstPass / count * 100m, 4);
        return new MetricResult(score, firstPass, count, score, KpiScoreStatus.Available,
            JsonSerializer.Serialize(new { EntityType = "Drawing", RecordIds = ids, Reason = (string?)null }));
    }

    private async Task<MetricResult> SiteProgressVarianceAsync(
        KpiDefinition definition,
        int userId,
        KpiPeriod period,
        CancellationToken ct)
    {
        var (localStart, localEnd) = LocalDateRange(period);
        var tasks = await db.ConstructionTasks.AsNoTracking()
            .Where(item => item.OwnerUserId == userId && item.Status == ConstructionTaskStatus.Completed &&
                item.ActualEnd >= localStart && item.ActualEnd < localEnd)
            .Select(item => new { item.Id, item.PlannedStart, item.PlannedEnd, item.ActualStart, item.ActualEnd })
            .ToListAsync(ct);
        if (tasks.Count == 0) return Missing("ConstructionTask", [], "No completed construction tasks in the period.");
        var variances = tasks.Select(item =>
        {
            var plannedDays = Math.Max(item.PlannedEnd.DayNumber - item.PlannedStart.DayNumber + 1, 1);
            var actualStart = item.ActualStart ?? item.PlannedStart;
            var actualDays = Math.Max(item.ActualEnd!.Value.DayNumber - actualStart.DayNumber + 1, 1);
            return Math.Abs((decimal)(actualDays - plannedDays) / plannedDays * 100m);
        });
        return AgainstTarget(definition, Math.Round(variances.Average(), 4), tasks.Select(item => item.Id), "ConstructionTask");
    }

    private async Task<MetricResult> DesignSiteErrorCountAsync(
        KpiDefinition definition,
        int userId,
        KpiPeriod period,
        CancellationToken ct)
    {
        var rows = await db.PunchItems.AsNoTracking()
            .Where(item => item.Status == PunchStatus.Verified &&
                item.RootCause == PunchRootCause.Design &&
                item.ResponsibleDesignUserId == userId &&
                item.RootCauseConfirmedAt != null &&
                item.VerifiedAt >= period.PeriodStartUtc &&
                item.VerifiedAt < period.PeriodEndUtc)
            .Select(item => item.Id)
            .ToListAsync(ct);
        return AgainstTarget(definition, rows.Count, rows, "PunchItem");
    }

    private async Task<MetricResult> FirstAcceptanceAsync(int userId, KpiPeriod period, CancellationToken ct)
    {
        var records = await db.AcceptanceRecords.AsNoTracking()
            .Where(item => item.CreatedByUserId == userId && item.ApprovedAt >= period.PeriodStartUtc && item.ApprovedAt < period.PeriodEndUtc && item.Status == AcceptanceStatus.Approved)
            .Select(item => new { item.Id, item.RevisionCount })
            .ToListAsync(ct);
        return Ratio("AcceptanceRecord", records.Select(item => item.Id),
            records.Count(item => item.RevisionCount == 0), records.Count);
    }

    private static (DateOnly Start, DateOnly End) LocalDateRange(KpiPeriod period) =>
        (new DateOnly(period.Year, period.Month, 1), new DateOnly(period.Year, period.Month, 1).AddMonths(1));

    private static MetricResult Ratio(string entityType, IEnumerable<int> ids, int numerator, int denominator)
    {
        var idList = ids.ToList();
        if (denominator == 0) return Missing(entityType, idList, "No denominator records in the period.");
        var score = Math.Round((decimal)numerator / denominator * 100m, 4);
        return new MetricResult(score, numerator, denominator, score, KpiScoreStatus.Available,
            Evidence(entityType, idList, null));
    }

    private static MetricResult AgainstTarget(KpiDefinition definition, decimal raw, IEnumerable<int> ids, string entityType)
    {
        var idList = ids.ToList();
        if (!definition.TargetValue.HasValue)
            return new MetricResult(raw, null, null, null, KpiScoreStatus.MissingConfiguration,
                Evidence(entityType, idList, "TargetValue is required for scoring."));
        var ratio = definition.TargetDirection == KpiTargetDirection.HigherIsBetter
            ? raw / definition.TargetValue.Value
            : raw <= 0 ? 1 : definition.TargetValue.Value / raw;
        var score = Math.Round(Math.Min(Math.Max(ratio * 100m, 0m), 100m), 4);
        return new MetricResult(raw, null, null, score, KpiScoreStatus.Available,
            Evidence(entityType, idList, null));
    }

    private static MetricResult MissingData(KpiDefinition definition, string reason) =>
        new(null, null, null, null, KpiScoreStatus.MissingData,
            Evidence(definition.SourceModule, [], reason));

    private static MetricResult Missing(string entityType, IEnumerable<int> ids, string reason) =>
        new(null, null, null, null, KpiScoreStatus.MissingData, Evidence(entityType, ids, reason));

    private static string Evidence(string entityType, IEnumerable<int> ids, string? reason) =>
        JsonSerializer.Serialize(new { EntityType = entityType, RecordIds = ids, Reason = reason });

    private async Task<KpiPeriod> GetOrCreatePeriodAsync(int year, int month, int userId, CancellationToken ct)
    {
        var existing = await db.KpiPeriods.SingleOrDefaultAsync(item =>
            item.Year == year && item.Month == month && item.UserId == userId, ct);
        if (existing is not null) return existing;
        var timeZone = ResolveTimeZone();
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEnd = localStart.AddMonths(1);
        var period = new KpiPeriod
        {
            Year = year,
            Month = month,
            UserId = userId,
            TimeZoneId = TimeZoneId,
            PeriodStartUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            PeriodEndUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone),
        };
        db.KpiPeriods.Add(period);
        await db.SaveChangesAsync(ct);
        return period;
    }

    private async Task<UserInfo?> UserAsync(int userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(user => user.Id == userId && user.IsActive)
            .Select(user => new UserInfo(
                user.Id,
                user.FullName ?? user.Email,
                KpiPosition(user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString())))
            .SingleOrDefaultAsync(ct);

    internal static string KpiPosition(string roleCode) => roleCode switch
    {
        "SALE" or "SALES_MANAGER" => "SALES",
        "DESIGN" or "DESIGN_LEAD" or "ARCHITECT" or "MEP_ENGINEER" or "STRUCT_ENGINEER" => "DESIGN",
        "PM" => "SITE",
        "QS" => "TENDERING",
        "PROCUREMENT" => "PROCUREMENT",
        "ACCOUNTANT" => "PROJECT_ACCOUNTING",
        _ => roleCode,
    };

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    private static void ValidatePeriod(int year, int month)
    {
        if (year is < 2020 or > 2100 || month is < 1 or > 12)
            throw new KpiOperationException("Kỳ KPI không hợp lệ.");
    }

    private static KpiDefinitionResponse MapDefinition(KpiDefinition item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        RoleCode = item.RoleCode,
        NameKey = item.NameKey,
        SourceModule = item.SourceModule,
        MetricCode = item.MetricCode,
        Weight = item.Weight,
        TargetValue = item.TargetValue,
        MinimumAcceptableScore = item.MinimumAcceptableScore,
        TargetDirection = item.TargetDirection,
        Version = item.Version,
        IsActive = item.IsActive,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
    };

    private static KpiScoreResponse MapScore(KpiScoreSnapshot item) => new()
    {
        Id = item.Id,
        DefinitionId = item.KpiDefinitionId,
        Code = item.DefinitionCode,
        NameKey = item.DefinitionNameKey,
        SourceModule = item.SourceModule,
        Weight = item.DefinitionWeight,
        TargetValue = item.TargetValue,
        MinimumAcceptableScore = item.MinimumAcceptableScore,
        RawValue = item.RawValue,
        Numerator = item.Numerator,
        Denominator = item.Denominator,
        Score = item.Score,
        WeightedScore = item.WeightedScore,
        Status = item.Status,
        EvidenceJson = item.EvidenceJson,
        DefinitionVersion = item.DefinitionVersion,
        CalculatedAt = item.CalculatedAt,
    };

    private sealed record UserInfo(int Id, string Name, string RoleCode);
    private sealed record MetricResult(
        decimal? RawValue,
        decimal? Numerator,
        decimal? Denominator,
        decimal? Score,
        KpiScoreStatus Status,
        string EvidenceJson);
}
