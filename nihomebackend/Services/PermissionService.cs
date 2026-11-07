using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

public sealed class PermissionService(AppDbContext db) : IPermissionService
{
    private const string OperationalProjectViewCode = "operations.projects.view";

    /// <summary>
    /// Route-level <c>[RequirePermission]</c> runs before the membership check
    /// inside each design endpoint, so a project member whose role lacks the
    /// design view codes would get 403 even though the endpoint would admit
    /// them.
    /// </summary>
    private static readonly string[] DesignViewCodes =
    [
        "design.projects.view",
        "design.concepts.view",
        "design.basic.view",
        "design.shop.view",
        "design.revisions.view",
        "design.ifc.view",
    ];

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

        var granted = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        await AddMembershipPermissionsAsync(user.Id, granted, ct);
        return granted.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Project membership grants access to that project's module even when the
    /// user's role does not carry the module's codes (the endpoint still checks
    /// the specific project before acting). Approvals, finalize and IFC release
    /// stay role-based.
    /// </summary>
    private async Task AddMembershipPermissionsAsync(
        int userId, HashSet<string> granted, CancellationToken ct)
    {
        if (!granted.Contains(OperationalProjectViewCode) &&
            await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
                project.ProjectManagerUserId == userId ||
                project.CreatedByUserId == userId ||
                project.TeamMembers.Any(member => member.UserId == userId && member.EndedAt == null) ||
                project.ScheduleRevisions.Any(revision =>
                    revision.Status == ProjectScheduleRevisionStatus.PendingApproval &&
                    revision.ApproverUserId == userId), ct))
        {
            granted.Add(OperationalProjectViewCode);
        }

        if (!DesignViewCodes.All(granted.Contains) && await CanViewAnyDesignProjectAsync(userId, ct))
        {
            foreach (var code in DesignViewCodes) granted.Add(code);
        }
    }

    private Task<bool> CanViewAnyDesignProjectAsync(int userId, CancellationToken ct) =>
        db.DesignProjects.AsNoTracking().AnyAsync(project =>
            project.ProjectManagerUserId == userId ||
            project.DesignLeadUserId == userId ||
            project.OperationalProject != null &&
            (project.OperationalProject.ProjectManagerUserId == userId ||
             project.OperationalProject.CreatedByUserId == userId ||
             project.OperationalProject.TeamMembers.Any(member =>
                 member.UserId == userId && member.EndedAt == null &&
                 (member.DesignProjectId == project.Id ||
                  member.DesignProjectId == null &&
                  member.Roles.Any(role => role.EndedAt == null &&
                      role.Scope == ProjectRoleScope.Project)))), ct);

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
