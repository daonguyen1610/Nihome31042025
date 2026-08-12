using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class VendorService(AppDbContext db) : IVendorService
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
            .Select(v => Map(v, v.CreatedBy != null ? v.CreatedBy.FullName : null))
            .ToListAsync(ct);

        return new VendorListResponse { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

    public async Task<VendorResponse?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Vendors.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => Map(v, v.CreatedBy != null ? v.CreatedBy.FullName : null))
            .FirstOrDefaultAsync(ct);

    public async Task<VendorResponse> CreateAsync(
        CreateVendorRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var code = NormalizeCode(request.VendorCode);
        ValidateRequiredText(code, request.CompanyName);
        await EnsureCodeAvailableAsync(code, null, ct);

        var now = DateTime.UtcNow;
        var vendor = new Vendor
        {
            VendorCode = code,
            CompanyName = request.CompanyName.Trim(),
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

        db.Vendors.Add(vendor);
        await db.SaveChangesAsync(ct);
        return Map(vendor, null);
    }

    public async Task<VendorResponse?> UpdateAsync(
        int id,
        UpdateVendorRequest request,
        int callerUserId,
        CancellationToken ct = default)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vendor is null) return null;

        var code = NormalizeCode(request.VendorCode);
        ValidateRequiredText(code, request.CompanyName);
        await EnsureCodeAvailableAsync(code, id, ct);

        vendor.VendorCode = code;
        vendor.CompanyName = request.CompanyName.Trim();
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
        return Map(vendor, null);
    }

    private async Task EnsureCodeAvailableAsync(string code, int? existingId, CancellationToken ct)
    {
        if (await db.Vendors.AsNoTracking().AnyAsync(v => v.VendorCode == code && v.Id != existingId, ct))
        {
            throw new VendorDuplicateException($"Vendor code '{code}' already exists.");
        }
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateRequiredText(string code, string companyName)
    {
        if (code.Length == 0) throw new VendorOperationException("VendorCode is required.");
        if (string.IsNullOrWhiteSpace(companyName)) throw new VendorOperationException("CompanyName is required.");
    }

    private static VendorResponse Map(Vendor vendor, string? createdByName) => new()
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
        UpdatedAt = vendor.UpdatedAt,
    };
}