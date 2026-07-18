using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Thrown for expected business-rule violations in the Tender workflow —
/// deadline in the past, editing locked fields on a submitted tender, etc.
/// Controller converts to HTTP 400.
/// </summary>
public class TenderOperationException(string message) : Exception(message)
{
}

/// <summary>
/// Tender (Gói thầu) service — CRUD, auto-generated preparation checklist
/// on create, and status-aware edit rules. Result-transition workflow
/// (Mark Won / Mark Lost + auto-create Contract) ships with the detail
/// slice (NIH-97).
/// </summary>
public interface ITenderService
{
    Task<TenderListResponse> ListAsync(TenderListParams parameters, CancellationToken ct = default);

    Task<TenderResponse?> GetAsync(int id, CancellationToken ct = default);

    Task<TenderResponse> CreateAsync(CreateTenderRequest request, int callerUserId, CancellationToken ct = default);

    Task<TenderResponse?> UpdateAsync(int id, UpdateTenderRequest request, int callerUserId, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    // ------------- NIH-97 Detail-page workflow -------------

    /// <summary>
    /// Inline-edit a single checklist row (status / owner / deadline). Only
    /// non-null fields on the request are applied; explicit clears are opted
    /// in via the <c>Clear*</c> flags. Returns the refreshed tender detail
    /// or <c>null</c> when either the tender or the checklist item is
    /// missing (or does not belong to the tender).
    /// </summary>
    Task<TenderResponse?> UpdateChecklistItemAsync(int tenderId, int itemId,
        UpdateTenderChecklistItemRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Attach an uploaded file to one checklist row. File writing is handled
    /// by the controller; this method only persists metadata + flips the
    /// item to <see cref="TenderChecklistItemStatus.Done"/> when the row is
    /// still un-started/preparing. Returns the refreshed tender or null on
    /// missing tender/item.
    /// </summary>
    Task<TenderResponse?> AttachChecklistFileAsync(int tenderId, int itemId,
        string filePath, string originalFileName, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Bulk-attach one or more checklist rows to <c>capability_documents</c>
    /// library entries. The library file path is copied onto the checklist
    /// row and the row moves to <see cref="TenderChecklistItemStatus.Done"/>
    /// so users don't have to re-upload common files (Hồ sơ năng lực).
    /// Returns the refreshed tender, or null when the tender is missing.
    /// Throws <see cref="TenderOperationException"/> on validation failures
    /// (unknown item / document, or an item that doesn't belong to the
    /// tender).
    /// </summary>
    Task<TenderResponse?> AttachChecklistFromLibraryAsync(int tenderId,
        AttachTenderChecklistFromLibraryRequest request, int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Sales-Manager transition to <see cref="TenderStatus.Won"/>. Requires
    /// a linked opportunity. Sets <c>ClosedAt = now</c>. Throws when the
    /// current status is terminal or the opportunity is unknown.
    /// </summary>
    Task<TenderResponse?> MarkWonAsync(int tenderId, MarkTenderWonRequest request,
        int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Sales-Manager transition to <see cref="TenderStatus.Lost"/>. Requires
    /// a reason code from master-data category <c>opportunity_lost_reason</c>.
    /// Sets <c>ClosedAt = now</c>. Throws when the current status is terminal.
    /// </summary>
    Task<TenderResponse?> MarkLostAsync(int tenderId, MarkTenderLostRequest request,
        int callerUserId, CancellationToken ct = default);

    /// <summary>
    /// Ordered (newest-first) audit-log slice for the History tab.
    /// Returns <c>null</c> when the tender doesn't exist so the controller
    /// can 404 without leaking existence.
    /// </summary>
    Task<List<TenderTimelineEvent>?> GetTimelineAsync(int tenderId, int limit, CancellationToken ct = default);
}
