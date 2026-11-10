using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class HseViolationService(AppDbContext db) : IHseViolationService
{
    public async Task<HseViolationListResponse> ListAsync(
        int projectId,
        HseViolationListParams parameters,
        bool includeSensitive,
        CancellationToken ct = default)
    {
        var query = BaseQuery().Where(item => item.OperationalProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            var statuses = ParseList<HseViolationStatus>(parameters.Status);
            if (statuses.Count > 0) query = query.Where(item => statuses.Contains(item.Status));
        }
        if (!string.IsNullOrWhiteSpace(parameters.Severity))
        {
            var severities = ParseList<HseViolationSeverity>(parameters.Severity);
            if (severities.Count > 0) query = query.Where(item => severities.Contains(item.Severity));
        }
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim();
            query = query.Where(item => EF.Functions.Like(item.Code, $"%{search}%") ||
                EF.Functions.Like(item.Description, $"%{search}%") ||
                EF.Functions.Like(item.Location, $"%{search}%") ||
                EF.Functions.Like(item.Category, $"%{search}%"));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(ct);
        var statusCounts = await db.HseViolations.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key.ToString(), Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, ct);
        return new HseViolationListResponse
        {
            Total = total,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            Items = rows.Select(item => Map(item, includeSensitive)).ToList(),
            StatusCounts = statusCounts,
        };
    }

    public async Task<HseViolationResponse?> GetAsync(
        int projectId,
        int id,
        bool includeSensitive,
        CancellationToken ct = default)
    {
        var entity = await BaseQuery().SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        return entity is null ? null : Map(entity, includeSensitive);
    }

    public async Task<HseViolationResponse> CreateAsync(
        int projectId,
        CreateHseViolationRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        await ValidateProjectAndFieldsAsync(projectId, request, ct);
        var offlineClientId = request.OfflineClientId.Trim();
        if (await db.HseViolations.AnyAsync(item =>
            item.OperationalProjectId == projectId && item.OfflineClientId == offlineClientId, ct))
            throw new HseViolationOperationException("Mã bản ghi ngoại tuyến đã được sử dụng trong dự án này.");

        var now = DateTime.UtcNow;
        var entity = new HseViolation
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync(projectId, ct),
            OfflineClientId = offlineClientId,
            Status = HseViolationStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        ApplyFields(entity, request);
        entity.Events.Add(NewEvent(entity, HseViolationEventType.Created, null, HseViolationStatus.Draft, null, callerUserId, now));
        db.HseViolations.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return (await GetAsync(projectId, entity.Id, true, ct))!;
    }

    public async Task<HseViolationResponse?> UpdateAsync(
        int projectId,
        int id,
        UpdateHseViolationRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.HseViolations.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != HseViolationStatus.Draft)
            throw new HseViolationOperationException("Chỉ được chỉnh sửa vi phạm HSE ở trạng thái Nháp.");
        await ValidateProjectAndFieldsAsync(projectId, request, ct);
        if (!string.Equals(entity.OfflineClientId, request.OfflineClientId.Trim(), StringComparison.Ordinal))
            throw new HseViolationOperationException("Không được thay đổi mã bản ghi ngoại tuyến.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        ApplyFields(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetAsync(projectId, id, true, ct);
    }

    public async Task<HseViolationResponse?> TransitionAsync(
        int projectId,
        int id,
        HseViolationStatus next,
        TransitionHseViolationRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.HseViolations.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status == next) return await GetAsync(projectId, id, true, ct);
        EnsureTransitionAllowed(entity.Status, next, request.Reason);
        if ((next is HseViolationStatus.Confirmed or HseViolationStatus.Rejected) &&
            (entity.CreatedByUserId == callerUserId || entity.ReportedByUserId == callerUserId))
            throw new HseViolationOperationException(
                "Người lập hoặc gửi vi phạm HSE không được tự xác nhận hay từ chối. Hãy nhờ người khác có quyền kiểm tra hồ sơ này.",
                HseViolationOperationException.IndependentReviewRequiredCode);
        CrmConcurrency.Apply(db, entity, request.RowVersion);

        if (next == HseViolationStatus.Remediated)
        {
            var note = TrimOrNull(request.RemediationNote) ?? entity.RemediationNote;
            var ownerId = request.RemediationOwnerUserId ?? entity.RemediationOwnerUserId;
            if (!ownerId.HasValue || string.IsNullOrWhiteSpace(note))
                throw new HseViolationOperationException("Người phụ trách và ghi chú khắc phục là bắt buộc khi xác nhận đã khắc phục.");
            await ValidateProjectUserAsync(projectId, ownerId.Value, "Người phụ trách khắc phục", ct);
            entity.RemediationOwnerUserId = ownerId;
            entity.RemediationNote = note;
            entity.RemediationDeadline = request.RemediationDeadline ?? entity.RemediationDeadline;
        }

        var now = DateTime.UtcNow;
        var from = entity.Status;
        entity.Status = next;
        entity.DecisionReason = next is HseViolationStatus.Rejected or HseViolationStatus.Cancelled
            ? request.Reason!.Trim()
            : null;
        if (next == HseViolationStatus.Reported)
        {
            entity.ReportedAt = now;
            entity.ReportedByUserId = callerUserId;
        }
        if (next == HseViolationStatus.Confirmed)
        {
            entity.ConfirmedAt = now;
            entity.ConfirmedByUserId = callerUserId;
        }
        if (next == HseViolationStatus.Closed)
        {
            entity.ClosedAt = now;
            entity.ClosedByUserId = callerUserId;
        }
        entity.UpdatedAt = now;
        entity.UpdatedByUserId = callerUserId;
        entity.Events.Add(NewEvent(entity, HseViolationEventType.Transition, from, next, request.Reason, callerUserId, now));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetAsync(projectId, id, true, ct);
    }

    public async Task<HseViolationResponse?> CorrectAsync(
        int projectId,
        int id,
        CorrectHseViolationRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.HseViolations.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status is not (HseViolationStatus.Confirmed or HseViolationStatus.Remediated or HseViolationStatus.Closed))
            throw new HseViolationOperationException("Chỉ được lập điều chỉnh chính thức sau khi vi phạm đã được xác nhận.");
        await ValidateProjectAndFieldsAsync(projectId, request, ct);
        if (!string.Equals(entity.OfflineClientId, request.OfflineClientId.Trim(), StringComparison.Ordinal))
            throw new HseViolationOperationException("Không được thay đổi mã bản ghi ngoại tuyến.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        ApplyFields(entity, request);
        var now = DateTime.UtcNow;
        entity.UpdatedAt = now;
        entity.UpdatedByUserId = callerUserId;
        entity.Events.Add(NewEvent(entity, HseViolationEventType.Correction, entity.Status, entity.Status, request.Reason.Trim(), callerUserId, now));
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetAsync(projectId, id, true, ct);
    }

    public async Task<DeletionImpactResponse?> GetDeletionImpactAsync(int projectId, int id, CancellationToken ct = default)
    {
        var entity = await db.HseViolations.AsNoTracking().SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        if (entity is null) return null;
        var eventCount = await db.HseViolationEvents.CountAsync(item => item.HseViolationId == id, ct);
        var hasEvidence = entity.EvidenceDocumentsJson != "[]";
        var allowedStatus = entity.Status is HseViolationStatus.Draft or HseViolationStatus.Rejected or HseViolationStatus.Cancelled;
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"HseViolation:{id}:{entity.Code}:{entity.Status}:{CrmConcurrency.Encode(entity.RowVersion)}:{eventCount}:{entity.EvidenceDocumentsJson}"))).ToLowerInvariant();
        var items = new List<DeletionImpactItemResponse>();
        if (!allowedStatus)
            items.Add(new DeletionImpactItemResponse { Key = "construction.recordState", Action = DeletionImpactActions.Block, Count = 1, Examples = [entity.Status.ToString()] });
        if (hasEvidence)
            items.Add(new DeletionImpactItemResponse { Key = "construction.hseEvidenceCleanup", Action = DeletionImpactActions.Block, Count = 1 });
        if (eventCount > 0)
            items.Add(new DeletionImpactItemResponse { Key = "construction.hseEvents", Action = DeletionImpactActions.Delete, Count = eventCount });
        return new DeletionImpactResponse
        {
            ResourceType = nameof(HseViolation),
            ResourceId = id,
            ResourceLabel = entity.Code,
            RequiredConfirmation = entity.Code,
            PlanToken = token,
            CanDelete = allowedStatus && !hasEvidence,
            TotalAffected = 1 + eventCount,
            Items = items,
        };
    }

    public async Task<bool> DeleteAsync(int projectId, int id, ConfirmDeletionRequest request, CancellationToken ct = default)
    {
        var impact = await GetDeletionImpactAsync(projectId, id, ct);
        if (impact is null) return false;
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!impact.CanDelete)
            throw new HseViolationOperationException("Không thể xoá vi phạm HSE khi hồ sơ còn mở hoặc còn bằng chứng cần dọn an toàn.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new HseViolationOperationException($"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new HseViolationOperationException("Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại ảnh hưởng xoá.");
        var entity = await db.HseViolations.SingleOrDefaultAsync(item =>
            item.OperationalProjectId == projectId && item.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status is not (HseViolationStatus.Draft or HseViolationStatus.Rejected or HseViolationStatus.Cancelled))
            throw new HseViolationOperationException("Chỉ được xoá vi phạm HSE ở trạng thái Nháp, Từ chối hoặc Đã huỷ.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        db.HseViolations.Remove(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return true;
    }

    private IQueryable<HseViolation> BaseQuery() => db.HseViolations.AsNoTracking()
        .Include(item => item.OperationalProject)
        .Include(item => item.ResponsibleSiteUser)
        .Include(item => item.RemediationOwnerUser)
        .Include(item => item.ReportedBy)
        .Include(item => item.ConfirmedBy)
        .Include(item => item.ClosedBy)
        .Include(item => item.Events.OrderBy(entry => entry.CreatedAt)).ThenInclude(entry => entry.CreatedBy);

    private async Task ValidateProjectAndFieldsAsync(int projectId, HseViolationFieldsRequest request, CancellationToken ct)
    {
        if (!await db.OperationalProjects.AsNoTracking().AnyAsync(item => item.Id == projectId, ct))
            throw new HseViolationOperationException($"Dự án vận hành #{projectId} không tồn tại.");
        if (!request.OccurredAt.HasValue)
            throw new HseViolationOperationException("Thời điểm xảy ra là bắt buộc.");
        if (!Enum.TryParse<HseViolationSeverity>(request.Severity, true, out _))
            throw new HseViolationOperationException($"Mức độ '{request.Severity}' không hợp lệ.");
        if (request.RemediationDeadline.HasValue && request.RemediationDeadline < request.OccurredAt)
            throw new HseViolationOperationException("Hạn khắc phục không được trước thời điểm xảy ra.");
        await ValidateProjectUserAsync(projectId, request.ResponsibleSiteUserId, "Người chịu trách nhiệm tại công trường", ct);
        if (request.RemediationOwnerUserId.HasValue)
            await ValidateProjectUserAsync(projectId, request.RemediationOwnerUserId.Value, "Người phụ trách khắc phục", ct);
        ValidateEvidenceDocuments(request.EvidenceDocuments);
    }

    private async Task ValidateProjectUserAsync(int projectId, int userId, string fieldName, CancellationToken ct)
    {
        var valid = await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            (db.OperationalProjects.Any(project => project.Id == projectId && project.ProjectManagerUserId == userId) ||
             db.OperationalProjectMembers.Any(member => member.OperationalProjectId == projectId &&
                 member.UserId == userId && member.EndedAt == null)), ct);
        if (!valid)
            throw new HseViolationOperationException($"{fieldName} phải là thành viên đang hoạt động của dự án.");
    }

    private static void ValidateEvidenceDocuments(IReadOnlyList<string> documents)
    {
        if (documents.Any(path => string.IsNullOrWhiteSpace(path) || path.Length > 500 ||
            !path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)))
            throw new HseViolationOperationException("Tài liệu minh chứng phải dùng đường dẫn nội bộ bắt đầu bằng '/'.");
    }

    private static void ApplyFields(HseViolation entity, HseViolationFieldsRequest request)
    {
        entity.OccurredAt = request.OccurredAt!.Value.ToUniversalTime();
        entity.Location = request.Location.Trim();
        entity.Category = request.Category.Trim();
        entity.Severity = Enum.Parse<HseViolationSeverity>(request.Severity, true);
        entity.Description = request.Description.Trim();
        entity.RegulatoryReference = TrimOrNull(request.RegulatoryReference);
        entity.EvidenceDocumentsJson = JsonSerializer.Serialize(request.EvidenceDocuments.Distinct().ToList());
        entity.ResponsibleSiteUserId = request.ResponsibleSiteUserId;
        entity.RemediationOwnerUserId = request.RemediationOwnerUserId;
        entity.RemediationDeadline = request.RemediationDeadline?.ToUniversalTime();
        entity.RemediationNote = TrimOrNull(request.RemediationNote);
        entity.PenaltyReference = TrimOrNull(request.PenaltyReference);
        entity.PenaltyAmount = request.PenaltyAmount;
    }

    private async Task<string> AllocateCodeAsync(int projectId, CancellationToken ct)
    {
        var codes = await db.HseViolations.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => item.Code)
            .ToListAsync(ct);
        var sequence = codes.Select(code => code.StartsWith("HSE-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code[4..], out var value) ? value : 0)
            .DefaultIfEmpty(0).Max() + 1;
        return $"HSE-{sequence:D4}";
    }

    private static void EnsureTransitionAllowed(HseViolationStatus from, HseViolationStatus to, string? reason)
    {
        var allowed = (from, to) switch
        {
            (HseViolationStatus.Draft, HseViolationStatus.Reported) => true,
            (HseViolationStatus.Reported, HseViolationStatus.Confirmed) => true,
            (HseViolationStatus.Reported, HseViolationStatus.Rejected) => true,
            (HseViolationStatus.Confirmed, HseViolationStatus.Remediated) => true,
            (HseViolationStatus.Confirmed, HseViolationStatus.Cancelled) => true,
            (HseViolationStatus.Remediated, HseViolationStatus.Closed) => true,
            _ => false,
        };
        if (!allowed)
            throw new HseViolationOperationException($"Không thể chuyển vi phạm HSE từ '{from}' sang '{to}'.");
        if (to is HseViolationStatus.Rejected or HseViolationStatus.Cancelled && string.IsNullOrWhiteSpace(reason))
            throw new HseViolationOperationException("Lý do là bắt buộc khi từ chối hoặc hủy vi phạm HSE.");
    }

    private static HseViolationEvent NewEvent(
        HseViolation entity,
        HseViolationEventType type,
        HseViolationStatus? from,
        HseViolationStatus to,
        string? reason,
        int userId,
        DateTime now) => new()
        {
            Type = type,
            FromStatus = from,
            ToStatus = to,
            Reason = TrimOrNull(reason),
            SnapshotJson = JsonSerializer.Serialize(new
            {
                entity.OccurredAt,
                entity.Location,
                entity.Category,
                Severity = entity.Severity.ToString(),
                entity.Description,
                entity.ResponsibleSiteUserId,
                entity.RemediationOwnerUserId,
                entity.RemediationDeadline,
                entity.RemediationNote,
                Status = to.ToString(),
            }),
            CreatedByUserId = userId,
            CreatedAt = now,
        };

    private static HseViolationResponse Map(HseViolation item, bool includeSensitive) => new()
    {
        Id = item.Id,
        OperationalProjectId = item.OperationalProjectId,
        OperationalProjectCode = item.OperationalProject.Code,
        OperationalProjectName = item.OperationalProject.Name,
        Code = item.Code,
        OfflineClientId = item.OfflineClientId,
        OccurredAt = item.OccurredAt,
        Location = item.Location,
        Category = item.Category,
        Severity = item.Severity.ToString(),
        Description = item.Description,
        RegulatoryReference = item.RegulatoryReference,
        EvidenceDocuments = JsonSerializer.Deserialize<List<string>>(item.EvidenceDocumentsJson) ?? [],
        ResponsibleSiteUserId = item.ResponsibleSiteUserId,
        ResponsibleSiteUserName = item.ResponsibleSiteUser.FullName,
        RemediationOwnerUserId = item.RemediationOwnerUserId,
        RemediationOwnerUserName = item.RemediationOwnerUser?.FullName,
        RemediationDeadline = item.RemediationDeadline,
        RemediationNote = item.RemediationNote,
        PenaltyReference = includeSensitive ? item.PenaltyReference : null,
        PenaltyAmount = includeSensitive ? item.PenaltyAmount : null,
        Status = item.Status.ToString(),
        ReportedAt = item.ReportedAt,
        ReportedByUserId = item.ReportedByUserId,
        ReportedByName = item.ReportedBy?.FullName,
        ConfirmedAt = item.ConfirmedAt,
        ConfirmedByUserId = item.ConfirmedByUserId,
        ConfirmedByName = item.ConfirmedBy?.FullName,
        ClosedAt = item.ClosedAt,
        ClosedByUserId = item.ClosedByUserId,
        ClosedByName = item.ClosedBy?.FullName,
        DecisionReason = item.DecisionReason,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Events = item.Events.Select(entry => new HseViolationEventResponse
        {
            Id = entry.Id,
            Type = entry.Type.ToString(),
            FromStatus = entry.FromStatus?.ToString(),
            ToStatus = entry.ToStatus.ToString(),
            Reason = entry.Reason,
            CreatedByUserId = entry.CreatedByUserId,
            CreatedByName = entry.CreatedBy.FullName,
            CreatedAt = entry.CreatedAt,
        }).ToList(),
    };

    private static List<TEnum> ParseList<TEnum>(string value) where TEnum : struct, Enum =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => Enum.TryParse<TEnum>(entry, true, out var parsed) ? (TEnum?)parsed : null)
            .Where(entry => entry.HasValue)
            .Select(entry => entry!.Value)
            .ToList();

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
