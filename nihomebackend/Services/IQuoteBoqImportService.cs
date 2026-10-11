using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IQuoteBoqImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);
    Task<QuoteBoqImportPreviewResponse> PreviewAsync(
        int quoteId, int callerUserId, IFormFile? file, CancellationToken ct = default);
    Task<QuoteBoqImportResponse?> ConfirmAsync(
        int quoteId,
        IFormFile? file,
        ConfirmQuoteBoqImportRequest request,
        int callerUserId,
        bool canManage,
        bool canSeeAll,
        CancellationToken ct = default);
}
