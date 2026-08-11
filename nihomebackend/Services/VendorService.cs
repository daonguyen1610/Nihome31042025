using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class VendorService(
    AppDbContext db,
    IWebHostEnvironment environment,
    INotificationService notifications,
    ILogger<VendorService> logger) : IVendorService
{
    public const long MaxDocumentSizeBytes = 20 * 1024 * 1024;
    public const long MaxDocumentRequestSizeBytes = MaxDocumentSizeBytes + 1024 * 1024;
    public const string ServiceGroupCategory = "vendor_service_group";
    private const int MaxPageSize = 100;
    private const string StorageFolder = "vendor-documents";
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg" };
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public async Task<VendorListResponse> ListAsync(
        int callerUserId, bool canSeeAll, string? search, VendorType? type, bool? isActive,
        int? ownerUserId, string? serviceGroupCode, string? sortBy, string? sortDirection,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = BuildFilteredQuery(callerUserId, canSeeAll, search, type, isActive, ownerUserId, serviceGroupCode);
        var total = await query.CountAsync(ct);
        var rows = await ApplySort(query, sortBy, sortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(v => v.Owner)
            .Include(v => v.Evaluations)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new VendorListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(v => Map(v, includeChildren: false)).ToList(),
        };
    }

    public async Task<List<VendorResponse>> ExportAsync(
        int callerUserId, bool canSeeAll, string? search, VendorType? type, bool? isActive,
        int? ownerUserId, string? serviceGroupCode, string? sortBy, string? sortDirection,
        CancellationToken ct = default)
    {
        var rows = await ApplySort(
                BuildFilteredQuery(callerUserId, canSeeAll, search, type, isActive, ownerUserId, serviceGroupCode),
                sortBy, sortDirection)
            .Include(v => v.Owner)
            .Include(v => v.Evaluations)
            .AsSplitQuery()
            .ToListAsync(ct);
        return rows.Select(v => Map(v, includeChildren: false)).ToList();
    }

    public Task<List<VendorOwnerOptionResponse>> GetOwnerOptionsAsync(CancellationToken ct = default) =>
        db.Users.AsNoTracking()
            .Where(user => user.IsActive)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Select(user => new VendorOwnerOptionResponse
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
            })
            .ToListAsync(ct);

    public Task<List<VendorProjectOptionResponse>> GetProjectOptionsAsync(CancellationToken ct = default) =>
        db.DesignProjects.AsNoTracking()
            .OrderByDescending(project => project.UpdatedAt)
            .ThenBy(project => project.ProjectCode)
            .Select(project => new VendorProjectOptionResponse
            {
                Id = project.Id,
                ProjectCode = project.ProjectCode,
                Name = project.Name,
            })
            .ToListAsync(ct);

    public async Task<VendorResponse?> GetAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.AsNoTracking()
            .Include(v => v.Owner)
            .Include(v => v.Documents)
            .Include(v => v.Evaluations).ThenInclude(e => e.Project)
            .Include(v => v.Evaluations).ThenInclude(e => e.EvaluatedBy)
            .Include(v => v.Evaluations).ThenInclude(e => e.UpdatedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        return vendor is null || !IsInScope(vendor, callerUserId, canSeeAll)
            ? null
            : Map(vendor, includeChildren: true);
    }

    public async Task<VendorResponse> CreateAsync(
        CreateVendorRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        EnsureOwnerAssignmentAllowed(request.OwnerUserId, callerUserId, canSeeAll);
        var normalized = await ValidateAsync(request, existingId: null, ct);
        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            VendorCode = normalized.Code,
            CompanyName = request.CompanyName.Trim(),
            NormalizedCompanyName = normalized.CompanyName,
            VendorType = request.VendorType,
            TaxCode = TrimOrNull(request.TaxCode),
            Phone = TrimOrNull(request.Phone),
            Email = TrimOrNull(request.Email),
            Address = TrimOrNull(request.Address),
            ContactPerson = TrimOrNull(request.ContactPerson),
            LicenseNo = TrimOrNull(request.LicenseNo),
            ServiceGroupCode = normalized.ServiceGroupCode,
            OwnerUserId = request.OwnerUserId,
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedByUserId = callerUserId,
            UpdatedAt = now,
            UpdatedByUserId = callerUserId,
        };
        db.Vendors.Add(vendor);
        await SaveChangesAsync(ct);
        await NotifyAssignedAsync(vendor, ct);
        await db.Entry(vendor).Reference(v => v.Owner).LoadAsync(ct);
        return Map(vendor, includeChildren: false);
    }

    public async Task<VendorResponse?> UpdateAsync(
        int id, UpdateVendorRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vendor = await db.Vendors.Include(v => v.Owner).FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vendor is null || !IsInScope(vendor, callerUserId, canSeeAll)) return null;

        EnsureOwnerAssignmentAllowed(request.OwnerUserId, callerUserId, canSeeAll);
        var normalized = await ValidateAsync(request, id, ct);
        var previousOwnerId = vendor.OwnerUserId;
        vendor.VendorCode = normalized.Code;
        vendor.CompanyName = request.CompanyName.Trim();
        vendor.NormalizedCompanyName = normalized.CompanyName;
        vendor.VendorType = request.VendorType;
        vendor.TaxCode = TrimOrNull(request.TaxCode);
        vendor.Phone = TrimOrNull(request.Phone);
        vendor.Email = TrimOrNull(request.Email);
        vendor.Address = TrimOrNull(request.Address);
        vendor.ContactPerson = TrimOrNull(request.ContactPerson);
        vendor.LicenseNo = TrimOrNull(request.LicenseNo);
        vendor.ServiceGroupCode = normalized.ServiceGroupCode;
        vendor.OwnerUserId = request.OwnerUserId;
        vendor.IsActive = request.IsActive;
        vendor.UpdatedAt = DateTime.UtcNow;
        vendor.UpdatedByUserId = callerUserId;
        await SaveChangesAsync(ct);

        if (previousOwnerId != vendor.OwnerUserId) await NotifyAssignedAsync(vendor, ct);
        await db.Entry(vendor).Reference(v => v.Owner).LoadAsync(ct);
        return Map(vendor, includeChildren: false);
    }

    public async Task<VendorDocumentResponse?> UploadDocumentAsync(
        int vendorId, VendorDocumentType documentType, IFormFile file, int callerUserId, bool canSeeAll,
        CancellationToken ct = default)
    {
        var vendor = await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct);
        if (vendor is null) return null;
        if (!Enum.IsDefined(documentType)) throw new VendorOperationException("Invalid vendor document type.");
        ValidateDocument(file);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var directory = GetVendorStorageDirectory(environment.ContentRootPath, vendorId);
        Directory.CreateDirectory(directory);
        var physicalPath = Path.Combine(directory, storedFileName);
        try
        {
            await using var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(output, ct);
            var document = new VendorDocument
            {
                VendorId = vendorId,
                DocumentType = documentType,
                OriginalFileName = Path.GetFileName(file.FileName),
                StoredFileName = storedFileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                FileSizeBytes = file.Length,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = callerUserId,
            };
            db.VendorDocuments.Add(document);
            await SaveChangesAsync(ct);
            return MapDocument(document);
        }
        catch
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    public async Task<VendorDocumentDownload?> DownloadDocumentAsync(
        int vendorId, int documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct) is null) return null;
        var document = await db.VendorDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.VendorId == vendorId, ct);
        if (document is null) return null;
        var path = GetDocumentPath(environment.ContentRootPath, vendorId, document.StoredFileName);
        if (!File.Exists(path)) throw new VendorDocumentMissingException("Vendor document metadata exists, but the physical file was not found.");
        return new VendorDocumentDownload
        {
            Content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            ContentType = document.ContentType,
            FileName = document.OriginalFileName,
        };
    }

    public async Task<bool> DeleteDocumentAsync(
        int vendorId, int documentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct) is null) return false;
        var document = await db.VendorDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.VendorId == vendorId, ct);
        if (document is null) return false;
        var path = GetDocumentPath(environment.ContentRootPath, vendorId, document.StoredFileName);
        db.VendorDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
        if (File.Exists(path)) File.Delete(path);
        return true;
    }

    public async Task<VendorEvaluationResponse?> CreateEvaluationAsync(
        int vendorId, UpsertVendorEvaluationRequest request, int callerUserId, bool canSeeAll,
        CancellationToken ct = default)
    {
        var vendor = await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct);
        if (vendor is null) return null;
        await ValidateEvaluationAsync(vendorId, request, existingId: null, ct);
        var now = DateTime.UtcNow;
        var evaluation = new VendorEvaluation
        {
            VendorId = vendorId,
            ProjectId = request.ProjectId,
            ScoreQuality = request.ScoreQuality,
            ScoreSchedule = request.ScoreSchedule,
            ScoreCost = request.ScoreCost,
            ScoreSafety = request.ScoreSafety,
            Comment = TrimOrNull(request.Comment),
            EvaluatedByUserId = callerUserId,
            EvaluatedAt = now,
            UpdatedByUserId = callerUserId,
            UpdatedAt = now,
        };
        db.VendorEvaluations.Add(evaluation);
        await SaveChangesAsync(ct);
        await LoadEvaluationReferencesAsync(evaluation, ct);
        await NotifyEvaluationAsync(vendor, evaluation, ct);
        return MapEvaluation(evaluation);
    }

    public async Task<VendorEvaluationResponse?> UpdateEvaluationAsync(
        int vendorId, int evaluationId, UpsertVendorEvaluationRequest request, int callerUserId,
        bool canSeeAll, CancellationToken ct = default)
    {
        var vendor = await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct);
        if (vendor is null) return null;
        var evaluation = await db.VendorEvaluations
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.VendorId == vendorId, ct);
        if (evaluation is null) return null;
        await ValidateEvaluationAsync(vendorId, request, evaluationId, ct);
        evaluation.ProjectId = request.ProjectId;
        evaluation.ScoreQuality = request.ScoreQuality;
        evaluation.ScoreSchedule = request.ScoreSchedule;
        evaluation.ScoreCost = request.ScoreCost;
        evaluation.ScoreSafety = request.ScoreSafety;
        evaluation.Comment = TrimOrNull(request.Comment);
        evaluation.UpdatedByUserId = callerUserId;
        evaluation.UpdatedAt = DateTime.UtcNow;
        await SaveChangesAsync(ct);
        await LoadEvaluationReferencesAsync(evaluation, ct);
        await NotifyEvaluationAsync(vendor, evaluation, ct);
        return MapEvaluation(evaluation);
    }

    public async Task<bool> DeleteEvaluationAsync(
        int vendorId, int evaluationId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vendor = await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct);
        if (vendor is null) return false;
        var evaluation = await db.VendorEvaluations
            .Include(e => e.Project).Include(e => e.EvaluatedBy)
            .FirstOrDefaultAsync(e => e.Id == evaluationId && e.VendorId == vendorId, ct);
        if (evaluation is null) return false;
        db.VendorEvaluations.Remove(evaluation);
        await db.SaveChangesAsync(ct);
        await NotifyEvaluationAsync(vendor, evaluation, ct);
        return true;
    }

    public async Task<List<VendorAuditResponse>?> GetHistoryAsync(
        int vendorId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (await FindScopedAsync(vendorId, callerUserId, canSeeAll, ct) is null) return null;
        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.ResourceType == EntityTypes.Vendor && a.ResourceId == vendorId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new VendorAuditResponse
            {
                Id = a.Id,
                Action = a.Action,
                Message = a.Message,
                ActorUserId = a.ActorUserId,
                ActorPhone = a.ActorPhone,
                Status = a.Status,
                OldValueJson = a.OldValueJson,
                NewValueJson = a.NewValueJson,
                CreatedAt = a.CreatedAt,
            }).ToListAsync(ct);
    }

    public static string NormalizeCompanyName(string value) =>
        WhitespaceRegex.Replace(value.Trim(), " ").ToUpperInvariant();

    public static string GetVendorStorageDirectory(string contentRootPath, int vendorId) =>
        Path.Combine(Path.GetFullPath(contentRootPath), "storage", StorageFolder, vendorId.ToString());

    public static string GetDocumentPath(string contentRootPath, int vendorId, string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        if (!string.Equals(safeName, storedFileName, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(safeName))
            throw new VendorOperationException("Invalid stored document name.");
        return Path.Combine(GetVendorStorageDirectory(contentRootPath, vendorId), safeName);
    }

    public static void ValidateDocument(IFormFile file)
    {
        if (file is null || file.Length == 0) throw new VendorOperationException("A non-empty document is required.");
        if (file.Length > MaxDocumentSizeBytes) throw new VendorOperationException("Document exceeds the 20MB limit.");
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new VendorOperationException("Unsupported document type. Allowed: PDF, DOC, DOCX, XLS, XLSX, PNG, JPG.");
    }

    private IQueryable<Vendor> BuildFilteredQuery(
        int callerUserId, bool canSeeAll, string? search, VendorType? type, bool? isActive,
        int? ownerUserId, string? serviceGroupCode)
    {
        var query = db.Vendors.AsNoTracking().AsQueryable();
        if (!canSeeAll) query = query.Where(v => v.OwnerUserId == callerUserId);
        else if (ownerUserId.HasValue) query = query.Where(v => v.OwnerUserId == ownerUserId.Value);
        if (type.HasValue) query = query.Where(v => v.VendorType == type.Value);
        if (isActive.HasValue) query = query.Where(v => v.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(serviceGroupCode))
        {
            var group = serviceGroupCode.Trim().ToLowerInvariant();
            query = query.Where(v => v.ServiceGroupCode.ToLower() == group);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(v =>
                v.VendorCode.ToLower().Contains(term) || v.CompanyName.ToLower().Contains(term) ||
                (v.TaxCode != null && v.TaxCode.ToLower().Contains(term)) ||
                (v.ContactPerson != null && v.ContactPerson.ToLower().Contains(term)) ||
                (v.Email != null && v.Email.ToLower().Contains(term)) ||
                (v.Phone != null && v.Phone.ToLower().Contains(term)));
        }
        return query;
    }

    private static IOrderedQueryable<Vendor> ApplySort(IQueryable<Vendor> query, string? sortBy, string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "vendorcode" => descending ? query.OrderByDescending(v => v.VendorCode) : query.OrderBy(v => v.VendorCode),
            "vendortype" => descending ? query.OrderByDescending(v => v.VendorType) : query.OrderBy(v => v.VendorType),
            "updatedat" => descending ? query.OrderByDescending(v => v.UpdatedAt) : query.OrderBy(v => v.UpdatedAt),
            "createdat" => descending ? query.OrderByDescending(v => v.CreatedAt) : query.OrderBy(v => v.CreatedAt),
            "isactive" => descending ? query.OrderByDescending(v => v.IsActive) : query.OrderBy(v => v.IsActive),
            _ => descending ? query.OrderByDescending(v => v.CompanyName) : query.OrderBy(v => v.CompanyName),
        };
    }

    private async Task<(string Code, string CompanyName, string ServiceGroupCode)> ValidateAsync(
        CreateVendorRequest request, int? existingId, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.VendorType)) throw new VendorOperationException("Invalid vendor type.");
        var code = request.VendorCode.Trim().ToUpperInvariant();
        var company = NormalizeCompanyName(request.CompanyName);
        var serviceGroup = request.ServiceGroupCode.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(serviceGroup))
            throw new VendorOperationException("Vendor code, company name, owner and service group are required.");
        if (await db.Vendors.AnyAsync(v => v.Id != existingId && v.VendorCode == code, ct))
            throw new VendorOperationException("Vendor code already exists.");
        if (await db.Vendors.AnyAsync(v => v.Id != existingId && v.NormalizedCompanyName == company, ct))
            throw new VendorOperationException("Company name already exists.");
        var taxCode = TrimOrNull(request.TaxCode);
        if (taxCode != null && await db.Vendors.AnyAsync(v => v.Id != existingId && v.TaxCode != null && v.TaxCode.ToUpper() == taxCode.ToUpper(), ct))
            throw new VendorOperationException("Tax code already exists.");
        if (!await db.Users.AnyAsync(u => u.Id == request.OwnerUserId && u.IsActive, ct))
            throw new VendorOperationException("Owner must be an active user.");
        if (!await db.MasterDataOptions.AnyAsync(m => m.Category == ServiceGroupCategory && m.Code == serviceGroup && m.IsActive, ct))
            throw new VendorOperationException("Service group is invalid or inactive.");
        return (code, company, serviceGroup);
    }

    private async Task ValidateEvaluationAsync(int vendorId, UpsertVendorEvaluationRequest request, int? existingId, CancellationToken ct)
    {
        if (request.ScoreQuality > 10 || request.ScoreSchedule > 10 || request.ScoreCost > 10 || request.ScoreSafety > 10)
            throw new VendorOperationException("Evaluation scores must be between 0 and 10.");
        if (!await db.DesignProjects.AnyAsync(p => p.Id == request.ProjectId, ct))
            throw new VendorOperationException("Project does not exist.");
        if (await db.VendorEvaluations.AnyAsync(e => e.VendorId == vendorId && e.ProjectId == request.ProjectId && e.Id != existingId, ct))
            throw new VendorOperationException("This vendor already has an evaluation for the project.");
    }

    private Task<Vendor?> FindScopedAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct) =>
        db.Vendors.FirstOrDefaultAsync(v => v.Id == id && (canSeeAll || v.OwnerUserId == callerUserId), ct);

    private static bool IsInScope(Vendor vendor, int callerUserId, bool canSeeAll) =>
        canSeeAll || vendor.OwnerUserId == callerUserId;

    private static void EnsureOwnerAssignmentAllowed(int ownerUserId, int callerUserId, bool canSeeAll)
    {
        if (!canSeeAll && ownerUserId != callerUserId)
            throw new VendorOperationException("You can only assign vendors to yourself.");
    }

    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Vendor write conflicted with an existing record.");
            throw new VendorOperationException("Vendor data conflicts with an existing record.");
        }
    }

    private async Task LoadEvaluationReferencesAsync(VendorEvaluation evaluation, CancellationToken ct)
    {
        await db.Entry(evaluation).Reference(e => e.Project).LoadAsync(ct);
        await db.Entry(evaluation).Reference(e => e.EvaluatedBy).LoadAsync(ct);
        await db.Entry(evaluation).Reference(e => e.UpdatedBy).LoadAsync(ct);
    }

    private async Task NotifyAssignedAsync(Vendor vendor, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyFromTemplateAsync(vendor.OwnerUserId, "vendor.assigned",
                new Dictionary<string, string> { ["vendorCode"] = vendor.VendorCode, ["companyName"] = vendor.CompanyName },
                EntityTypes.Vendor, vendor.Id, $"/admin/procurement/vendors/{vendor.Id}");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to notify owner for vendor {VendorId}", vendor.Id); }
    }

    private async Task NotifyEvaluationAsync(Vendor vendor, VendorEvaluation evaluation, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyManyFromTemplateAsync(
                new[] { vendor.OwnerUserId, evaluation.EvaluatedByUserId }.Distinct(),
                "vendor.evaluation-changed",
                new Dictionary<string, string>
                {
                    ["vendorCode"] = vendor.VendorCode,
                    ["companyName"] = vendor.CompanyName,
                    ["projectName"] = evaluation.Project?.Name ?? evaluation.ProjectId.ToString(),
                }, EntityTypes.Vendor, vendor.Id, $"/admin/procurement/vendors/{vendor.Id}");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to notify evaluation change for vendor {VendorId}", vendor.Id); }
    }

    private static VendorResponse Map(Vendor vendor, bool includeChildren)
    {
        var evaluationAverages = vendor.Evaluations.Select(Average).ToList();
        return new VendorResponse
        {
            Id = vendor.Id,
            VendorCode = vendor.VendorCode,
            CompanyName = vendor.CompanyName,
            VendorType = vendor.VendorType,
            TaxCode = vendor.TaxCode,
            Phone = vendor.Phone,
            Email = vendor.Email,
            Address = vendor.Address,
            ContactPerson = vendor.ContactPerson,
            LicenseNo = vendor.LicenseNo,
            ServiceGroupCode = vendor.ServiceGroupCode,
            OwnerUserId = vendor.OwnerUserId,
            OwnerName = vendor.Owner?.FullName ?? string.Empty,
            IsActive = vendor.IsActive,
            CreatedAt = vendor.CreatedAt,
            CreatedByUserId = vendor.CreatedByUserId,
            UpdatedAt = vendor.UpdatedAt,
            UpdatedByUserId = vendor.UpdatedByUserId,
            AverageScore = evaluationAverages.Count == 0 ? null : Math.Round(evaluationAverages.Average(), 2),
            Documents = includeChildren ? vendor.Documents.OrderByDescending(d => d.CreatedAt).Select(MapDocument).ToList() : new(),
            Evaluations = includeChildren ? vendor.Evaluations.OrderByDescending(e => e.EvaluatedAt).Select(MapEvaluation).ToList() : new(),
        };
    }

    private static VendorDocumentResponse MapDocument(VendorDocument document) => new()
    {
        Id = document.Id,
        DocumentType = document.DocumentType,
        OriginalFileName = document.OriginalFileName,
        ContentType = document.ContentType,
        FileSizeBytes = document.FileSizeBytes,
        CreatedAt = document.CreatedAt,
        CreatedByUserId = document.CreatedByUserId,
    };

    private static VendorEvaluationResponse MapEvaluation(VendorEvaluation evaluation) => new()
    {
        Id = evaluation.Id,
        ProjectId = evaluation.ProjectId,
        ProjectName = evaluation.Project?.Name ?? string.Empty,
        ProjectCode = evaluation.Project?.ProjectCode ?? string.Empty,
        ScoreQuality = evaluation.ScoreQuality,
        ScoreSchedule = evaluation.ScoreSchedule,
        ScoreCost = evaluation.ScoreCost,
        ScoreSafety = evaluation.ScoreSafety,
        AverageScore = Average(evaluation),
        Comment = evaluation.Comment,
        EvaluatedByUserId = evaluation.EvaluatedByUserId,
        EvaluatorName = evaluation.EvaluatedBy?.FullName ?? string.Empty,
        EvaluatedAt = evaluation.EvaluatedAt,
        UpdatedByUserId = evaluation.UpdatedByUserId,
        UpdatedByName = evaluation.UpdatedBy?.FullName ?? string.Empty,
        UpdatedAt = evaluation.UpdatedAt,
    };

    private static decimal Average(VendorEvaluation evaluation) =>
        Math.Round((evaluation.ScoreQuality + evaluation.ScoreSchedule + evaluation.ScoreCost + evaluation.ScoreSafety) / 4m, 2);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
