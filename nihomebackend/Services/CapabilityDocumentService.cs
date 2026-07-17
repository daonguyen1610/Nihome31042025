using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Capability-document service — see <see cref="ICapabilityDocumentService"/>
/// for surface, <see cref="CapabilityDocument"/> for the entity summary.
///
/// The service assumes the caller has already stored the uploaded file
/// under <c>wwwroot/files/capability/</c> and only receives the resulting
/// host-relative path; it owns metadata persistence, tag validation
/// against the <c>capability_document_tag</c> master-data category, version
/// snapshotting on file replace, and physical-file cleanup on delete /
/// replace. Deletion of a document that is still referenced by an open
/// tender is enforced by a callback surface once the tender module ships
/// (NIH-97); today the service only guards on structural rules.
/// </summary>
public class CapabilityDocumentService(
    AppDbContext db,
    ILogger<CapabilityDocumentService> logger,
    IWebHostEnvironment? env = null) : ICapabilityDocumentService
{
    public const string TagMasterDataCategory = "capability_document_tag";
    public const string ManagedFilePrefix = "/files/capability/";

    private const int MaxPageSize = 100;
    private const int WarningWindowDays = 60;
    private const int CriticalWindowDays = 30;

    private readonly string _webRoot = ResolveWebRoot(env);

    private static string ResolveWebRoot(IWebHostEnvironment? env)
    {
        if (!string.IsNullOrEmpty(env?.ContentRootPath))
        {
            return Path.Combine(env.ContentRootPath, "wwwroot");
        }
        if (!string.IsNullOrEmpty(env?.WebRootPath))
        {
            return env.WebRootPath;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    // ------------------------------ List / Get ------------------------------

    public async Task<CapabilityDocumentListResponse> ListAsync(
        string? tagCode = null,
        int? issuedYear = null,
        string? search = null,
        string? expiryState = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var q = db.CapabilityDocuments
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(tagCode))
        {
            var normalized = tagCode.Trim();
            q = q.Where(d => d.TagCode == normalized);
        }
        if (issuedYear.HasValue)
        {
            var year = issuedYear.Value;
            q = q.Where(d => d.IssuedDate != null && d.IssuedDate.Value.Year == year);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(d => EF.Functions.Like(d.Name, $"%{term}%")
                          || EF.Functions.Like(d.OriginalFileName, $"%{term}%"));
        }

        // Expiry state is computed in-memory (bucket relative to "now"),
        // so if the caller asks to filter by state we materialise the
        // matching set first, then paginate in-memory. Capability-doc
        // libraries are typically small (< 1k rows) so this is cheap.
        var now = DateTime.UtcNow;
        var wantedExpiry = string.IsNullOrWhiteSpace(expiryState)
            ? null
            : expiryState.Trim().ToLowerInvariant();

        List<CapabilityDocumentResponse> items;
        int total;

        if (wantedExpiry is null)
        {
            total = await q.CountAsync(ct);
            var rows = await q
                .OrderByDescending(d => d.UpdatedAt)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    Doc = d,
                    UploadedByName = d.UploadedBy != null ? d.UploadedBy.FullName : null,
                    PrevCount = db.CapabilityDocumentVersions.Count(v => v.CapabilityDocumentId == d.Id),
                })
                .ToListAsync(ct);
            var tagLabels = await LoadTagLabelsAsync(rows.Select(r => r.Doc.TagCode), ct);
            items = rows.Select(r => Map(r.Doc, r.UploadedByName, r.PrevCount, tagLabels, now)).ToList();
        }
        else
        {
            var rows = await q
                .OrderByDescending(d => d.UpdatedAt)
                .ThenByDescending(d => d.Id)
                .Select(d => new
                {
                    Doc = d,
                    UploadedByName = d.UploadedBy != null ? d.UploadedBy.FullName : null,
                    PrevCount = db.CapabilityDocumentVersions.Count(v => v.CapabilityDocumentId == d.Id),
                })
                .ToListAsync(ct);
            var tagLabels = await LoadTagLabelsAsync(rows.Select(r => r.Doc.TagCode), ct);
            var all = rows.Select(r => Map(r.Doc, r.UploadedByName, r.PrevCount, tagLabels, now))
                .Where(i => i.ExpiryState == wantedExpiry)
                .ToList();
            total = all.Count;
            items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        return new CapabilityDocumentListResponse
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<CapabilityDocumentDetailResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.CapabilityDocuments
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Include(d => d.Versions).ThenInclude(v => v.UploadedBy)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var tagLabels = await LoadTagLabelsAsync(new[] { entity.TagCode }, ct);
        var now = DateTime.UtcNow;
        var mapped = Map(entity, entity.UploadedBy?.FullName, entity.Versions.Count, tagLabels, now);

        return new CapabilityDocumentDetailResponse
        {
            Id = mapped.Id,
            Name = mapped.Name,
            TagCode = mapped.TagCode,
            TagLabel = mapped.TagLabel,
            IssuedDate = mapped.IssuedDate,
            ExpiryDate = mapped.ExpiryDate,
            Description = mapped.Description,
            FilePath = mapped.FilePath,
            OriginalFileName = mapped.OriginalFileName,
            FileSize = mapped.FileSize,
            ContentType = mapped.ContentType,
            CurrentVersion = mapped.CurrentVersion,
            ExpiryState = mapped.ExpiryState,
            UploadedByUserId = mapped.UploadedByUserId,
            UploadedByName = mapped.UploadedByName,
            CreatedAt = mapped.CreatedAt,
            UpdatedAt = mapped.UpdatedAt,
            PreviousVersionCount = mapped.PreviousVersionCount,
            Versions = entity.Versions
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new CapabilityDocumentVersionResponse
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    FilePath = v.FilePath,
                    OriginalFileName = v.OriginalFileName,
                    FileSize = v.FileSize,
                    ContentType = v.ContentType,
                    UploadedByUserId = v.UploadedByUserId,
                    UploadedByName = v.UploadedBy?.FullName,
                    CreatedAt = v.CreatedAt,
                }).ToList(),
        };
    }

    public async Task<List<CapabilityDocumentResponse>> GetManyAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new List<CapabilityDocumentResponse>();
        var idSet = ids.Distinct().ToList();
        var docs = await db.CapabilityDocuments
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Where(d => idSet.Contains(d.Id))
            .ToListAsync(ct);

        var tagLabels = await LoadTagLabelsAsync(docs.Select(d => d.TagCode), ct);
        var now = DateTime.UtcNow;
        return docs.Select(d => Map(d, d.UploadedBy?.FullName, 0, tagLabels, now)).ToList();
    }

    // ------------------------------ Create / Update -------------------------

    public async Task<CapabilityDocumentResponse> CreateAsync(
        UpsertCapabilityDocumentRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var normalizedName = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new CapabilityDocumentOperationException("Tên hồ sơ là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(request.FilePath) || string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            throw new CapabilityDocumentOperationException("File tải lên là bắt buộc.");
        }

        var normalizedFilePath = NormalizeManagedPath(request.FilePath)
            ?? throw new CapabilityDocumentOperationException("File tải lên không hợp lệ.");

        await EnsureTagValidAsync(request.TagCode, ct);
        EnsureDateRangeValid(request.IssuedDate, request.ExpiryDate);

        var entity = new CapabilityDocument
        {
            Name = normalizedName,
            TagCode = request.TagCode.Trim(),
            IssuedDate = request.IssuedDate,
            ExpiryDate = request.ExpiryDate,
            Description = TrimOrNull(request.Description),
            FilePath = normalizedFilePath,
            OriginalFileName = request.OriginalFileName!.Trim(),
            FileSize = request.FileSize ?? 0,
            ContentType = (request.ContentType ?? "application/octet-stream").Trim(),
            CurrentVersion = 1,
            UploadedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.CapabilityDocuments.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Capability document {Id} created by user {UserId} (tag={Tag})",
            entity.Id, callerUserId, entity.TagCode);

        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<CapabilityDocumentResponse?> UpdateAsync(
        int id,
        UpsertCapabilityDocumentRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.CapabilityDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var normalizedName = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new CapabilityDocumentOperationException("Tên hồ sơ là bắt buộc.");
        }

        await EnsureTagValidAsync(request.TagCode, ct);
        EnsureDateRangeValid(request.IssuedDate, request.ExpiryDate);

        entity.Name = normalizedName;
        entity.TagCode = request.TagCode.Trim();
        entity.IssuedDate = request.IssuedDate;
        entity.ExpiryDate = request.ExpiryDate;
        entity.Description = TrimOrNull(request.Description);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Capability document {Id} metadata updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<CapabilityDocumentResponse?> ReplaceFileAsync(
        int id,
        ReplaceCapabilityDocumentFileRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var entity = await db.CapabilityDocuments
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var normalizedNewPath = NormalizeManagedPath(request.FilePath)
            ?? throw new CapabilityDocumentOperationException("File tải lên không hợp lệ.");
        if (string.Equals(normalizedNewPath, entity.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new CapabilityDocumentOperationException("File mới trùng với file hiện tại.");
        }

        // Snapshot the current file as the next version entry (preserves history).
        var snapshot = new CapabilityDocumentVersion
        {
            CapabilityDocumentId = entity.Id,
            VersionNumber = entity.CurrentVersion,
            FilePath = entity.FilePath,
            OriginalFileName = entity.OriginalFileName,
            FileSize = entity.FileSize,
            ContentType = entity.ContentType,
            UploadedByUserId = entity.UploadedByUserId,
            CreatedAt = entity.UpdatedAt,
        };
        db.CapabilityDocumentVersions.Add(snapshot);

        entity.CurrentVersion += 1;
        entity.FilePath = normalizedNewPath;
        entity.OriginalFileName = request.OriginalFileName.Trim();
        entity.FileSize = request.FileSize;
        entity.ContentType = (request.ContentType ?? "application/octet-stream").Trim();
        entity.UploadedByUserId = callerUserId;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Capability document {Id} file replaced -> V{Version} by user {UserId}",
            id, entity.CurrentVersion, callerUserId);

        return await GetAsync(id, ct);
    }

    // ------------------------------ Delete ----------------------------------

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.CapabilityDocuments
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;

        var paths = new List<string> { entity.FilePath };
        paths.AddRange(entity.Versions.Select(v => v.FilePath));

        db.CapabilityDocuments.Remove(entity);
        await db.SaveChangesAsync(ct);

        foreach (var p in paths.Distinct())
        {
            DeleteManagedFile(p);
        }

        logger.LogInformation("Capability document {Id} deleted (cleaned {FileCount} files)", id, paths.Count);
        return true;
    }

    // ------------------------------ Helpers ---------------------------------

    private CapabilityDocumentResponse Map(
        CapabilityDocument d,
        string? uploadedByName,
        int previousVersionCount,
        IReadOnlyDictionary<string, string?> tagLabels,
        DateTime now) => new()
        {
            Id = d.Id,
            Name = d.Name,
            TagCode = d.TagCode,
            TagLabel = tagLabels.TryGetValue(d.TagCode, out var label) ? label : null,
            IssuedDate = d.IssuedDate,
            ExpiryDate = d.ExpiryDate,
            Description = d.Description,
            FilePath = d.FilePath,
            OriginalFileName = d.OriginalFileName,
            FileSize = d.FileSize,
            ContentType = d.ContentType,
            CurrentVersion = d.CurrentVersion,
            ExpiryState = ComputeExpiryState(d.ExpiryDate, now),
            UploadedByUserId = d.UploadedByUserId,
            UploadedByName = uploadedByName,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            PreviousVersionCount = previousVersionCount,
        };

    /// <summary>
    /// Bucket a document by proximity to expiry so the FE can render the
    /// orange/red badge and callers can filter without recomputing the
    /// same math client-side.
    /// </summary>
    public static string ComputeExpiryState(DateTime? expiry, DateTime now)
    {
        if (!expiry.HasValue) return "none";
        var days = (expiry.Value - now).TotalDays;
        if (days < 0) return "expired";
        if (days <= CriticalWindowDays) return "critical";
        if (days <= WarningWindowDays) return "warning";
        return "ok";
    }

    private async Task<Dictionary<string, string?>> LoadTagLabelsAsync(IEnumerable<string> tagCodes, CancellationToken ct)
    {
        var codes = tagCodes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (codes.Count == 0) return new Dictionary<string, string?>();
        var rows = await db.MasterDataOptions
            .AsNoTracking()
            .Where(o => o.Category == TagMasterDataCategory && codes.Contains(o.Code))
            .Select(o => new { o.Code, o.Name })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Code, r => (string?)r.Name);
    }

    private async Task EnsureTagValidAsync(string? tagCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tagCode))
        {
            throw new CapabilityDocumentOperationException("Phân loại (tag) là bắt buộc.");
        }
        var trimmed = tagCode.Trim();
        var exists = await db.MasterDataOptions.AsNoTracking().AnyAsync(
            o => o.Category == TagMasterDataCategory && o.Code == trimmed && o.IsActive, ct);
        if (!exists)
        {
            throw new CapabilityDocumentOperationException($"Phân loại '{trimmed}' không hợp lệ.");
        }
    }

    private static void EnsureDateRangeValid(DateTime? issued, DateTime? expiry)
    {
        if (issued.HasValue && expiry.HasValue && expiry.Value < issued.Value)
        {
            throw new CapabilityDocumentOperationException("Ngày hết hạn phải sau ngày cấp.");
        }
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    /// <summary>
    /// Reject anything that is not under <c>/files/capability/</c> so a
    /// stray absolute URL or path-traversal payload cannot land in the
    /// entity. Returns the normalised host-relative path or <c>null</c>.
    /// </summary>
    public static string? NormalizeManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var trimmed = path.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var abs))
        {
            trimmed = abs.AbsolutePath;
        }
        if (!trimmed.StartsWith(ManagedFilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        // Guard against traversal — segment must be a single filename.
        var fileName = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }
        return $"{ManagedFilePrefix}{fileName}";
    }

    private void DeleteManagedFile(string? filePath)
    {
        var normalized = NormalizeManagedPath(filePath);
        if (normalized is null) return;
        try
        {
            var relative = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_webRoot, relative);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            // Cleanup errors must not break the request — log and continue.
            logger.LogWarning(ex, "Failed to delete capability-document file {Path}", filePath);
        }
    }
}
