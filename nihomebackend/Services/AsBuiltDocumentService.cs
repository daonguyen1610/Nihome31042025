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
    ILogger<AsBuiltDocumentService> logger,
    AsBuiltDocumentCategoryService categoryService,
    IBusinessDocumentStorageService? documentStorage = null,
    IProjectDocumentStagingService? projectDocuments = null) : IAsBuiltDocumentService
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
        var q = BuildFilteredQuery(p);

        var total = await q.CountAsync(ct);

        var rows = await ApplySort(q, p)
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
            .Include(a => a.Category)
            .GroupBy(a => a.Category.Code)
            .Select(g => new { CategoryCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryCode, x => x.Count, ct);

        // Completeness only makes sense when we're looking at one project.
        var completedRequired = 0;
        var requiredCategoryIds = await categoryService.GetRequiredCategoryIdsAsync();
        if (p.DesignProjectId.HasValue)
        {
            var approvedCatIds = await scope
                .Where(a => a.Status == AsBuiltStatus.Approved || a.Status == AsBuiltStatus.Archived)
                .Select(a => a.CategoryId)
                .Distinct()
                .ToListAsync(ct);
            completedRequired = requiredCategoryIds.Count(approvedCatIds.Contains);
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
            TotalRequiredCategories = requiredCategoryIds.Length,
        };
    }

    public async Task<List<AsBuiltDocumentResponse>> ExportAsync(
        AsBuiltDocumentListParams p,
        CancellationToken ct = default)
    {
        var rows = await ApplySort(BuildFilteredQuery(p), p).ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<AsBuiltDocumentResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.Category)
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

        var categoryId = await ResolveCategoryIdAsync(request.Category);

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new AsBuiltDocumentOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }
        await EnsureUniqueTitleAsync(request.DesignProjectId, title, null, ct);

        var code = await AllocateCodeAsync(request.DesignProjectId, ct);

        var entity = new AsBuiltDocument
        {
            DesignProjectId = request.DesignProjectId,
            DocumentCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            CategoryId = categoryId,
            FileUrl = TrimOrNull(request.FileUrl),
            Note = TrimOrNull(request.Note),
            Status = AsBuiltStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            db.AsBuiltDocuments.Add(entity);
            await db.SaveChangesAsync(ct);
            await StageFileDiffAsync(project, entity.Id, null, entity.FileUrl, callerUserId, ct);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            else await RollbackCreateAsync(entity, CancellationToken.None);
            throw;
        }
        logger.LogInformation("AsBuiltDocument {Id} ({Code}) created on project {ProjectId}",
            entity.Id, entity.DocumentCode, entity.DesignProjectId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<AsBuiltDocumentResponse?> UpdateAsync(int id, UpdateAsBuiltDocumentRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.AsBuiltDocuments.Include(a => a.DesignProject).FirstOrDefaultAsync(a => a.Id == id, ct);
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

        var categoryId = await ResolveCategoryIdAsync(request.Category, entity.CategoryId);

        await EnsureUniqueTitleAsync(entity.DesignProjectId, title, entity.Id, ct);

        var previousFileUrl = entity.FileUrl;
        entity.Title = title;
        entity.CategoryId = categoryId;
        entity.Description = TrimOrNull(request.Description);
        entity.FileUrl = TrimOrNull(request.FileUrl);
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await StageFileDiffAsync(entity.DesignProject, entity.Id, previousFileUrl, entity.FileUrl,
            callerUserId, ct);
        await db.SaveChangesAsync(ct);
        if (!string.Equals(previousFileUrl, entity.FileUrl, StringComparison.Ordinal))
            documentStorage?.Delete(previousFileUrl, BusinessDocumentArea.AsBuilt);
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
        var entity = await db.AsBuiltDocuments.Include(a => a.DesignProject)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        var fileUrl = entity.FileUrl;
        await StageFileDiffAsync(entity.DesignProject, entity.Id, fileUrl, null,
            entity.UpdatedByUserId, ct);
        db.AsBuiltDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
        documentStorage?.Delete(fileUrl, BusinessDocumentArea.AsBuilt);
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

        var rows = await db.AsBuiltDocuments.Include(a => a.DesignProject)
            .Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var response = new AsBuiltDocumentBulkDeleteResponse();
        foreach (var row in rows)
        {
            response.DeletedIds.Add(row.Id);
            await StageFileDiffAsync(row.DesignProject, row.Id, row.FileUrl, null,
                row.UpdatedByUserId, ct);
            db.AsBuiltDocuments.Remove(row);
        }
        response.SkippedIds.AddRange(ids.Except(rows.Select(r => r.Id)));
        if (response.DeletedIds.Count > 0) await db.SaveChangesAsync(ct);
        foreach (var row in rows)
            documentStorage?.Delete(row.FileUrl, BusinessDocumentArea.AsBuilt);
        return response;
    }

    // --------------------------------------------------------------------
    //  Helpers
    // --------------------------------------------------------------------

    private async Task StageFileDiffAsync(DesignProject project, int recordId,
        string? previous, string? current, int? userId, CancellationToken ct)
    {
        if (project.OperationalProjectId is not int projectId || projectDocuments is null) return;
        var oldPath = ManagedPath(previous);
        var newPath = ManagedPath(current);
        if (oldPath is not null && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
            await projectDocuments.StageExistingManagedFileDeleteAsync(projectId,
                ProjectDocumentSourceModule.Acceptance, nameof(AsBuiltDocument), "file",
                recordId, oldPath, userId, ct);
        if (newPath is not null && !string.Equals(oldPath, newPath, StringComparison.Ordinal))
            await projectDocuments.StageExistingManagedFileAsync(projectId,
                ProjectDocumentCategory.ConstructionAcceptance, ProjectDocumentSourceModule.Acceptance,
                nameof(AsBuiltDocument), "file", recordId, newPath, Path.GetFileName(newPath),
                project.CustomerId, project.ContractId, userId, ct);
    }

    private static string? ManagedPath(string? path)
    {
        var value = path?.Trim();
        return value?.StartsWith("/files/business-documents/as-built/", StringComparison.OrdinalIgnoreCase) == true
            ? value
            : null;
    }

    private async Task RollbackCreateAsync(AsBuiltDocument entity, CancellationToken ct)
    {
        db.ProjectDocuments.RemoveRange(db.ChangeTracker.Entries<ProjectDocument>()
            .Where(entry => entry.Entity.SourceEntityType == nameof(AsBuiltDocument)
                && entry.Entity.SourceRecordId == entity.Id)
            .Select(entry => entry.Entity));
        db.AsBuiltDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<AsBuiltDocument> BuildFilteredQuery(AsBuiltDocumentListParams p)
    {
        var query = db.AsBuiltDocuments
            .AsNoTracking()
            .Include(a => a.DesignProject)
            .Include(a => a.Category)
            .Include(a => a.SubmittedBy)
            .Include(a => a.ApprovedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue)
        {
            query = query.Where(a => a.DesignProjectId == p.DesignProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(p.Category))
        {
            var categoryCodes = p.Category
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (categoryCodes.Count > 0) query = query.Where(a => categoryCodes.Contains(a.Category.Code));
        }

        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<AsBuiltStatus>(value, true, out var parsed)
                    ? (AsBuiltStatus?)parsed
                    : null)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
            if (statuses.Count > 0) query = query.Where(a => statuses.Contains(a.Status));
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            query = query.Where(a => EF.Functions.Like(a.Title, $"%{term}%")
                                  || EF.Functions.Like(a.DocumentCode, $"%{term}%"));
        }

        if (p.OpenOnly)
        {
            query = query.Where(a => a.Status == AsBuiltStatus.Draft || a.Status == AsBuiltStatus.Submitted);
        }

        return query;
    }

    private static IOrderedQueryable<AsBuiltDocument> ApplySort(
        IQueryable<AsBuiltDocument> query,
        AsBuiltDocumentListParams p)
    {
        var descending = string.Equals(p.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (p.SortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("code", true) => query.OrderByDescending(a => a.DocumentCode).ThenByDescending(a => a.Id),
            ("code", false) => query.OrderBy(a => a.DocumentCode).ThenBy(a => a.Id),
            ("title", true) => query.OrderByDescending(a => a.Title).ThenByDescending(a => a.Id),
            ("title", false) => query.OrderBy(a => a.Title).ThenBy(a => a.Id),
            ("project", true) => query.OrderByDescending(a => a.DesignProject.Name).ThenByDescending(a => a.Id),
            ("project", false) => query.OrderBy(a => a.DesignProject.Name).ThenBy(a => a.Id),
            ("status", true) => query.OrderByDescending(a => a.Status).ThenByDescending(a => a.Id),
            ("status", false) => query.OrderBy(a => a.Status).ThenBy(a => a.Id),
            ("updatedat", true) => query.OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id),
            ("updatedat", false) => query.OrderBy(a => a.UpdatedAt).ThenBy(a => a.Id),
            (_, true) => query.OrderByDescending(a => a.Category).ThenByDescending(a => a.DocumentCode),
            _ => query.OrderBy(a => a.Category).ThenBy(a => a.DocumentCode),
        };
    }

    private async Task EnsureUniqueTitleAsync(
        int projectId,
        string title,
        int? excludedId,
        CancellationToken ct)
    {
        var normalizedTitle = title.ToLower();
        var duplicateExists = await db.AsBuiltDocuments.AnyAsync(
            document => document.DesignProjectId == projectId
                && document.Title.ToLower() == normalizedTitle
                && (!excludedId.HasValue || document.Id != excludedId.Value),
            ct);
        if (duplicateExists)
        {
            throw new AsBuiltDocumentOperationException(
                $"Tiêu đề tài liệu '{title}' đã tồn tại trong dự án.");
        }
    }

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
        CategoryId = e.CategoryId,
        Category = e.Category?.Code ?? string.Empty,
        CategoryName = e.Category?.Name ?? string.Empty,
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

    private async Task<int> ResolveCategoryIdAsync(string? categoryCode, int? allowedInactiveCategoryId = null)
    {
        try
        {
            return await categoryService.ResolveCategoryIdAsync(null, categoryCode, allowedInactiveCategoryId);
        }
        catch (InvalidOperationException exception)
        {
            throw new AsBuiltDocumentOperationException(exception.Message);
        }
    }
}
