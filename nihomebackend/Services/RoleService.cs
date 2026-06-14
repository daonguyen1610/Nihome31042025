using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services;

public sealed class RoleService(
    AppDbContext db,
    IPermissionService permissions,
    IAuditLogger audit,
    INotificationService notifications) : IRoleService
{
    private const string ResourceType = "rbac.role";
    private const string ActionUpdateRole = "rbac.role.update";
    private const string ActionUpdatePermissions = "rbac.role.permissions.update";

    public async Task<List<RoleResponse>> ListRolesAsync(CancellationToken ct = default)
    {
        return await db.Roles.AsNoTracking()
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Code)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                LabelKey = r.LabelKey,
                DescriptionKey = r.DescriptionKey,
                IsSystem = r.IsSystem,
                IsActive = r.IsActive,
                UserCount = db.Users.Count(u => u.RoleEntityId == r.Id),
                PermissionCount = db.RolePermissions.Count(rp => rp.RoleId == r.Id),
            })
            .ToListAsync(ct);
    }

    public async Task<RoleResponse?> GetRoleAsync(int id, CancellationToken ct = default)
    {
        return await db.Roles.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new RoleResponse
            {
                Id = r.Id,
                Code = r.Code,
                Name = r.Name,
                LabelKey = r.LabelKey,
                DescriptionKey = r.DescriptionKey,
                IsSystem = r.IsSystem,
                IsActive = r.IsActive,
                UserCount = db.Users.Count(u => u.RoleEntityId == r.Id),
                PermissionCount = db.RolePermissions.Count(rp => rp.RoleId == r.Id),
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<PermissionResponse>> ListPermissionsAsync(CancellationToken ct = default)
    {
        return await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Module).ThenBy(p => p.Action)
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Module = p.Module,
                Action = p.Action,
                Code = p.Module + RbacConventions.CodeSeparator + p.Action,
                DescriptionKey = p.DescriptionKey,
            })
            .ToListAsync(ct);
    }

    public async Task<RolePermissionsResponse?> GetRolePermissionsAsync(int id, CancellationToken ct = default)
    {
        var role = await GetRoleAsync(id, ct);
        if (role == null) return null;

        var codes = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == id && rp.Permission.IsActive)
            .Select(rp => rp.Permission.Module + RbacConventions.CodeSeparator + rp.Permission.Action)
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new RolePermissionsResponse { Role = role, Permissions = codes };
    }

    public async Task<RoleWriteResult<RoleResponse>> UpdateRoleAsync(
        int id, UpdateRoleRequest req, int actorUserId, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return RoleWriteResult<RoleResponse>.NotFound();
        if (string.Equals(role.Code, SystemRoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return RoleWriteResult<RoleResponse>.SystemRole();

        var oldSnapshot = new { role.Name, role.LabelKey, role.DescriptionKey, role.IsActive };
        var changed = false;

        if (req.Name is { } name && !string.Equals(role.Name, name, StringComparison.Ordinal))
        {
            role.Name = name.Trim();
            changed = true;
        }
        if (req.LabelKey is { } label && !string.Equals(role.LabelKey, label, StringComparison.Ordinal))
        {
            role.LabelKey = label.Trim();
            changed = true;
        }
        if (req.DescriptionKey is { } desc && !string.Equals(role.DescriptionKey, desc, StringComparison.Ordinal))
        {
            role.DescriptionKey = desc.Trim();
            changed = true;
        }
        if (req.IsActive.HasValue && req.IsActive.Value != role.IsActive)
        {
            // Block deactivating any system role — could lock out admins/users.
            if (role.IsSystem && req.IsActive.Value == false)
                return RoleWriteResult<RoleResponse>.Invalid("System roles cannot be deactivated.");
            role.IsActive = req.IsActive.Value;
            changed = true;
        }

        if (!changed)
        {
            var current = await GetRoleAsync(id, ct);
            return RoleWriteResult<RoleResponse>.Ok(current!);
        }

        role.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var response = (await GetRoleAsync(id, ct))!;

        audit.Log(new AuditEvent
        {
            Action = ActionUpdateRole,
            ResourceType = ResourceType,
            ResourceId = role.Code,
            Message = $"Updated role '{role.Code}'.",
            Status = "success",
            OldValue = oldSnapshot,
            NewValue = new { role.Name, role.LabelKey, role.DescriptionKey, role.IsActive },
        });

        await notifications.CreateForAdminsAsync(
            module: "rbac",
            title: "rbac.notification.role-updated.title",
            body: role.Code,
            linkUrl: $"/admin/roles/{role.Id}");

        return RoleWriteResult<RoleResponse>.Ok(response);
    }

    public async Task<RoleWriteResult<RolePermissionsResponse>> UpdateRolePermissionsAsync(
        int id, UpdateRolePermissionsRequest req, int actorUserId, CancellationToken ct = default)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role == null) return RoleWriteResult<RolePermissionsResponse>.NotFound();
        if (string.Equals(role.Code, SystemRoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            return RoleWriteResult<RolePermissionsResponse>.SystemRole();

        var requested = (req.Permissions ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Resolve requested codes -> permission ids; reject unknown.
        var catalog = await db.Permissions.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, Code = p.Module + RbacConventions.CodeSeparator + p.Action })
            .ToListAsync(ct);
        var idByCode = catalog.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var unknown = requested.Where(c => !idByCode.ContainsKey(c)).OrderBy(c => c).ToList();
        if (unknown.Count > 0)
            return RoleWriteResult<RolePermissionsResponse>.UnknownCodes(unknown);

        // Anti-escalation: a non-SUPER_ADMIN actor cannot grant any permission
        // they do not themselves hold. Computed against the *current* effective
        // set so changes are atomic from the actor's perspective. Note this
        // check is self-correcting for SUPER_ADMIN — their effective set is
        // the full catalog, so escalations is always empty for them.
        var actorPerms = await permissions.GetForUserAsync(actorUserId, ct);
        var existingCodes = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == id)
            .Select(rp => rp.Permission.Module + RbacConventions.CodeSeparator + rp.Permission.Action)
            .ToListAsync(ct);
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newlyGranted = requested.Where(c => !existingSet.Contains(c)).ToList();
        var escalations = newlyGranted.Where(c => !actorPerms.Contains(c)).OrderBy(c => c).ToList();
        if (escalations.Count > 0)
            return RoleWriteResult<RolePermissionsResponse>.Escalation(escalations);

        // Diff: compute added/removed for audit + change detection. Reuse the
        // existingSet computed above where possible — but we need the tracked
        // entities (with Permission nav) to actually remove rows.
        var existing = await db.RolePermissions
            .Where(rp => rp.RoleId == id)
            .Include(rp => rp.Permission)
            .ToListAsync(ct);
        var existingByCode = existing.ToDictionary(
            rp => RbacConventions.BuildCode(rp.Permission.Module, rp.Permission.Action),
            rp => rp,
            StringComparer.OrdinalIgnoreCase);

        var toRemove = existingByCode.Where(kv => !requested.Contains(kv.Key)).Select(kv => kv.Value).ToList();
        var toAdd = requested.Where(c => !existingByCode.ContainsKey(c))
            .Select(c => new RolePermission
            {
                RoleId = id,
                PermissionId = idByCode[c],
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0)
        {
            var unchanged = (await GetRolePermissionsAsync(id, ct))!;
            return RoleWriteResult<RolePermissionsResponse>.Ok(unchanged);
        }

        if (toRemove.Count > 0) db.RolePermissions.RemoveRange(toRemove);
        if (toAdd.Count > 0) db.RolePermissions.AddRange(toAdd);
        role.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var response = (await GetRolePermissionsAsync(id, ct))!;

        var added = toAdd.Select(rp => catalog.First(p => p.Id == rp.PermissionId).Code)
            .OrderBy(c => c).ToList();
        var removed = toRemove.Select(rp => RbacConventions.BuildCode(rp.Permission.Module, rp.Permission.Action))
            .OrderBy(c => c).ToList();

        audit.Log(new AuditEvent
        {
            Action = ActionUpdatePermissions,
            ResourceType = ResourceType,
            ResourceId = role.Code,
            Message = $"Updated permissions for role '{role.Code}' (+{added.Count} / -{removed.Count}).",
            Status = "success",
            OldValue = new { permissions = existingByCode.Keys.OrderBy(c => c).ToList() },
            NewValue = new { permissions = response.Permissions },
            Metadata = new { added, removed },
        });

        await notifications.CreateForAdminsAsync(
            module: "rbac",
            title: "rbac.notification.role-permissions-updated.title",
            body: role.Code,
            linkUrl: $"/admin/roles/{role.Id}");

        return RoleWriteResult<RolePermissionsResponse>.Ok(response);
    }
}
