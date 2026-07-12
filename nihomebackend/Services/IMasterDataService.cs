using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IMasterDataService
{
    Task<List<MasterDataCategoryResponse>> GetCategoriesAsync(CancellationToken ct = default);

    Task<List<MasterDataOptionResponse>> GetByCategoryAsync(
        string category,
        bool includeInactive = false,
        CancellationToken ct = default);

    Task<MasterDataOptionResponse?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<MasterDataOptionResponse> CreateAsync(string category, UpsertMasterDataOptionRequest req, CancellationToken ct = default);

    Task<MasterDataOptionResponse?> UpdateAsync(int id, UpsertMasterDataOptionRequest req, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Thrown when the caller tries to create/update an option with a code
/// that already exists inside the same category.
/// </summary>
public class MasterDataDuplicateCodeException(string category, string code)
    : InvalidOperationException($"Master-data code '{code}' already exists in category '{category}'.")
{
    public string Category { get; } = category;
    public string Code { get; } = code;
}
