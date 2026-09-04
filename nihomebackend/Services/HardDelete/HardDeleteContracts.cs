using NihomeBackend.Models;

namespace NihomeBackend.Services.HardDelete;

public sealed record HardDeleteItemDefinition(
    HardDeleteItemKind Kind,
    string ActionIdentifier,
    int Sequence,
    IReadOnlyDictionary<string, string>? ExpectedAppProperties = null,
    string? ExpectedParentId = null);

public sealed record CreateHardDeleteOperationRequest(
    string ResourceType,
    string ResourceId,
    string ResourceLabel,
    string PlanToken,
    string Confirmation,
    string RequestedBy,
    IReadOnlyList<HardDeleteItemDefinition> Items);

public sealed record HardDeleteOperationResult(
    Guid OperationId,
    HardDeleteOperationStatus Status,
    bool IsComplete,
    bool RequiresManualAction,
    string? ErrorCode,
    string? ErrorMessage);

public class HardDeleteOperationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class HardDeleteOperationConflictException(string message)
    : HardDeleteOperationException("active_operation_exists", message);

public sealed class HardDeleteAuthorizationException(string message)
    : HardDeleteOperationException("hard_delete_authorization_changed", message);

public interface IHardDeleteOperationService
{
    Task<HardDeleteOperationResult> CreateAsync(
        CreateHardDeleteOperationRequest request, CancellationToken ct = default);
    Task<HardDeleteOperationResult> ProcessAsync(
        Guid operationId, CancellationToken ct = default, string? requestedBy = null);
    Task<HardDeleteOperationResult?> GetAsync(
        Guid operationId, CancellationToken ct = default, string? requestedBy = null);
}

public sealed record HardDeleteResourceContext(
    Guid OperationId,
    string ResourceType,
    string ResourceId,
    string PlanToken,
    string RequestedBy,
    string ActionIdentifier,
    bool IsForwardRecovery);

public interface IHardDeleteResourceHandler
{
    string ResourceType { get; }
    Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default) =>
        Task.CompletedTask;
    Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default);
}

public interface IHardDeleteResourceHandlerRegistry
{
    IHardDeleteResourceHandler? Find(string resourceType);
}

public sealed class HardDeleteResourceHandlerRegistry(
    IEnumerable<IHardDeleteResourceHandler> handlers) : IHardDeleteResourceHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IHardDeleteResourceHandler> handlersByResource = handlers
        .GroupBy(handler => handler.ResourceType, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

    public IHardDeleteResourceHandler? Find(string resourceType) =>
        handlersByResource.GetValueOrDefault(resourceType);
}

internal static class HardDeleteStateMachine
{
    public static void Transition(HardDeleteOperation operation, HardDeleteOperationStatus next, DateTime now)
    {
        if (!CanTransition(operation.Status, next))
            throw new HardDeleteOperationException(
                "invalid_operation_transition",
                $"Không thể chuyển tác vụ xóa từ {operation.Status} sang {next}.");
        operation.Status = next;
        operation.UpdatedAt = now;
        if (next == HardDeleteOperationStatus.Processing) operation.StartedAt ??= now;
        if (next == HardDeleteOperationStatus.Completed) operation.CompletedAt = now;
    }

    internal static bool CanTransition(HardDeleteOperationStatus current, HardDeleteOperationStatus next) =>
        (current, next) switch
        {
            (HardDeleteOperationStatus.Preparing, HardDeleteOperationStatus.Ready or HardDeleteOperationStatus.Failed) => true,
            (HardDeleteOperationStatus.Ready, HardDeleteOperationStatus.Processing or
                HardDeleteOperationStatus.ManualActionRequired) => true,
            (HardDeleteOperationStatus.Processing, HardDeleteOperationStatus.Completed or
                HardDeleteOperationStatus.ManualActionRequired or HardDeleteOperationStatus.Failed) => true,
            (HardDeleteOperationStatus.Failed, HardDeleteOperationStatus.Processing or
                HardDeleteOperationStatus.ManualActionRequired) => true,
            (HardDeleteOperationStatus.ManualActionRequired, HardDeleteOperationStatus.Processing) => true,
            _ => false,
        };
}