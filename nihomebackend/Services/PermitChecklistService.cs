using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// NIH-137 permit checklist service — see <see cref="IPermitChecklistService"/>.
/// </summary>
public class PermitChecklistService(
    AppDbContext db,
    IBusinessDocumentStorageService documentStorage,
    ILogger<PermitChecklistService> logger,
    IProjectDocumentStagingService projectDocuments) : IPermitChecklistService
{
    private const int MaxPageSize = 200;
    private const string PermitTypeCategory = "permit_type";
    private const int DueSoonDays = 7;
    private const int ExpiringSoonDays = 30;

    public async Task<PermitChecklistListResponse> ListAsync(
        PermitChecklistListParams p,
        CancellationToken ct = default,
        IReadOnlySet<int>? accessibleOperationalProjectIds = null)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.PermitChecklistItems
            .AsNoTracking()
            .Include(x => x.OperationalProject)
            .Include(x => x.DesignProject)
            .Include(x => x.Owner)
            .Include(x => x.WorkTask)!.ThenInclude(task => task!.AssigneeUser)
            .Include(x => x.WorkTask)!.ThenInclude(task => task!.ApproverUser)
            .AsQueryable();

        if (accessibleOperationalProjectIds is not null)
            q = q.Where(x => accessibleOperationalProjectIds.Contains(x.OperationalProjectId));
        if (p.OperationalProjectId.HasValue) q = q.Where(x => x.OperationalProjectId == p.OperationalProjectId.Value);
        if (p.DesignProjectId.HasValue) q = q.Where(x => x.DesignProjectId == p.DesignProjectId.Value);
        if (p.OwnerUserId.HasValue)
            q = q.Where(x => x.WorkTask != null
                ? x.WorkTask.AssigneeUserId == p.OwnerUserId.Value
                : x.OwnerUserId == p.OwnerUserId.Value);
        if (p.ApproverUserId.HasValue)
            q = q.Where(x => x.WorkTask != null && x.WorkTask.ApproverUserId == p.ApproverUserId.Value);

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = ParseEnumCsv<PermitStatus>(p.Status);
            if (statuses.Count > 0) q = q.Where(x => statuses.Contains(x.Status));
        }
        if (!string.IsNullOrWhiteSpace(p.PermitTypeCode))
        {
            var codes = p.PermitTypeCode.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (codes.Count > 0) q = q.Where(x => codes.Contains(x.PermitTypeCode));
        }

        var now = DateTime.UtcNow;
        var dueSoonCutoff = now.AddDays(DueSoonDays);
        var expiringSoonCutoff = now.AddDays(ExpiringSoonDays);

        if (p.Overdue == true)
        {
            q = q.Where(x => (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) != null
                          && (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) < now
                          && x.Status != PermitStatus.Issued);
        }
        if (p.DueSoon == true)
        {
            q = q.Where(x => (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) != null
                          && (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) >= now
                          && (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) <= dueSoonCutoff
                          && x.Status != PermitStatus.Issued);
        }
        if (p.ExpiringSoon == true)
        {
            q = q.Where(x => x.Status == PermitStatus.Issued
                          && x.ExpiresAt != null
                          && x.ExpiresAt >= now
                          && x.ExpiresAt <= expiringSoonCutoff);
        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(x =>
                EF.Functions.Like(x.OperationalProject.Name, $"%{term}%") ||
                EF.Functions.Like(x.OperationalProject.Code, $"%{term}%") ||
                (x.IssuingAgency != null && EF.Functions.Like(x.IssuingAgency, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);

        // Overdue items and near-deadline items surface first so the risk
        // register naturally sorts to the top of every list.
        var rows = await q
            .OrderBy(x => x.Status == PermitStatus.Issued ? 1 : 0)
            .ThenBy(x => x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline ?? DateTime.MaxValue)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var typeCodes = rows.Select(r => r.PermitTypeCode).Distinct().ToList();
        var labelByCode = typeCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == PermitTypeCategory && typeCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        var response = new PermitChecklistListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, labelByCode, now, dueSoonCutoff, expiringSoonCutoff)).ToList(),
            Risk = await ComputeRiskAsync(
                now, dueSoonCutoff, expiringSoonCutoff, ct, accessibleOperationalProjectIds),
        };
        return response;
    }

    public async Task<PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.PermitChecklistItems.AsNoTracking()
            .Include(x => x.OperationalProject)
            .Include(x => x.DesignProject)
            .Include(x => x.Owner)
            .Include(x => x.WorkTask)!.ThenInclude(task => task!.AssigneeUser)
            .Include(x => x.WorkTask)!.ThenInclude(task => task!.ApproverUser)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        var label = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == PermitTypeCategory && m.Code == entity.PermitTypeCode)
            .Select(m => m.Name)
            .FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        return Map(entity,
            label is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [entity.PermitTypeCode] = label },
            now,
            now.AddDays(DueSoonDays),
            now.AddDays(ExpiringSoonDays));
    }

    public async Task<IReadOnlyList<PermitChecklistHistoryResponse>> HistoryAsync(
        int id, CancellationToken ct = default) => await db.PermitChecklistHistory.AsNoTracking()
        .Where(item => item.PermitChecklistItemId == id)
        .OrderByDescending(item => item.ChangedAt)
        .Select(item => new PermitChecklistHistoryResponse
        {
            Id = item.Id,
            FromStatus = item.FromStatus.HasValue ? item.FromStatus.Value.ToString() : null,
            ToStatus = item.ToStatus.ToString(),
            Action = item.Action,
            SnapshotJson = item.SnapshotJson,
            ChangedAt = item.ChangedAt,
            ChangedByUserId = item.ChangedByUserId,
            ChangedByName = item.ChangedByUser.FullName ?? item.ChangedByUser.Email ?? $"#{item.ChangedByUserId}",
        }).ToListAsync(ct);

    public async Task<PermitChecklistItemResponse> CreateAsync(
        CreatePermitChecklistItemRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var project = await db.OperationalProjects.AsNoTracking()
            .Where(x => x.Id == request.OperationalProjectId)
            .Select(x => new { x.Id, DesignIds = x.DesignProjects.Select(design => design.Id).ToList() })
            .SingleOrDefaultAsync(ct);
        if (project is null)
            throw new PermitChecklistOperationException($"Dự án vận hành #{request.OperationalProjectId} không tồn tại.");
        if (!request.DesignProjectId.HasValue && project.DesignIds.Count == 1)
            request.DesignProjectId = project.DesignIds[0];
        if (request.DesignProjectId.HasValue && !project.DesignIds.Contains(request.DesignProjectId.Value))
            throw new PermitChecklistOperationException("Dự án thiết kế không thuộc đúng Dự án vận hành.");
        if (request.DesignProjectId.HasValue && !await db.DesignProjects.AsNoTracking().AnyAsync(x => x.Id == request.DesignProjectId.Value, ct))
        {
            throw new PermitChecklistOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        var permitTypeCode = request.PermitTypeCode.Trim();
        if (!await db.MasterDataOptions.AsNoTracking().AnyAsync(
                x => x.Category == PermitTypeCategory && x.Code == permitTypeCode && x.IsActive, ct))
        {
            throw new PermitChecklistOperationException($"Loại giấy phép '{permitTypeCode}' không hợp lệ hoặc đã ngừng sử dụng.");
        }

        if (await db.PermitChecklistItems.AsNoTracking().AnyAsync(
                x => x.OperationalProjectId == request.OperationalProjectId && x.PermitTypeCode == permitTypeCode, ct))
        {
            throw new PermitChecklistDuplicateException("Dự án đã có hạng mục cho loại giấy phép này.");
        }

        var status = ParseStatus(request.Status);
        await ValidateOwnerAsync(request.OwnerUserId, ct);
        var now = DateTime.UtcNow;
        var entity = new PermitChecklistItem
        {
            OperationalProjectId = request.OperationalProjectId,
            DesignProjectId = request.DesignProjectId,
            PermitTypeCode = permitTypeCode,
            Status = status,
            IssuingAgency = TrimOrNull(request.IssuingAgency),
            OwnerUserId = request.OwnerUserId,
            TargetDeadline = request.TargetDeadline,
            SubmittedAt = request.SubmittedAt,
            IssuedAt = request.IssuedAt,
            ExpiresAt = request.ExpiresAt,
            Note = TrimOrNull(request.Note),
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        ValidateStatusData(entity);

        db.PermitChecklistItems.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Permit checklist item {Id} created by user {UserId}", entity.Id, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<PermitChecklistItemResponse?> UpdateAsync(int id, UpdatePermitChecklistItemRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.PermitChecklistItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        var previousStatus = entity.Status;
        if (entity.WorkTaskId.HasValue && (request.OwnerUserId.HasValue || request.ClearOwner ||
            request.TargetDeadline.HasValue || request.ClearTargetDeadline))
            throw new PermitChecklistOperationException(
                "Người thực hiện và lịch kế hoạch của giấy phép phải được cập nhật trên đầu việc Pháp lý liên kết.");

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            entity.Status = ParseStatus(request.Status);
            ValidateStatusTransition(previousStatus, entity.Status);
        }

        if (request.OwnerUserId.HasValue)
        {
            await ValidateOwnerAsync(request.OwnerUserId, ct);
            entity.OwnerUserId = request.OwnerUserId;
        }
        else if (request.ClearOwner)
        {
            entity.OwnerUserId = null;
        }

        ApplyStringPatch(request.IssuingAgency, request.ClearIssuingAgency,
            v => entity.IssuingAgency = v);
        ApplyDatePatch(request.TargetDeadline, request.ClearTargetDeadline,
            v => entity.TargetDeadline = v);
        ApplyDatePatch(request.SubmittedAt, request.ClearSubmittedAt,
            v => entity.SubmittedAt = v);
        ApplyDatePatch(request.IssuedAt, request.ClearIssuedAt,
            v => entity.IssuedAt = v);
        ApplyDatePatch(request.ExpiresAt, request.ClearExpiresAt,
            v => entity.ExpiresAt = v);
        ApplyStringPatch(request.Note, request.ClearNote, v => entity.Note = v);

        ValidateStatusData(entity);

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        db.PermitChecklistHistory.Add(new PermitChecklistHistory
        {
            PermitChecklistItemId = entity.Id,
            FromStatus = previousStatus,
            ToStatus = entity.Status,
            Action = previousStatus == entity.Status ? "Updated" : "StatusChanged",
            SnapshotJson = JsonSerializer.Serialize(request),
            ChangedAt = entity.UpdatedAt,
            ChangedByUserId = callerUserId,
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Permit checklist item {Id} updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<PermitChecklistItemResponse?> UploadDocumentAsync(
        int id,
        PermitDocumentKind kind,
        IFormFile? file,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.PermitChecklistItems
            .Include(item => item.OperationalProject)
            .Include(item => item.DesignProject)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        var uploaded = await documentStorage.StoreAsync(file, BusinessDocumentArea.Permits, ct);
        var previousPath = kind == PermitDocumentKind.SubmittedPackage
            ? entity.SubmittedFilePath
            : entity.IssuedFilePath;
        if (kind == PermitDocumentKind.SubmittedPackage)
        {
            entity.SubmittedFilePath = uploaded.Path;
        }
        else
        {
            entity.IssuedFilePath = uploaded.Path;
        }
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        db.PermitChecklistHistory.Add(new PermitChecklistHistory
        {
            PermitChecklistItemId = entity.Id,
            FromStatus = entity.Status,
            ToStatus = entity.Status,
            Action = kind == PermitDocumentKind.SubmittedPackage ? "SubmittedPackageUploaded" : "IssuedPermitUploaded",
            SnapshotJson = JsonSerializer.Serialize(new { uploaded.Path, uploaded.OriginalFileName }),
            ChangedAt = entity.UpdatedAt,
            ChangedByUserId = callerUserId,
        });
        try
        {
            if (entity.OperationalProjectId > 0)
            {
                var slot = kind == PermitDocumentKind.SubmittedPackage ? "submittedPackage" : "issuedPermit";
                if (!string.IsNullOrWhiteSpace(previousPath))
                {
                    await projectDocuments.StageExistingManagedFileDeleteAsync(
                        entity.OperationalProjectId, ProjectDocumentSourceModule.Legal,
                        nameof(PermitChecklistItem), slot, entity.Id, previousPath, callerUserId, ct);
                }
                await projectDocuments.StageExistingManagedFileAsync(
                    entity.OperationalProjectId, ProjectDocumentCategory.LegalPermits,
                    ProjectDocumentSourceModule.Legal, nameof(PermitChecklistItem), slot, entity.Id,
                    uploaded.Path, uploaded.OriginalFileName, entity.OperationalProject.CustomerId,
                    entity.DesignProject?.ContractId, callerUserId, ct);
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            documentStorage.Delete(uploaded.Path, BusinessDocumentArea.Permits);
            throw;
        }
        documentStorage.Delete(previousPath, BusinessDocumentArea.Permits);

        logger.LogInformation(
            "Permit checklist item {Id} {Kind} document uploaded by user {UserId}",
            id,
            kind,
            callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<PermitChecklistItemResponse?> DeleteAsync(int id, CancellationToken ct = default)
    {
        var response = await GetAsync(id, ct);
        if (response is null) return null;

        var entity = await db.PermitChecklistItems
            .Include(item => item.OperationalProject)
            .Include(item => item.DesignProject)
            .FirstAsync(x => x.Id == id, ct);
        var submittedFilePath = entity.SubmittedFilePath;
        var issuedFilePath = entity.IssuedFilePath;
        if (entity.OperationalProjectId > 0)
        {
            if (!string.IsNullOrWhiteSpace(submittedFilePath))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    entity.OperationalProjectId, ProjectDocumentSourceModule.Legal,
                    nameof(PermitChecklistItem), "submittedPackage", entity.Id, submittedFilePath,
                    entity.UpdatedByUserId, ct);
            if (!string.IsNullOrWhiteSpace(issuedFilePath))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    entity.OperationalProjectId, ProjectDocumentSourceModule.Legal,
                    nameof(PermitChecklistItem), "issuedPermit", entity.Id, issuedFilePath,
                    entity.UpdatedByUserId, ct);
        }
        db.PermitChecklistItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        documentStorage.Delete(submittedFilePath, BusinessDocumentArea.Permits);
        documentStorage.Delete(issuedFilePath, BusinessDocumentArea.Permits);
        logger.LogInformation("Permit checklist item {Id} deleted", id);
        return response;
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task ValidateOwnerAsync(int? ownerUserId, CancellationToken ct)
    {
        if (ownerUserId.HasValue && !await db.Users.AsNoTracking().AnyAsync(u => u.Id == ownerUserId.Value, ct))
        {
            throw new PermitChecklistOperationException($"Người phụ trách #{ownerUserId} không tồn tại.");
        }
    }

    private static PermitStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return PermitStatus.NotStarted;
        if (Enum.TryParse<PermitStatus>(status, true, out var parsed)) return parsed;
        throw new PermitChecklistOperationException($"Trạng thái '{status}' không hợp lệ.");
    }

    private static void ValidateStatusTransition(PermitStatus from, PermitStatus to)
    {
        if (from == to) return;
        var allowed = from switch
        {
            PermitStatus.NotStarted => to == PermitStatus.Preparing,
            PermitStatus.Preparing => to is PermitStatus.Submitted or PermitStatus.Rejected,
            PermitStatus.Submitted => to is PermitStatus.UnderReview or PermitStatus.NeedMoreDocs or PermitStatus.Issued or PermitStatus.Rejected,
            PermitStatus.UnderReview => to is PermitStatus.NeedMoreDocs or PermitStatus.Issued or PermitStatus.Rejected,
            PermitStatus.NeedMoreDocs => to is PermitStatus.Preparing or PermitStatus.Submitted,
            PermitStatus.Issued => to == PermitStatus.Expired,
            PermitStatus.Rejected => to == PermitStatus.Preparing,
            PermitStatus.Expired => to == PermitStatus.Preparing,
            _ => false,
        };
        if (!allowed)
            throw new PermitChecklistOperationException($"Không thể chuyển giấy phép từ {from} sang {to}.");
    }

    private static void ValidateStatusData(PermitChecklistItem entity)
    {
        if (entity.SubmittedAt.HasValue && entity.IssuedAt.HasValue && entity.IssuedAt < entity.SubmittedAt)
            throw new PermitChecklistOperationException("Ngày cấp giấy phép không được trước ngày nộp hồ sơ.");
        if (entity.IssuedAt.HasValue && entity.ExpiresAt.HasValue && entity.ExpiresAt < entity.IssuedAt)
            throw new PermitChecklistOperationException("Ngày hết hạn không được trước ngày cấp giấy phép.");
        if ((entity.Status is PermitStatus.Submitted or PermitStatus.UnderReview or PermitStatus.NeedMoreDocs) &&
            (!entity.SubmittedAt.HasValue || string.IsNullOrWhiteSpace(entity.SubmittedFilePath)))
            throw new PermitChecklistOperationException("Trạng thái đã nộp/thẩm định yêu cầu ngày nộp và hồ sơ đã nộp.");
        if (entity.Status == PermitStatus.Issued &&
            (!entity.IssuedAt.HasValue || string.IsNullOrWhiteSpace(entity.IssuedFilePath)))
            throw new PermitChecklistOperationException("Trạng thái Đã cấp yêu cầu ngày cấp và tệp giấy phép được cấp.");
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PermitChecklistRiskSummary> ComputeRiskAsync(
        DateTime now,
        DateTime dueSoonCutoff,
        DateTime expiringSoonCutoff,
        CancellationToken ct,
        IReadOnlySet<int>? accessibleOperationalProjectIds)
    {
        var openBase = db.PermitChecklistItems.AsNoTracking()
            .Where(x => x.Status != PermitStatus.Issued);
        if (accessibleOperationalProjectIds is not null)
            openBase = openBase.Where(x => accessibleOperationalProjectIds.Contains(x.OperationalProjectId));

        var overdue = await openBase
            .CountAsync(x => (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) != null &&
                (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) < now, ct);
        var dueSoon = await openBase
            .CountAsync(x => (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) != null
                          && (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) >= now
                          && (x.WorkTask != null ? x.WorkTask.PlannedEnd : x.TargetDeadline) <= dueSoonCutoff, ct);
        var totalOpen = await openBase.CountAsync(ct);
        var expiringBase = db.PermitChecklistItems.AsNoTracking()
            .Where(x => x.Status == PermitStatus.Issued);
        if (accessibleOperationalProjectIds is not null)
            expiringBase = expiringBase.Where(x => accessibleOperationalProjectIds.Contains(x.OperationalProjectId));
        var expiringSoon = await expiringBase
            .CountAsync(x => x.ExpiresAt != null
                          && x.ExpiresAt >= now
                          && x.ExpiresAt <= expiringSoonCutoff, ct);

        return new PermitChecklistRiskSummary
        {
            Overdue = overdue,
            DueSoon = dueSoon,
            ExpiringSoon = expiringSoon,
            TotalOpen = totalOpen,
        };
    }

    private static void ApplyStringPatch(string? value, bool clear, Action<string?> setter)
    {
        if (clear) { setter(null); return; }
        if (value is null) return;
        setter(value.Trim().Length == 0 ? null : value.Trim());
    }

    private static void ApplyDatePatch(DateTime? value, bool clear, Action<DateTime?> setter)
    {
        if (clear) { setter(null); return; }
        if (value is null) return;
        setter(value);
    }

    private static List<TEnum> ParseEnumCsv<TEnum>(string csv) where TEnum : struct, Enum
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<TEnum>(s, true, out var v) ? (TEnum?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

    private static PermitChecklistItemResponse Map(PermitChecklistItem entity,
        IReadOnlyDictionary<string, string> labelByCode,
        DateTime now, DateTime dueSoonCutoff, DateTime expiringSoonCutoff)
    {
        var isOpen = entity.Status != PermitStatus.Issued;
        var deadline = entity.WorkTask?.PlannedEnd ?? entity.TargetDeadline;
        var isOverdue = isOpen && deadline is DateTime td && td < now;
        var isDueSoon = isOpen && deadline is DateTime td2 && td2 >= now && td2 <= dueSoonCutoff;
        var isExpiringSoon = entity.Status == PermitStatus.Issued
            && entity.ExpiresAt is DateTime ex && ex >= now && ex <= expiringSoonCutoff;

        return new PermitChecklistItemResponse
        {
            Id = entity.Id,
            OperationalProjectId = entity.OperationalProjectId,
            OperationalProjectCode = entity.OperationalProject?.Code,
            OperationalProjectName = entity.OperationalProject?.Name,
            DesignProjectId = entity.DesignProjectId,
            DesignProjectCode = entity.DesignProject?.ProjectCode,
            DesignProjectName = entity.DesignProject?.Name,
            PermitTypeCode = entity.PermitTypeCode,
            IsRequired = entity.IsRequired,
            PermitTypeLabel = labelByCode.TryGetValue(entity.PermitTypeCode, out var label) ? label : null,
            IssuingAgency = entity.IssuingAgency,
            OwnerUserId = entity.WorkTask?.AssigneeUserId ?? entity.OwnerUserId,
            OwnerName = entity.WorkTask?.AssigneeUser?.FullName ?? entity.Owner?.FullName,
            WorkTaskId = entity.WorkTaskId,
            WorkPackageId = entity.WorkTask?.WorkPackageId,
            ApproverUserId = entity.WorkTask?.ApproverUserId,
            ApproverName = entity.WorkTask?.ApproverUser?.FullName,
            ProgressPercent = entity.WorkTask?.ProgressPercent ?? 0,
            WorkTaskStatus = entity.WorkTask?.Status.ToString(),
            WorkTaskRowVersion = entity.WorkTask is null ? null : CrmConcurrency.Encode(entity.WorkTask.RowVersion),
            TargetDeadline = entity.WorkTask?.PlannedEnd ?? entity.TargetDeadline,
            SubmittedAt = entity.SubmittedAt,
            IssuedAt = entity.IssuedAt,
            ExpiresAt = entity.ExpiresAt,
            SubmittedFilePath = entity.SubmittedFilePath,
            IssuedFilePath = entity.IssuedFilePath,
            Status = entity.Status.ToString(),
            Note = entity.Note,
            IsOverdue = isOverdue,
            IsDueSoon = isDueSoon,
            IsExpiringSoon = isExpiringSoon,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
