using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Services;

/// <param name="code">Stable code the client maps to a localized message, when it needs one.</param>
/// <param name="parameters">Values that message needs.</param>
public sealed class OperationalProjectOperationException(string message, string? code = null,
    IReadOnlyDictionary<string, object>? parameters = null) : Exception(message)
{
    public string? Code => code;
    public IReadOnlyDictionary<string, object>? Parameters => parameters;

    public object ToResponse() => NihomeBackend.Infrastructure.CodedErrorBody.Create(Message, Code, Parameters);
}

public interface IOperationalProjectService
{
    Task<string> SuggestCodeAsync(CancellationToken ct = default);

    Task<OperationalProjectListResponse> ListAsync(
        OperationalProjectListParams parameters,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> GetAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<IReadOnlyList<OperationalProjectTimelineItemResponse>?> GetTimelineAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse> CreateAsync(
        CreateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> UpdateAsync(
        int id,
        UpdateOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> ReopenAsync(
        int id,
        ReopenOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> RenameCodeAsync(
        int id,
        RenameOperationalProjectRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<OperationalProjectResponse?> RetryDriveRenameAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<DeletionImpactResponse?> GetDeletionImpactAsync(
        int id,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);

    Task<HardDeleteOperationResult?> DeleteAsync(
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        bool canSeeAll,
        CancellationToken ct = default);
}
