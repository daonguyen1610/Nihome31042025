using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IQuoteDocumentService
{
    Task<List<QuoteDocumentResponse>?> ListAsync(
        int quoteId, int callerUserId, bool canSeeAll, CancellationToken ct = default);

    Task<QuoteDocumentResponse?> UploadAsync(
        int quoteId,
        IFormFile? file,
        string? label,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<QuoteDocumentContent?> GetContentAsync(
        int quoteId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        int quoteId,
        int documentId,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    void DeleteQuoteFiles(int quoteId);
}

public sealed record QuoteDocumentContent(
    string FullPath,
    string OriginalFileName,
    string ContentType);

public sealed class QuoteDocumentException(string message) : InvalidOperationException(message);
