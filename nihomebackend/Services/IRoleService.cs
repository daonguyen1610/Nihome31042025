using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Manages runtime-editable RBAC roles and their permission assignments.
/// System roles (SUPER_ADMIN, ADMIN, USER) are seeder-controlled and immune
/// to Create/Delete/matrix-edit through this service. Non-SUPER_ADMIN actors
/// cannot grant permissions they do not themselves hold (anti-escalation).
/// </summary>
public interface IRoleService
{
    Task<List<RoleResponse>> ListRolesAsync(CancellationToken ct = default);
    Task<RoleResponse?> GetRoleAsync(int id, CancellationToken ct = default);
    Task<List<PermissionResponse>> ListPermissionsAsync(CancellationToken ct = default);
    Task<RolePermissionsResponse?> GetRolePermissionsAsync(int id, CancellationToken ct = default);

    Task<RoleWriteResult<RoleResponse>> CreateRoleAsync(
        CreateRoleRequest req, int actorUserId, CancellationToken ct = default);

    Task<RoleWriteResult<RoleResponse>> UpdateRoleAsync(
        int id, UpdateRoleRequest req, int actorUserId, CancellationToken ct = default);

    Task<RoleWriteResult<RolePermissionsResponse>> UpdateRolePermissionsAsync(
        int id, UpdateRolePermissionsRequest req, int actorUserId, CancellationToken ct = default);

    Task<RoleWriteResult<RoleResponse>> DeleteRoleAsync(
        int id, int actorUserId, CancellationToken ct = default);
}

public enum RoleWriteStatus
{
    Success,
    NotFound,
    ForbiddenSystemRole,
    ForbiddenEscalation,
    InvalidPermissionCodes,
    InvalidRequest,
    Conflict,
    InUse,
}

public sealed record RoleWriteResult<T>(
    RoleWriteStatus Status,
    T? Value = null,
    IReadOnlyList<string>? OffendingCodes = null,
    string? Error = null,
    int? UserCount = null) where T : class
{
    public static RoleWriteResult<T> Ok(T value) => new(RoleWriteStatus.Success, value);
    public static RoleWriteResult<T> NotFound() => new(RoleWriteStatus.NotFound);
    public static RoleWriteResult<T> SystemRole() => new(RoleWriteStatus.ForbiddenSystemRole);
    public static RoleWriteResult<T> Escalation(IReadOnlyList<string> codes) =>
        new(RoleWriteStatus.ForbiddenEscalation, OffendingCodes: codes);
    public static RoleWriteResult<T> UnknownCodes(IReadOnlyList<string> codes) =>
        new(RoleWriteStatus.InvalidPermissionCodes, OffendingCodes: codes);
    public static RoleWriteResult<T> Invalid(string error) =>
        new(RoleWriteStatus.InvalidRequest, Error: error);
    public static RoleWriteResult<T> Conflict(string error) =>
        new(RoleWriteStatus.Conflict, Error: error);
    public static RoleWriteResult<T> InUseBy(int userCount) =>
        new(RoleWriteStatus.InUse, UserCount: userCount);
}
