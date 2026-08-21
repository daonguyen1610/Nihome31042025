using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface ICustomerDocumentService
{
    Task<List<CustomerDocumentResponse>?> ListAsync(
        int customerId, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<CustomerDocumentResponse?> UploadAsync(
        int customerId,
        IFormFile? file,
        string? label,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<ManagedDocumentContent?> GetContentAsync(
        int customerId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int customerId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    void DeleteCustomerFiles(int customerId);
}

public sealed class CustomerDocumentException(string message) : InvalidOperationException(message);