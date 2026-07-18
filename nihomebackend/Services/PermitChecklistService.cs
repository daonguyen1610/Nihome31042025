using Microsoft.EntityFrameworkCore;
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
    ILogger<PermitChecklistService> logger) : IPermitChecklistService
{
    private const int MaxPageSize = 200;
    private const string PermitTypeCategory = "permit_type";
    private const int DueSoonDays = 7;
    private const int ExpiringSoonDays = 30;

    public async Task EnsureForProjectAsync(int designProjectId, int? callerUserId, CancellationToken ct = default)
    {
        var projectExists = await db.DesignProjects.AsNoTracking()
            .AnyAsync(dp => dp.Id == designProjectId, ct);
        if (!projectExists)
        {
            throw new PermitChecklistOperationException($"Dự án #{designProjectId} không tồn tại.");
        }

        var templateCodes = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == PermitTypeCategory && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .Select(m => m.Code)
            .ToListAsync(ct);

        if (templateCodes.Count == 0) return;

        var existingCodes = await db.PermitChecklistItems
            .Where(p => p.DesignProjectId == designProjectId)
            .Select(p => p.PermitTypeCode)
            .ToListAsync(ct);
        var missing = templateCodes.Except(existingCodes, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var code in missing)
        {
            db.PermitChecklistItems.Add(new PermitChecklistItem
            {
                DesignProjectId = designProjectId,
                PermitTypeCode = code,
                Status = PermitStatus.NotStarted,
                CreatedByUserId = callerUserId,
                UpdatedByUserId = callerUserId,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded {Count} permit checklist items for design project {ProjectId}",
            missing.Count, designProjectId);
    }

    public async Task<PermitChecklistListResponse> ListAsync(PermitChecklistListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.PermitChecklistItems
            .AsNoTracking()
            .Include(x => x.DesignProject)
            .Include(x => x.Owner)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(x => x.DesignProjectId == p.DesignProjectId.Value);
        if (p.OwnerUserId.HasValue) q = q.Where(x => x.OwnerUserId == p.OwnerUserId.Value);

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
            q = q.Where(x => x.TargetDeadline != null
                          && x.TargetDeadline < now
                          && x.Status != PermitStatus.Issued);
        }
        if (p.DueSoon == true)
        {
            q = q.Where(x => x.TargetDeadline != null
                          && x.TargetDeadline >= now
                          && x.TargetDeadline <= dueSoonCutoff
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
                EF.Functions.Like(x.DesignProject.Name, $"%{term}%") ||
                EF.Functions.Like(x.DesignProject.ProjectCode, $"%{term}%") ||
                (x.IssuingAgency != null && EF.Functions.Like(x.IssuingAgency, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);

        // Overdue items and near-deadline items surface first so the risk
        // register naturally sorts to the top of every list.
        var rows = await q
            .OrderBy(x => x.Status == PermitStatus.Issued ? 1 : 0)
            .ThenBy(x => x.TargetDeadline ?? DateTime.MaxValue)
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
            Risk = await ComputeRiskAsync(now, dueSoonCutoff, expiringSoonCutoff, ct),
        };
        return response;
    }

    public async Task<PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.PermitChecklistItems.AsNoTracking()
            .Include(x => x.DesignProject)
            .Include(x => x.Owner)
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

    public async Task<PermitChecklistItemResponse?> UpdateAsync(int id, UpdatePermitChecklistItemRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.PermitChecklistItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PermitStatus>(request.Status, true, out var newStatus))
            {
                throw new PermitChecklistOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
            }
            entity.Status = newStatus;
        }

        if (request.OwnerUserId.HasValue)
        {
            if (!await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
            {
                throw new PermitChecklistOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
            }
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

        // Business rule: moving to Issued requires an IssuedAt date so the
        // "expiring soon" cron has something to check against.
        if (entity.Status == PermitStatus.Issued && entity.IssuedAt is null)
        {
            entity.IssuedAt = DateTime.UtcNow;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Permit checklist item {Id} updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<PermitChecklistRiskSummary> ComputeRiskAsync(
        DateTime now, DateTime dueSoonCutoff, DateTime expiringSoonCutoff, CancellationToken ct)
    {
        var openBase = db.PermitChecklistItems.AsNoTracking()
            .Where(x => x.Status != PermitStatus.Issued);

        var overdue = await openBase
            .CountAsync(x => x.TargetDeadline != null && x.TargetDeadline < now, ct);
        var dueSoon = await openBase
            .CountAsync(x => x.TargetDeadline != null
                          && x.TargetDeadline >= now
                          && x.TargetDeadline <= dueSoonCutoff, ct);
        var totalOpen = await openBase.CountAsync(ct);
        var expiringSoon = await db.PermitChecklistItems.AsNoTracking()
            .CountAsync(x => x.Status == PermitStatus.Issued
                          && x.ExpiresAt != null
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
        var isOverdue = isOpen && entity.TargetDeadline is DateTime td && td < now;
        var isDueSoon = isOpen && entity.TargetDeadline is DateTime td2 && td2 >= now && td2 <= dueSoonCutoff;
        var isExpiringSoon = entity.Status == PermitStatus.Issued
            && entity.ExpiresAt is DateTime ex && ex >= now && ex <= expiringSoonCutoff;

        return new PermitChecklistItemResponse
        {
            Id = entity.Id,
            DesignProjectId = entity.DesignProjectId,
            DesignProjectCode = entity.DesignProject?.ProjectCode,
            DesignProjectName = entity.DesignProject?.Name,
            PermitTypeCode = entity.PermitTypeCode,
            PermitTypeLabel = labelByCode.TryGetValue(entity.PermitTypeCode, out var label) ? label : null,
            IssuingAgency = entity.IssuingAgency,
            OwnerUserId = entity.OwnerUserId,
            OwnerName = entity.Owner?.FullName,
            TargetDeadline = entity.TargetDeadline,
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
