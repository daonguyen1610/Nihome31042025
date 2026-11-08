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

    private sealed record RoleState(int Id, string Code, bool IsActive, bool IsSystem);

    private static int? ResolveActiveRoleId(int? assignedId, string legacyCode, IReadOnlyCollection<RoleState> roles)
    {
        var assigned = roles.FirstOrDefault(role => role.Id == assignedId);
        if (assigned is not null) return assigned.IsActive ? assigned.Id : null;
        if (!SystemRoleCodes.IsSystem(legacyCode)) return null;
        return roles.FirstOrDefault(role => role.Code == legacyCode && role.IsActive && role.IsSystem)?.Id;
    }

    private async Task<int?> ResolveActiveRoleIdAsync(int? roleEntityId, string legacyRoleCode, CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking()
            .Where(role => role.Id == roleEntityId || role.Code == legacyRoleCode)
            .Select(role => new RoleState(role.Id, role.Code, role.IsActive, role.IsSystem)).ToListAsync(ct);
        return ResolveActiveRoleId(roleEntityId, legacyRoleCode, roles);
    }

    public async Task<IReadOnlySet<int>> FilterByRolePermissionAsync(IReadOnlyCollection<int> userIds,
        string permissionCode, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id) && user.IsActive)
            .Select(user => new { user.Id, user.RoleEntityId, user.Role }).ToListAsync(ct);
        var roles = await db.Roles.AsNoTracking()
            .Select(role => new RoleState(role.Id, role.Code, role.IsActive, role.IsSystem)).ToListAsync(ct);
        var permittedRoles = await db.RolePermissions.AsNoTracking()
            .Where(grant => grant.Permission.IsActive && grant.Permission.Module + RbacConventions.CodeSeparator + grant.Permission.Action == permissionCode)
            .Select(grant => grant.RoleId).ToListAsync(ct);
        return users.Where(user => ResolveActiveRoleId(user.RoleEntityId, UserRoleCodeMapper.ToCode(user.Role), roles) is int roleId && permittedRoles.Contains(roleId))
            .Select(user => user.Id).ToHashSet();
    }
}
