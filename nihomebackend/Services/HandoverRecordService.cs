using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class HandoverRecordService(
    AppDbContext db,
    ILogger<HandoverRecordService> logger) : IHandoverRecordService
{
    private const int MaxPageSize = 200;
    private static readonly HandoverStatus[] EditableStatuses =
        [HandoverStatus.Draft, HandoverStatus.Reopened];

    public async Task<HandoverRecordListResponse> ListAsync(
        HandoverRecordListParams parameters,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize <= 0 ? 20 : parameters.PageSize, 1, MaxPageSize);
        var query = ApplyFilters(ApplyScope(BaseQuery(), callerUserId, canSeeAll), parameters);

        if (parameters.ReadyOnly)
        {
            var readyProjectIds = await GetReadyProjectIdsAsync(ct);
            query = query.Where(row => readyProjectIds.Contains(row.DesignProjectId)
                && row.CommissioningCompleted && row.ChecklistCompleted);
        }

        var total = await query.CountAsync(ct);
        var rows = await ApplySort(query, parameters.SortBy, parameters.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var readiness = await GetReadinessAsync(rows.Select(row => row.DesignProjectId), rows, ct);

        var scope = ApplyScope(db.HandoverRecords.AsNoTracking(), callerUserId, canSeeAll);
        if (parameters.DesignProjectId.HasValue)
            scope = scope.Where(row => row.DesignProjectId == parameters.DesignProjectId.Value);
        var statusCounts = await scope.GroupBy(row => row.Status)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key.ToString(), item => item.Count, ct);
        var scopedRows = await scope.Select(row => new
        {
            row.DesignProjectId,
            row.CommissioningCompleted,
            row.ChecklistCompleted,
        }).ToListAsync(ct);
        var scopedReadiness = await GetReadinessAsync(
            scopedRows.Select(row => row.DesignProjectId), null, ct);
        var readyCount = scopedRows.Count(row => row.CommissioningCompleted
            && row.ChecklistCompleted
            && scopedReadiness.GetValueOrDefault(row.DesignProjectId)?.IsReady == true);

        return new HandoverRecordListResponse
        {
            Items = rows.Select(row => Map(row, readiness[row.DesignProjectId])).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            StatusCounts = statusCounts,
            ReadyCount = readyCount,
        };
    }

    public async Task<List<HandoverRecordResponse>> ExportAsync(
        HandoverRecordListParams parameters,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var query = ApplyFilters(ApplyScope(BaseQuery(), callerUserId, canSeeAll), parameters);
        if (parameters.ReadyOnly)
        {
            var readyProjectIds = await GetReadyProjectIdsAsync(ct);
            query = query.Where(row => readyProjectIds.Contains(row.DesignProjectId)
                && row.CommissioningCompleted && row.ChecklistCompleted);
        }
        var rows = await ApplySort(query, parameters.SortBy, parameters.SortDirection).ToListAsync(ct);
        var readiness = await GetReadinessAsync(rows.Select(row => row.DesignProjectId), rows, ct);
        return rows.Select(row => Map(row, readiness[row.DesignProjectId])).ToList();
    }

    public async Task<HandoverRecordResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var entity = await ApplyScope(BaseQuery(), callerUserId, canSeeAll)
            .FirstOrDefaultAsync(row => row.Id == id, ct);
        if (entity is null) return null;
        var readiness = await GetReadinessAsync([entity.DesignProjectId], [entity], ct);
        return Map(entity, readiness[entity.DesignProjectId]);
    }

    public async Task<HandoverRecordResponse> CreateAsync(
        CreateHandoverRecordRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        NormalizeCollections(request);
        ValidateRequest(request.Title, request.PlannedHandoverDate, request.Description,
            request.Location, request.CommissioningNotes, request.ChecklistItems,
            request.Documents, request.Signatories);
        var project = await db.DesignProjects.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.DesignProjectId, ct)
            ?? throw new HandoverRecordOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        await EnsureUserAsync(request.ResponsibleUserId, ct);
        if (!canSeeAll && project.ProjectManagerUserId != callerUserId
            && project.DesignLeadUserId != callerUserId)
        {
            throw new HandoverRecordOperationException("Bạn không thuộc phạm vi dự án đã chọn.");
        }
        if (await db.HandoverRecords.AnyAsync(row => row.DesignProjectId == request.DesignProjectId, ct))
            throw new HandoverRecordOperationException("Dự án đã có hồ sơ bàn giao.");

        var entity = new HandoverRecord
        {
            DesignProjectId = request.DesignProjectId,
            HandoverCode = await AllocateCodeAsync(ct),
            Title = request.Title.Trim(),
            Description = TrimOrNull(request.Description),
            PlannedHandoverDate = request.PlannedHandoverDate,
            Location = TrimOrNull(request.Location),
            ResponsibleUserId = request.ResponsibleUserId,
            CommissioningCompleted = request.CommissioningCompleted,
            CommissioningNotes = TrimOrNull(request.CommissioningNotes),
            ChecklistItems = JsonSerializer.Serialize(request.ChecklistItems),
            ChecklistCompleted = IsChecklistComplete(request.ChecklistItems),
            Documents = JsonSerializer.Serialize(request.Documents.Select(item => item.Trim())),
            Signatories = JsonSerializer.Serialize(request.Signatories.Select(item => item.Trim())),
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        entity.StatusHistory.Add(NewHistory(null, HandoverStatus.Draft, callerUserId, null));
        db.HandoverRecords.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Concurrent handover creation failed for project {ProjectId}", request.DesignProjectId);
            throw new HandoverRecordConflictException("Dự án đã có hồ sơ bàn giao hoặc mã bàn giao vừa được sử dụng. Vui lòng tải lại và thử lại.");
        }
        logger.LogInformation("HandoverRecord {Id} created for project {ProjectId}", entity.Id, entity.DesignProjectId);
        return (await GetAsync(entity.Id, callerUserId, true, ct))!;
    }

    public async Task<HandoverRecordResponse?> UpdateAsync(
        int id,
        UpdateHandoverRecordRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.HandoverRecords.Include(row => row.DesignProject), callerUserId, canSeeAll)
            .FirstOrDefaultAsync(row => row.Id == id, ct);
        if (entity is null) return null;
        if (!EditableStatuses.Contains(entity.Status))
            throw new HandoverRecordOperationException($"Không thể chỉnh sửa hồ sơ ở trạng thái '{entity.Status}'.");
        NormalizeCollections(request);
        ValidateRequest(request.Title, request.PlannedHandoverDate, request.Description,
            request.Location, request.CommissioningNotes, request.ChecklistItems,
            request.Documents, request.Signatories);
        await EnsureUserAsync(request.ResponsibleUserId, ct);
        if (!canSeeAll && request.ResponsibleUserId != entity.ResponsibleUserId
            && entity.DesignProject.ProjectManagerUserId != callerUserId
            && entity.DesignProject.DesignLeadUserId != callerUserId)
        {
            throw new HandoverRecordOperationException("Chỉ quản lý dự án hoặc trưởng nhóm thiết kế mới được thay đổi người phụ trách.");
        }

        entity.Title = request.Title.Trim();
        entity.Description = TrimOrNull(request.Description);
        entity.PlannedHandoverDate = request.PlannedHandoverDate;
        entity.Location = TrimOrNull(request.Location);
        entity.ResponsibleUserId = request.ResponsibleUserId;
        entity.CommissioningCompleted = request.CommissioningCompleted;
        entity.CommissioningNotes = TrimOrNull(request.CommissioningNotes);
        entity.ChecklistItems = JsonSerializer.Serialize(request.ChecklistItems);
        entity.ChecklistCompleted = IsChecklistComplete(request.ChecklistItems);
        entity.Documents = JsonSerializer.Serialize(request.Documents.Select(item => item.Trim()));
        entity.Signatories = JsonSerializer.Serialize(request.Signatories.Select(item => item.Trim()));
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await SaveWithConcurrencyAsync(ct);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<HandoverRecordResponse?> TransitionAsync(
        int id,
        TransitionHandoverStatusRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.HandoverRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(row => row.Id == id, ct);
        if (entity is null) return null;
        if (!Enum.TryParse<HandoverStatus>(request.Status, true, out var next))
            throw new HandoverRecordOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        if (next == HandoverStatus.HandedOver)
            throw new HandoverRecordOperationException("Sử dụng endpoint /complete để hoàn tất bàn giao.");
        ValidateTransitionNote(request.Note);
        EnsureTransitionAllowed(entity.Status, next);
        if (next == HandoverStatus.ReadyForHandover)
            await EnsureReadyAsync(entity, ct);
        ApplyTransition(entity, next, callerUserId, request.Note);
        await SaveWithConcurrencyAsync(ct);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<HandoverRecordResponse?> CompleteAsync(
        int id,
        TransitionHandoverStatusRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.HandoverRecords, callerUserId, canSeeAll)
            .FirstOrDefaultAsync(row => row.Id == id, ct);
        if (entity is null) return null;
        ValidateTransitionNote(request.Note);
        EnsureTransitionAllowed(entity.Status, HandoverStatus.HandedOver);
        await EnsureReadyAsync(entity, ct);
        if (DeserializeStrings(entity.Signatories).Count == 0)
            throw new HandoverRecordOperationException("Cần ít nhất một bên ký trước khi hoàn tất bàn giao.");
        ApplyTransition(entity, HandoverStatus.HandedOver, callerUserId, request.Note);
        await SaveWithConcurrencyAsync(ct);
        return await GetAsync(id, callerUserId, true, ct);
    }

    public async Task<bool> DeleteAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default)
    {
        var entity = await ApplyScope(db.HandoverRecords, callerUserId, canSeeAll)
            .Include(row => row.StatusHistory)
            .FirstOrDefaultAsync(row => row.Id == id, ct);
        if (entity is null) return false;
        db.HandoverStatusHistory.RemoveRange(entity.StatusHistory);
        db.HandoverRecords.Remove(entity);
        await SaveWithConcurrencyAsync(ct);
        return true;
    }

    private IQueryable<HandoverRecord> BaseQuery() => db.HandoverRecords.AsNoTracking()
        .Include(row => row.DesignProject)
        .Include(row => row.ResponsibleUser)
        .Include(row => row.SubmittedBy)
        .Include(row => row.HandedOverBy)
        .Include(row => row.StatusHistory)
            .ThenInclude(history => history.ChangedByUser);

    private static IQueryable<HandoverRecord> ApplyScope(
        IQueryable<HandoverRecord> query, int callerUserId, bool canSeeAll)
    {
        if (canSeeAll) return query;
        return query.Where(row => row.CreatedByUserId == callerUserId
            || row.ResponsibleUserId == callerUserId
            || row.DesignProject.ProjectManagerUserId == callerUserId
            || row.DesignProject.DesignLeadUserId == callerUserId);
    }

    private static IQueryable<HandoverRecord> ApplyFilters(
        IQueryable<HandoverRecord> query, HandoverRecordListParams parameters)
    {
        if (parameters.DesignProjectId.HasValue)
            query = query.Where(row => row.DesignProjectId == parameters.DesignProjectId.Value);
        if (parameters.ResponsibleUserId.HasValue)
            query = query.Where(row => row.ResponsibleUserId == parameters.ResponsibleUserId.Value);
        if (parameters.PlannedFrom.HasValue)
            query = query.Where(row => row.PlannedHandoverDate >= parameters.PlannedFrom.Value);
        if (parameters.PlannedTo.HasValue)
            query = query.Where(row => row.PlannedHandoverDate <= parameters.PlannedTo.Value);
        if (!string.IsNullOrWhiteSpace(parameters.Status)
            && Enum.TryParse<HandoverStatus>(parameters.Status, true, out var status))
            query = query.Where(row => row.Status == status);
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var term = parameters.Search.Trim();
            query = query.Where(row => EF.Functions.Like(row.Title, $"%{term}%")
                || EF.Functions.Like(row.HandoverCode, $"%{term}%")
                || EF.Functions.Like(row.DesignProject.Name, $"%{term}%"));
        }
        return query;
    }

    private static IOrderedQueryable<HandoverRecord> ApplySort(
        IQueryable<HandoverRecord> query, string? sortBy, string? sortDirection)
    {
        var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant(), desc) switch
        {
            ("code", false) => query.OrderBy(row => row.HandoverCode),
            ("code", true) => query.OrderByDescending(row => row.HandoverCode),
            ("title", false) => query.OrderBy(row => row.Title),
            ("title", true) => query.OrderByDescending(row => row.Title),
            ("project", false) => query.OrderBy(row => row.DesignProject.Name),
            ("project", true) => query.OrderByDescending(row => row.DesignProject.Name),
            ("status", false) => query.OrderBy(row => row.Status),
            ("status", true) => query.OrderByDescending(row => row.Status),
            ("updatedat", false) => query.OrderBy(row => row.UpdatedAt),
            ("updatedat", true) => query.OrderByDescending(row => row.UpdatedAt),
            (_, false) => query.OrderBy(row => row.PlannedHandoverDate),
            _ => query.OrderByDescending(row => row.PlannedHandoverDate),
        };
    }

    private async Task<HashSet<int>> GetReadyProjectIdsAsync(CancellationToken ct)
    {
        var projectIds = await db.DesignProjects.AsNoTracking().Select(item => item.Id).ToListAsync(ct);
        var readiness = await GetReadinessAsync(projectIds, null, ct);
        return readiness.Where(item => item.Value.IsReady).Select(item => item.Key).ToHashSet();
    }

    private async Task<Dictionary<int, HandoverReadinessResponse>> GetReadinessAsync(
        IEnumerable<int> projectIds,
        IEnumerable<HandoverRecord>? records,
        CancellationToken ct)
    {
        var ids = projectIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => new HandoverReadinessResponse
        {
            RequiredAsBuiltCategories = AsBuiltCategoryExtensions.Required.Length,
        });
        if (ids.Count == 0) return result;

        var approvedCategories = await db.AsBuiltDocuments.AsNoTracking()
            .Where(item => ids.Contains(item.DesignProjectId) && item.Status == AsBuiltStatus.Approved)
            .Select(item => new { item.DesignProjectId, item.Category })
            .Distinct().ToListAsync(ct);
        foreach (var group in approvedCategories.GroupBy(item => item.DesignProjectId))
            result[group.Key].ApprovedRequiredAsBuiltCategories = group.Count(item => AsBuiltCategoryExtensions.Required.Contains(item.Category));

        var unresolved = await db.PunchItems.AsNoTracking()
            .Where(item => ids.Contains(item.DesignProjectId)
                && item.Status != PunchStatus.Verified && item.Status != PunchStatus.Cancelled)
            .GroupBy(item => item.DesignProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        foreach (var item in unresolved) result[item.ProjectId].UnresolvedPunchItems = item.Count;

        var acceptances = await db.AcceptanceRecords.AsNoTracking()
            .Where(item => ids.Contains(item.DesignProjectId) && item.Status == AcceptanceStatus.Approved)
            .GroupBy(item => item.DesignProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        foreach (var item in acceptances) result[item.ProjectId].ApprovedAcceptanceRecords = item.Count;

        var stateByProject = records?.ToDictionary(item => item.DesignProjectId) ?? new();
        foreach (var item in result)
        {
            if (stateByProject.TryGetValue(item.Key, out var record))
            {
                item.Value.CommissioningCompleted = record.CommissioningCompleted;
                item.Value.ChecklistCompleted = record.ChecklistCompleted;
            }
            item.Value.IsReady = item.Value.ApprovedRequiredAsBuiltCategories == item.Value.RequiredAsBuiltCategories
                && item.Value.UnresolvedPunchItems == 0
                && item.Value.ApprovedAcceptanceRecords > 0;
        }
        return result;
    }

    private async Task EnsureReadyAsync(HandoverRecord entity, CancellationToken ct)
    {
        var readiness = (await GetReadinessAsync([entity.DesignProjectId], [entity], ct))[entity.DesignProjectId];
        if (!entity.CommissioningCompleted)
            throw new HandoverRecordOperationException("Commissioning chưa hoàn tất.");
        if (!entity.ChecklistCompleted)
            throw new HandoverRecordOperationException("Checklist bàn giao chưa hoàn tất.");
        if (readiness.ApprovedAcceptanceRecords == 0)
            throw new HandoverRecordOperationException("Dự án chưa có biên bản nghiệm thu từng phần được duyệt.");
        if (readiness.UnresolvedPunchItems > 0)
            throw new HandoverRecordOperationException("Dự án còn lỗi tồn đọng chưa được xác minh.");
        if (readiness.ApprovedRequiredAsBuiltCategories < readiness.RequiredAsBuiltCategories)
            throw new HandoverRecordOperationException("Hồ sơ hoàn công chưa đủ các nhóm tài liệu bắt buộc đã duyệt.");
    }

    private static void EnsureTransitionAllowed(HandoverStatus from, HandoverStatus to)
    {
        var allowed = (from, to) switch
        {
            (HandoverStatus.Draft, HandoverStatus.ReadyForHandover) => true,
            (HandoverStatus.Draft, HandoverStatus.Cancelled) => true,
            (HandoverStatus.ReadyForHandover, HandoverStatus.Draft) => true,
            (HandoverStatus.ReadyForHandover, HandoverStatus.HandedOver) => true,
            (HandoverStatus.ReadyForHandover, HandoverStatus.Cancelled) => true,
            (HandoverStatus.HandedOver, HandoverStatus.Reopened) => true,
            (HandoverStatus.Reopened, HandoverStatus.ReadyForHandover) => true,
            (HandoverStatus.Reopened, HandoverStatus.Cancelled) => true,
            _ => false,
        };
        if (!allowed)
            throw new HandoverRecordOperationException($"Không thể chuyển trạng thái từ '{from}' sang '{to}'.");
    }

    private static void ApplyTransition(
        HandoverRecord entity, HandoverStatus next, int userId, string? note)
    {
        var previous = entity.Status;
        entity.Status = next;
        entity.ResolutionNote = TrimOrNull(note);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = userId;
        if (next == HandoverStatus.ReadyForHandover)
        {
            entity.SubmittedAt = DateTime.UtcNow;
            entity.SubmittedByUserId = userId;
        }
        if (next == HandoverStatus.HandedOver)
        {
            entity.HandedOverAt = DateTime.UtcNow;
            entity.HandedOverByUserId = userId;
            entity.ActualHandoverDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        if (next == HandoverStatus.Reopened)
        {
            entity.ReopenCount++;
            entity.ActualHandoverDate = null;
            entity.HandedOverAt = null;
            entity.HandedOverByUserId = null;
        }
        entity.StatusHistory.Add(NewHistory(previous, next, userId, note));
    }

    private static HandoverStatusHistory NewHistory(
        HandoverStatus? from, HandoverStatus to, int userId, string? note) => new()
        {
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            Note = TrimOrNull(note),
        };

    private static void ValidateRequest(
        string? title,
        DateOnly plannedDate,
        string? description,
        string? location,
        string? commissioningNotes,
        List<HandoverChecklistItemRequest>? checklist,
        List<string>? documents,
        List<string>? signatories)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new HandoverRecordOperationException("Tiêu đề bàn giao là bắt buộc.");
        if (title.Trim().Length > 300)
            throw new HandoverRecordOperationException("Tiêu đề không được vượt quá 300 ký tự.");
        if (plannedDate == default)
            throw new HandoverRecordOperationException("Ngày bàn giao dự kiến là bắt buộc.");
        EnsureLength(description, 4000, "Mô tả");
        EnsureLength(location, 300, "Địa điểm");
        EnsureLength(commissioningNotes, 4000, "Ghi chú commissioning");

        checklist ??= new();
        if (checklist.Count > 50 || checklist.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            throw new HandoverRecordOperationException("Checklist gồm tối đa 50 mục có tên hợp lệ.");
        if (checklist.Any(item => item.Name.Trim().Length > 300 || item.Note?.Trim().Length > 1000))
            throw new HandoverRecordOperationException("Mục checklist hoặc ghi chú vượt quá độ dài cho phép.");
        if (checklist.GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            throw new HandoverRecordOperationException("Checklist không được trùng tên mục.");
        if (JsonSerializer.Serialize(checklist).Length > 16000)
            throw new HandoverRecordOperationException("Checklist không được vượt quá 16000 ký tự.");

        documents ??= new();
        if (documents.Count > 20 || documents.Any(item => string.IsNullOrWhiteSpace(item)
            || item.Trim().Length > 500 || !IsSafeDocumentUrl(item)))
            throw new HandoverRecordOperationException("Tài liệu gồm tối đa 20 đường dẫn bắt đầu bằng / hoặc URL HTTP(S).");
        if (JsonSerializer.Serialize(documents).Length > 4000)
            throw new HandoverRecordOperationException("Danh sách tài liệu không được vượt quá 4000 ký tự.");

        signatories ??= new();
        if (signatories.Count > 20 || signatories.Any(item => string.IsNullOrWhiteSpace(item) || item.Trim().Length > 200))
            throw new HandoverRecordOperationException("Danh sách bên ký gồm tối đa 20 tên, mỗi tên tối đa 200 ký tự.");
        if (JsonSerializer.Serialize(signatories).Length > 5000)
            throw new HandoverRecordOperationException("Danh sách bên ký không được vượt quá 5000 ký tự.");
    }

    private static void NormalizeCollections(CreateHandoverRecordRequest request)
    {
        request.ChecklistItems ??= [];
        request.Documents ??= [];
        request.Signatories ??= [];
    }

    private static void NormalizeCollections(UpdateHandoverRecordRequest request)
    {
        request.ChecklistItems ??= [];
        request.Documents ??= [];
        request.Signatories ??= [];
    }

    private static void ValidateTransitionNote(string? note) =>
        EnsureLength(note, 2000, "Ghi chú chuyển trạng thái");

    private async Task SaveWithConcurrencyAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Concurrent handover update rejected");
            throw new HandoverRecordConflictException("Hồ sơ đã được người khác cập nhật. Vui lòng tải lại trước khi thử lại.");
        }
    }

    private static bool IsSafeDocumentUrl(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("/", StringComparison.Ordinal))
            return !trimmed.StartsWith("//", StringComparison.Ordinal)
                && !trimmed.StartsWith("/\\", StringComparison.Ordinal);
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }

    private async Task EnsureUserAsync(int userId, CancellationToken ct)
    {
        if (!await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive, ct))
            throw new HandoverRecordOperationException($"Người phụ trách #{userId} không tồn tại hoặc đã ngừng hoạt động.");
    }

    private async Task<string> AllocateCodeAsync(CancellationToken ct)
    {
        var codes = await db.HandoverRecords.AsNoTracking().Select(item => item.HandoverCode).ToListAsync(ct);
        var next = codes.Select(code => int.TryParse(code.Replace("HO-", "", StringComparison.OrdinalIgnoreCase), out var value) ? value : 0)
            .DefaultIfEmpty().Max() + 1;
        return $"HO-{next:D4}";
    }

    private static bool IsChecklistComplete(List<HandoverChecklistItemRequest> items) =>
        items.Count > 0 && items.All(item => item.IsCompleted);

    private static void EnsureLength(string? value, int maxLength, string field)
    {
        if (value?.Trim().Length > maxLength)
            throw new HandoverRecordOperationException($"{field} không được vượt quá {maxLength} ký tự.");
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> DeserializeStrings(string json) =>
        JsonSerializer.Deserialize<List<string>>(json) ?? new();

    private static HandoverRecordResponse Map(HandoverRecord entity, HandoverReadinessResponse readiness)
    {
        readiness.CommissioningCompleted = entity.CommissioningCompleted;
        readiness.ChecklistCompleted = entity.ChecklistCompleted;
        readiness.IsReady = readiness.IsReady && entity.CommissioningCompleted && entity.ChecklistCompleted;
        return new HandoverRecordResponse
        {
            Id = entity.Id,
            DesignProjectId = entity.DesignProjectId,
            DesignProjectName = entity.DesignProject.Name ?? string.Empty,
            HandoverCode = entity.HandoverCode,
            Title = entity.Title,
            Description = entity.Description,
            PlannedHandoverDate = entity.PlannedHandoverDate,
            ActualHandoverDate = entity.ActualHandoverDate,
            Location = entity.Location,
            ResponsibleUserId = entity.ResponsibleUserId,
            ResponsibleUserName = entity.ResponsibleUser.FullName ?? string.Empty,
            CommissioningCompleted = entity.CommissioningCompleted,
            CommissioningNotes = entity.CommissioningNotes,
            ChecklistItems = JsonSerializer.Deserialize<List<HandoverChecklistItemResponse>>(entity.ChecklistItems) ?? new(),
            Documents = DeserializeStrings(entity.Documents),
            Signatories = DeserializeStrings(entity.Signatories),
            ResolutionNote = entity.ResolutionNote,
            Status = entity.Status.ToString(),
            Readiness = readiness,
            StatusHistory = entity.StatusHistory.OrderByDescending(item => item.ChangedAt).Select(item => new HandoverStatusHistoryResponse
            {
                FromStatus = item.FromStatus?.ToString(),
                ToStatus = item.ToStatus.ToString(),
                Note = item.Note,
                ChangedByUserId = item.ChangedByUserId,
                ChangedByName = item.ChangedByUser.FullName ?? string.Empty,
                ChangedAt = item.ChangedAt,
            }).ToList(),
            SubmittedAt = entity.SubmittedAt,
            SubmittedByName = entity.SubmittedBy?.FullName,
            HandedOverAt = entity.HandedOverAt,
            HandedOverByName = entity.HandedOverBy?.FullName,
            ReopenCount = entity.ReopenCount,
            CreatedAt = entity.CreatedAt,
            CreatedByUserId = entity.CreatedByUserId,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}