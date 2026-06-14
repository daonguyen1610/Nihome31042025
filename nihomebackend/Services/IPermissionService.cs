using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Resolves the effective set of permission codes for a user. Source of truth
/// is the <c>role_permissions</c> join table; the resolution chain is:
///   1. If <see cref="ApplicationUser.RoleEntityId"/> is set AND the role is
///      active, use that role's permissions.
///   2. Otherwise fall back to a system role whose <c>Code</c> matches the
///      legacy <see cref="ApplicationUser.Role"/> enum (also active-only).
///   3. Otherwise return an empty set (no implicit grants — fail closed).
///
/// Inactive users always resolve to an empty set regardless of role state,
/// so this service is safe to call without separate user-active checks.
/// </summary>
public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetForUserAsync(int userId, CancellationToken ct = default);
    Task<bool> HasAsync(int userId, string permissionCode, CancellationToken ct = default);
}
