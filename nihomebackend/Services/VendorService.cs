using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services;

public class VendorService(
    AppDbContext db,
    IBusinessDocumentStorageService? documentStorage = null,
    IPermissionService? permissions = null,
    IProjectAccessService? projectAccess = null,
    IAuditLogger? audit = null) : IVendorService
{
    private const int MaxPageSize = 100;

    public async Task<VendorListResponse> ListAsync(
        VendorType? vendorType = null,
        bool? isActive = null,
        string? search = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Vendors.AsNoTracking().AsQueryable();
        if (vendorType.HasValue) query = query.Where(v => v.VendorType == vendorType.Value);
        if (isActive.HasValue) query = query.Where(v => v.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(v =>
                EF.Functions.Like(v.VendorCode, like) ||
                EF.Functions.Like(v.CompanyName, like) ||
                (v.TaxCode != null && EF.Functions.Like(v.TaxCode, like)) ||
                (v.ContactPerson != null && EF.Functions.Like(v.ContactPerson, like)) ||
                (v.Phone != null && EF.Functions.Like(v.Phone, like)) ||
                (v.Email != null && EF.Functions.Like(v.Email, like)) ||
                (v.TradeCategory != null && EF.Functions.Like(v.TradeCategory, like)));
        }

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Vendor> orderedQuery = (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("vendorcode", false) => query.OrderBy(v => v.VendorCode),
            ("vendorcode", true) => query.OrderByDescending(v => v.VendorCode),
            ("companyname", false) => query.OrderBy(v => v.CompanyName),
            ("companyname", true) => query.OrderByDescending(v => v.CompanyName),
            ("vendortype", false) => query.OrderBy(v => v.VendorType),
            ("vendortype", true) => query.OrderByDescending(v => v.VendorType),
            ("updatedat", false) => query.OrderBy(v => v.UpdatedAt),
            ("updatedat", true) => query.OrderByDescending(v => v.UpdatedAt),
            (_, false) => query.OrderBy(v => v.CreatedAt),
            _ => query.OrderByDescending(v => v.CreatedAt),
        };

        var total = await query.CountAsync(ct);
        var items = await orderedQuery
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => Map(v, v.CreatedBy != null ? v.CreatedBy.FullName : null, null))
            .ToListAsync(ct);

        return new VendorListResponse { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

    public async Task<VendorResponse?> GetAsync(int id, int? callerUserId = null, CancellationToken ct = default) =>
        await GetDetailAsync(id, callerUserId, ct);

    public async Task<VendorResponse> CreateAsync(
        CreateVendorRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var code = NormalizeCode(request.VendorCode);
        var companyName = request.CompanyName.Trim();
        ValidateRequest(code, companyName, request.Phone, request.Email,
            request.CapabilityFileUrl, request.DriveFolder);
        await EnsureUniqueAsync(code, companyName, null, ct);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            VendorCode = code,
            CompanyName = companyName,
            VendorType = request.VendorType!.Value,
            TaxCode = TrimOrNull(request.TaxCode),
            Phone = TrimOrNull(request.Phone),
            Email = TrimOrNull(request.Email)?.ToLowerInvariant(),
            Address = TrimOrNull(request.Address),
            ContactPerson = TrimOrNull(request.ContactPerson),
            LicenseNo = TrimOrNull(request.LicenseNo),
            TradeCategory = TrimOrNull(request.TradeCategory),
            CapabilityFileUrl = TrimOrNull(request.CapabilityFileUrl),
            DriveFolder = TrimOrNull(request.DriveFolder),
            IsActive = true,
            CreatedByUserId = callerUserId,
            CreatedAt = now,
            UpdatedByUserId = callerUserId,
            UpdatedAt = now,
        };

        var upload = await GetUploadClaimAsync(request.CapabilityFileUrl, request.CapabilityUploadToken, callerUserId, ct);
        if (upload is not null)
        {
            upload.Vendor = vendor;
            upload.ClaimedAt = DateTime.UtcNow;
        }
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync(ct);
        await db.Entry(vendor).Reference(v => v.CreatedBy).LoadAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Map(vendor, vendor.CreatedBy?.FullName);
    }

    public async Task<VendorResponse?> UpdateAsync(
        int id,
        UpdateVendorRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var vendor = await db.Vendors
            .Include(v => v.CreatedBy)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vendor is null) return null;

        var code = NormalizeCode(request.VendorCode);
        var companyName = request.CompanyName.Trim();
        ValidateRequest(code, companyName, request.Phone, request.Email,
            request.CapabilityFileUrl, request.DriveFolder);
        await EnsureUniqueAsync(code, companyName, id, ct);
        CrmConcurrency.Apply(db, vendor, request.RowVersion);

        var previousCapabilityFileUrl = vendor.CapabilityFileUrl;
        var capabilityChanged = !string.Equals(previousCapabilityFileUrl, request.CapabilityFileUrl?.Trim(), StringComparison.Ordinal);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        if (capabilityChanged)
        {
            var replacementUpload = await GetUploadClaimAsync(
                request.CapabilityFileUrl, request.CapabilityUploadToken, callerUserId, ct);
            var previousClaim = await db.VendorDocumentUploads.SingleOrDefaultAsync(item => item.VendorId == id, ct);
            if (previousClaim is not null)
            {
                previousClaim.VendorId = null;
                previousClaim.Vendor = null;
                previousClaim.ClaimedAt = null;
                previousClaim.CreatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            if (replacementUpload is not null)
            {
                replacementUpload.Vendor = vendor;
                replacementUpload.ClaimedAt = DateTime.UtcNow;
            }
        }
        vendor.VendorCode = code;
        vendor.CompanyName = companyName;
        vendor.VendorType = request.VendorType!.Value;
        vendor.TaxCode = TrimOrNull(request.TaxCode);
        vendor.Phone = TrimOrNull(request.Phone);
        vendor.Email = TrimOrNull(request.Email)?.ToLowerInvariant();
        vendor.Address = TrimOrNull(request.Address);
        vendor.ContactPerson = TrimOrNull(request.ContactPerson);
        vendor.LicenseNo = TrimOrNull(request.LicenseNo);
        vendor.TradeCategory = TrimOrNull(request.TradeCategory);
        vendor.CapabilityFileUrl = TrimOrNull(request.CapabilityFileUrl);
        vendor.DriveFolder = TrimOrNull(request.DriveFolder);
        vendor.IsActive = request.IsActive;
        vendor.UpdatedByUserId = callerUserId;
        vendor.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        if (capabilityChanged && !string.IsNullOrWhiteSpace(previousCapabilityFileUrl))
        {
            audit?.Log(new AuditEvent
            {
                Action = "vendor-document.replace",
                ResourceType = EntityTypes.Vendor,
                ResourceId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Message = "Vendor capability document replaced or removed.",
                OldValue = new { Path = previousCapabilityFileUrl },
                NewValue = new { Path = vendor.CapabilityFileUrl },
            });
        }
        return await GetDetailAsync(id, callerUserId, ct);
    }

    private async Task EnsureUniqueAsync(string code, string companyName, int? existingId, CancellationToken ct)
    {
        if (await db.Vendors.AsNoTracking().AnyAsync(v => v.VendorCode == code && v.Id != existingId, ct))
        {
            throw new VendorDuplicateException($"Vendor code '{code}' already exists.");
        }
        var normalizedCompanyName = companyName.ToUpper();
        if (await db.Vendors.AsNoTracking().AnyAsync(v =>
            v.CompanyName.ToUpper() == normalizedCompanyName && v.Id != existingId, ct))
        {
            throw new VendorDuplicateException($"Company name '{companyName}' already exists.");
        }
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateRequest(
        string code, string companyName, string? phone, string? email,
        string? capabilityFileUrl, string? driveFolder)
    {
        if (code.Length == 0) throw new VendorOperationException("VendorCode is required.");
        if (string.IsNullOrWhiteSpace(companyName)) throw new VendorOperationException("CompanyName is required.");
        var contactError = ContactValidation.Validate(phone, email);
        if (contactError is not null) throw new VendorOperationException(contactError);
        ValidateUrl(capabilityFileUrl, allowManagedVendorFile: true, "CapabilityFileUrl");
        ValidateUrl(driveFolder, allowManagedVendorFile: false, "DriveFolder");
    }

    private static void ValidateUrl(string? value, bool allowManagedVendorFile, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var trimmed = value.Trim();
        if (allowManagedVendorFile && trimmed.StartsWith("/files/business-documents/vendors/", StringComparison.Ordinal) &&
            string.Equals(trimmed, $"/files/business-documents/vendors/{Path.GetFileName(trimmed)}", StringComparison.Ordinal)) return;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") return;
        throw new VendorOperationException($"{field} must be an approved vendor file path or an HTTP(S) URL.");
    }

    private async Task<VendorDocumentUpload?> GetUploadClaimAsync(
        string? value, Guid? claimToken, int callerUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/files/business-documents/vendors/", StringComparison.Ordinal)) return null;
        if (!claimToken.HasValue)
            throw new VendorOperationException("CapabilityUploadToken is required for a managed vendor document.");
        var upload = await db.VendorDocumentUploads.SingleOrDefaultAsync(item => item.Token == claimToken.Value, ct);
        if (upload is null || upload.VendorId.HasValue || upload.CreatedByUserId != callerUserId ||
            !string.Equals(upload.Path, value.Trim(), StringComparison.Ordinal))
            throw new VendorOperationException("The vendor document claim is invalid, expired, or already used.");
        if (documentStorage?.GetContent(BusinessDocumentArea.Vendors, Path.GetFileName(upload.Path)) is null)
            throw new VendorOperationException("The uploaded vendor document no longer exists.");
        return upload;
    }

    private async Task<VendorResponse?> GetDetailAsync(int id, int? callerUserId, CancellationToken ct)
    {
        var vendor = await db.Vendors.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                Vendor = item,
                CreatedByName = item.CreatedBy != null ? item.CreatedBy.FullName : null,
                UpdatedByName = item.UpdatedBy != null ? item.UpdatedBy.FullName : null,
            }).SingleOrDefaultAsync(ct);
        if (vendor is null) return null;
        var response = Map(vendor.Vendor, vendor.CreatedByName, vendor.UpdatedByName);
        if (callerUserId.HasValue && permissions is not null &&
            await permissions.HasAsync(callerUserId.Value, "crm.contracts.view", ct))
        {
            var canViewAllContracts = await permissions.HasAsync(callerUserId.Value, "crm.contracts.view.all", ct);
            response.Contracts = await db.Contracts.AsNoTracking()
                .Where(item => item.VendorId == id && (canViewAllContracts || item.OwnerUserId == callerUserId.Value))
                .OrderByDescending(item => item.UpdatedAt).Select(item => new VendorContractSummaryResponse
                {
                    Id = item.Id,
                    ContractNumber = item.ContractNumber,
                    Status = item.Status.ToString(),
                    OperationalProjectId = item.OperationalProjectId,
                    OperationalProjectCode = item.OperationalProject != null ? item.OperationalProject.Code : null,
                    OperationalProjectName = item.OperationalProject != null ? item.OperationalProject.Name : null,
                }).ToListAsync(ct);
        }
        if (callerUserId.HasValue && permissions is not null && projectAccess is not null &&
            await permissions.HasAsync(callerUserId.Value, "proc.vendor-ratings.view", ct))
        {
            var projectIds = await projectAccess.GetAccessibleOperationalProjectIdsAsync(callerUserId.Value, ct);
            response.Ratings = await db.VendorRatings.AsNoTracking()
                .Where(item => item.VendorId == id && projectIds.Contains(item.OperationalProjectId))
                .OrderByDescending(item => item.Id).Select(item => new VendorRatingSummaryResponse
                {
                    Id = item.Id,
                    ContractId = item.ContractId,
                    ContractNumber = item.Contract.ContractNumber,
                    OperationalProjectId = item.OperationalProjectId,
                    OperationalProjectCode = item.OperationalProject.Code,
                    Status = item.Status.ToString(),
                    OverallScore = item.OverallScore,
                    ApprovedAt = item.ApprovedAt,
                }).ToListAsync(ct);
        }
        var resourceId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        response.History = await (
            from item in db.AuditLogs.AsNoTracking()
            join user in db.Users.AsNoTracking() on item.ActorUserId equals (int?)user.Id into actors
            from actor in actors.DefaultIfEmpty()
            where item.ResourceType == EntityTypes.Vendor && item.ResourceId == resourceId
            orderby item.CreatedAt descending
            select new VendorHistorySummaryResponse
            {
                Id = item.Id,
                Action = item.Action,
                Message = item.Message,
                ActorUserId = item.ActorUserId,
                ActorName = actor != null ? actor.FullName : item.ActorPhone,
                CreatedAt = item.CreatedAt,
            }).Take(50).ToListAsync(ct);
        return response;
    }

    private static VendorResponse Map(Vendor vendor, string? createdByName, string? updatedByName = null) => new()
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
        TradeCategory = vendor.TradeCategory,
        CapabilityFileUrl = vendor.CapabilityFileUrl,
        DriveFolder = vendor.DriveFolder,
        IsActive = vendor.IsActive,
        CreatedByUserId = vendor.CreatedByUserId,
        CreatedByName = createdByName,
        CreatedAt = vendor.CreatedAt,
        UpdatedByUserId = vendor.UpdatedByUserId,
        UpdatedByName = updatedByName,
        UpdatedAt = vendor.UpdatedAt,
        RowVersion = CrmConcurrency.Encode(vendor.RowVersion),
    };
}