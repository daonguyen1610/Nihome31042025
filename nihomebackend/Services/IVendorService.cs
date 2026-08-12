using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IVendorService
{
    Task<VendorListResponse> ListAsync(
        VendorType? vendorType = null,
        bool? isActive = null,
        string? search = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<VendorResponse?> GetAsync(int id, CancellationToken ct = default);
    Task<VendorResponse> CreateAsync(CreateVendorRequest request, int callerUserId, CancellationToken ct = default);
    Task<VendorResponse?> UpdateAsync(int id, UpdateVendorRequest request, int callerUserId, CancellationToken ct = default);
    Task<VendorResponse?> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class VendorDuplicateException(string message) : InvalidOperationException(message);
public sealed class VendorOperationException(string message) : InvalidOperationException(message);