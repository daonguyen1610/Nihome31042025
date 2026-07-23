using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Implementation of the M4 as-built dossier workflow (NIH-145).
/// State machine, category-aware completeness roll-up, and bulk delete.
/// </summary>
public class AsBuiltDocumentService(
    AppDbContext db,
    ILogger<AsBuiltDocumentService> logger) : IAsBuiltDocumentService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;

    // --------------------------------------------------------------------
    //  Read paths
    // --------------------------------------------------------------------

    public async Task<AsBuiltDocumentListResponse> ListAsync(AsBuiltDocumentListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.AsBuiltDocuments
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(a => a.DesignProjectId == p.DesignProjectId.Value);

        if (!string.IsNullOrWhiteSpace(p.Category))
        {
            var cats = p.Category.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<AsBuiltCategory>(s, true, out var v) ? (AsBuiltCategory?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (cats.Count > 0) q = q.Where(a => cats.Contains(a.Category));
        }

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<AsBuiltStatus>(s, true, out var v) ? (AsBuiltStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(a => statuses.Contains(a.Status));
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(a => EF.Functions.Like(a.Title, $"%{term}%")
                          || EF.Functions.Like(a.DocumentCode, $"%{term}%"));
        }

        if (p.OpenOnly)
        {
            q = q.Where(a => a.Status == AsBuiltStatus.Draft || a.Status == AsBuiltStatus.Submitted);
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderBy(a => a.Category)
            .ThenBy(a => a.DocumentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Roll-ups on the project-scoped set (ignoring paging/other filters
        // so the header pills always reflect the whole project).
        var scope = db.AsBuiltDocuments.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(a => a.DesignProjectId == p.DesignProjectId.Value);

        var statusCounts = await scope
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);
        var categoryCounts = await scope
            .GroupBy(a => a.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Category.ToString(), x => x.Count, ct);

        // Completeness only makes sense when we're looking at one project.
        var completedRequired = 0;
        if (p.DesignProjectId.HasValue)
        {
            var approvedCats = await scope
                .Where(a => a.Status == AsBuiltStatus.Approved || a.Status == AsBuiltStatus.Archived)
                .Select(a => a.Category)
                .Distinct()
                .ToListAsync(ct);
            completedRequired = AsBuiltCategoryExtensions.Required.Count(approvedCats.Contains);
        }

        return new AsBuiltDocumentListResponse
        {
            Items = rows.Select(Map).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
            StatusCounts = statusCounts,
            CategoryCounts = categoryCounts,
            CompletedRequiredCategories = completedRequired,
            TotalRequiredCategories = AsBuiltCategoryExtensions.Required.Length,
        };
    }

    public async Task<AsBuiltDocumentResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return entity is null ? null : Map(entity);
    }

    // --------------------------------------------------------------------
    //  Write paths
    // --------------------------------------------------------------------

    public async Task<AsBuiltDocumentResponse> CreateAsync(CreateAsBuiltDocumentRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new AsBuiltDocumentOperationException("Tiêu đề tài liệu là bắt buộc.");
        }
        if (!Enum.TryParse<AsBuiltCategory>(request.Category, true, out var category))
        {
            throw new AsBuiltDocumentOperationException($"Danh mục '{request.Category}' không hợp lệ.");
        }

        var projectExists = await db.DesignProjects.AnyAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (!projectExists)
        {
            throw new AsBuiltDocumentOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        var code = await AllocateCodeAsync(request.DesignProjectId, ct);

        var entity = new AsBuiltDocument
        {
            DesignProjectId = request.DesignProjectId,
            DocumentCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            Category = category,
            FileUrl = TrimOrNull(request.FileUrl),
            Note = TrimOrNull(request.Note),
            Status = AsBuiltStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.AsBuiltDocuments.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AsBuiltDocument {Id} ({Code}) created on project {ProjectId}",
            entity.Id, entity.DocumentCode, entity.DesignProjectId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<AsBuiltDocumentResponse?> UpdateAsync(int id, UpdateAsBuiltDocumentRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        if (entity.Status is AsBuiltStatus.Approved or AsBuiltStatus.Archived or AsBuiltStatus.Cancelled)
        {
            throw new AsBuiltDocumentOperationException(
                $"Không thể chỉnh sửa tài liệu ở trạng thái '{entity.Status}'.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new AsBuiltDocumentOperationException("Tiêu đề tài liệu là bắt buộc.");
        }
        if (!Enum.TryParse<AsBuiltCategory>(request.Category, true, out var category))
        {
            throw new AsBuiltDocumentOperationException($"Danh mục '{request.Category}' không hợp lệ.");
        }

        entity.Title = title;
        entity.Category = category;
        entity.Description = TrimOrNull(request.Description);
        entity.FileUrl = TrimOrNull(request.FileUrl);
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AsBuiltDocumentResponse?> TransitionAsync(int id, TransitionAsBuiltStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        if (!Enum.TryParse<AsBuiltStatus>(request.Status, true, out var next))
        {
            throw new AsBuiltDocumentOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }
        if (next == AsBuiltStatus.Approved)
        {
            throw new AsBuiltDocumentOperationException(
                "Dùng POST /approve để duyệt tài liệu — thao tác cần quyền construction.asbuilt.approve.");
        }

        EnsureTransitionAllowed(entity.Status, next);
        ApplyTransition(entity, next, callerUserId, request.Note);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AsBuiltDocument {Id} transitioned -> {To}", id, next);
        return await GetAsync(id, ct);
    }

    public async Task<AsBuiltDocumentResponse?> ApproveAsync(int id, TransitionAsBuiltStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, AsBuiltStatus.Approved);
        ApplyTransition(entity, AsBuiltStatus.Approved, callerUserId, request.Note);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AsBuiltDocument {Id} approved by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status != AsBuiltStatus.Draft && entity.Status != AsBuiltStatus.Cancelled)
        {
            throw new AsBuiltDocumentOperationException(
                "Chỉ có thể xoá tài liệu ở trạng thái Nháp hoặc Đã huỷ.");
        }
        db.AsBuiltDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AsBuiltDocumentBulkDeleteResponse> BulkDeleteAsync(BulkDeleteAsBuiltDocumentsRequest request, CancellationToken ct = default)
    {
        var ids = (request.Ids ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new AsBuiltDocumentOperationException("Danh sách tài liệu cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new AsBuiltDocumentOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} tài liệu mỗi lần.");
        }

        var rows = await db.AsBuiltDocuments.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var response = new AsBuiltDocumentBulkDeleteResponse();
        foreach (var row in rows)
        {
            if (row.Status == AsBuiltStatus.Draft || row.Status == AsBuiltStatus.Cancelled)
            {
                response.DeletedIds.Add(row.Id);
                db.AsBuiltDocuments.Remove(row);
            }
            else
            {
                response.SkippedIds.Add(row.Id);
            }
        }
        response.SkippedIds.AddRange(ids.Except(rows.Select(r => r.Id)));
        if (response.DeletedIds.Count > 0) await db.SaveChangesAsync(ct);
        return response;
    }

    // --------------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------------

    private async Task<string> AllocateCodeAsync(int projectId, CancellationToken ct)
    {
        var codes = await db.AsBuiltDocuments
            .Where(a => a.DesignProjectId == projectId)
            .Select(a => a.DocumentCode)
            .ToListAsync(ct);
        var maxSeq = codes
            .Select(c =>
            {
                var idx = c.LastIndexOf('-');
                if (idx < 0 || idx == c.Length - 1) return 0;
                return int.TryParse(c[(idx + 1)..], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"AB-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// State machine allowances:
    ///   Draft      → Submitted, Cancelled
    ///   Submitted  → Approved, Draft (revise), Cancelled
    ///   Approved   → Archived, Draft (revise), Cancelled
    ///   Archived   → (terminal)
    ///   Cancelled  → Draft (restore)
    /// </summary>
    private static void EnsureTransitionAllowed(AsBuiltStatus from, AsBuiltStatus to)
    {
        if (from == to)
        {
            throw new AsBuiltDocumentOperationException($"Trạng thái đã là '{from}'.");
        }
        var allowed = from switch
        {
            AsBuiltStatus.Draft => to is AsBuiltStatus.Submitted or AsBuiltStatus.Cancelled,
            AsBuiltStatus.Submitted => to is AsBuiltStatus.Approved or AsBuiltStatus.Draft or AsBuiltStatus.Cancelled,
            AsBuiltStatus.Approved => to is AsBuiltStatus.Archived or AsBuiltStatus.Draft or AsBuiltStatus.Cancelled,
            AsBuiltStatus.Archived => false,
            AsBuiltStatus.Cancelled => to is AsBuiltStatus.Draft,
            _ => false,
        };
        if (!allowed)
        {
            throw new AsBuiltDocumentOperationException($"Không thể chuyển '{from}' sang '{to}'.");
        }
    }

    private static void ApplyTransition(AsBuiltDocument entity, AsBuiltStatus next, int userId, string? note)
    {
        var now = DateTime.UtcNow;
        switch (next)
        {
            case AsBuiltStatus.Submitted:
                entity.SubmittedAt = now;
                entity.SubmittedByUserId = userId;
                break;
            case AsBuiltStatus.Approved:
                entity.ApprovedAt = now;
                entity.ApprovedByUserId = userId;
                break;
            case AsBuiltStatus.Archived:
                entity.ArchivedAt = now;
                break;
            case AsBuiltStatus.Draft when entity.Status == AsBuiltStatus.Approved:
                // Revised back from Approved — clear the approval signature so
                // the completeness roll-up drops this doc until it's re-approved.
                entity.ApprovedAt = null;
                entity.ApprovedByUserId = null;
                break;
        }
        if (!string.IsNullOrWhiteSpace(note))
        {
            entity.Note = note.Trim();
        }
        entity.Status = next;
        entity.UpdatedByUserId = userId;
        entity.UpdatedAt = now;
    }

    private static AsBuiltDocumentResponse Map(AsBuiltDocument e) => new()
    {
        Id = e.Id,
        DesignProjectId = e.DesignProjectId,
        DesignProjectName = e.DesignProject?.Name ?? string.Empty,
        DocumentCode = e.DocumentCode,
        Title = e.Title,
        Category = e.Category.ToString(),
        Description = e.Description,
        FileUrl = e.FileUrl,
        Status = e.Status.ToString(),
        Note = e.Note,
        SubmittedAt = e.SubmittedAt,
        SubmittedByUserId = e.SubmittedByUserId,
        SubmittedByName = e.SubmittedBy?.FullName,
        ApprovedAt = e.ApprovedAt,
        ApprovedByUserId = e.ApprovedByUserId,
        ApprovedByName = e.ApprovedBy?.FullName,
        ArchivedAt = e.ArchivedAt,
        CreatedAt = e.CreatedAt,
        CreatedByUserId = e.CreatedByUserId,
        UpdatedAt = e.UpdatedAt,
        UpdatedByUserId = e.UpdatedByUserId,
    };

    private static string? TrimOrNull(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
