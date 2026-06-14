using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public sealed class PermissionService(AppDbContext db) : IPermissionService
{
    private static readonly FrozenSet<string> Empty =
        FrozenSet<string>.Empty.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> GetForUserAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0) return Empty;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.IsActive, u.Role, u.RoleEntityId })
            .FirstOrDefaultAsync(ct);

        if (user == null || !user.IsActive) return Empty;

        var roleId = await ResolveActiveRoleIdAsync(user.RoleEntityId, UserRoleCodeMapper.ToCode(user.Role), ct);
        if (roleId == null) return Empty;

        var codes = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId && rp.Permission.IsActive)
            .Select(rp => rp.Permission.Module + RbacConventions.CodeSeparator + rp.Permission.Action)
            .ToListAsync(ct);

        return codes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasAsync(int userId, string permissionCode, CancellationToken ct = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(permissionCode)) return false;
        var set = await GetForUserAsync(userId, ct);
        return set.Contains(permissionCode.Trim());
    }

    private async Task<int?> ResolveActiveRoleIdAsync(int? roleEntityId, string legacyRoleCode, CancellationToken ct)
    {
        if (roleEntityId.HasValue)
        {
            var isActive = await db.Roles.AsNoTracking()
                .Where(r => r.Id == roleEntityId.Value)
                .Select(r => (bool?)r.IsActive)
                .FirstOrDefaultAsync(ct);
            if (isActive == true) return roleEntityId.Value;
            // FK set but role inactive: fail closed, do NOT fall back to enum.
            if (isActive == false) return null;
            // Role row was deleted (FK SetNull would clear it normally; if for
            // some reason it lingers, treat as no role).
        }

        // Legacy fallback: enum value must match a SYSTEM role code.
        if (!SystemRoleCodes.IsSystem(legacyRoleCode)) return null;
        return await db.Roles.AsNoTracking()
            .Where(r => r.Code == legacyRoleCode && r.IsActive && r.IsSystem)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);
    }
}
