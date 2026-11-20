using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IWorkflowConfigService
{
    Task<List<WorkflowConfigResponse>> ListAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<WorkflowConfigResponse?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<WorkflowConfigResponse> CreateAsync(UpsertWorkflowConfigRequest req, CancellationToken ct = default);

    Task<WorkflowConfigResponse?> UpdateAsync(int id, UpsertWorkflowConfigRequest req, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Thrown when the (Module, Action) pair already exists on a different row.
/// </summary>
public class WorkflowConfigDuplicateException(string module, string action)
    : InvalidOperationException($"A workflow already exists for module '{module}' action '{action}'.")
{
    public string Module { get; } = module;
    public string Action { get; } = action;
}

/// <summary>
/// Thrown when the caller-supplied step list violates a config-level rule
/// (empty, duplicate order values, unknown approver role).
/// </summary>
/// <param name="code">Stable code the client maps to a localized message, when it needs one.</param>
/// <param name="parameters">Values that message needs, such as the role.</param>
public class WorkflowConfigValidationException(string message, string? code = null,
    IReadOnlyDictionary<string, object>? parameters = null) : InvalidOperationException(message)
{
    public string? Code => code;
    public IReadOnlyDictionary<string, object>? Parameters => parameters;

    public object ToResponse() => Code is null
        ? new { message = Message }
        : new { message = Message, code = Code, @params = Parameters };
}
