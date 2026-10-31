using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

/// <summary>
/// Idempotent RBAC bootstrap. Runs on every startup and:
///   1. Upserts the effective permission catalog (JSON base ∪ discovered
///      [RequirePermission] attributes) into the <c>permissions</c> table.
///   2. Ensures the 3 system roles + business roles from the JSON seed exist.
///      New roles can ONLY be introduced by editing the JSON seed — there is
///      no API to create roles.
///   3. Force-syncs all SYSTEM roles (SUPER_ADMIN/ADMIN/USER) to the catalog
///      every boot. System roles are deterministic by design — admins cannot
///      customise them and new catalog codes must propagate automatically.
///   4. Seeds the initial permission set for each business role exactly once
///      (tracked by <c>Role.InitialPermissionsSeeded</c>). Subsequent admin
///      edits in the matrix editor are preserved on restart.
///   5. Backfills <c>users.RoleEntityId</c> for any user whose enum role maps
///      to an existing system role row.
/// All seed defaults (base catalog, business roles, role × permission
/// patterns) come from <c>Data/Rbac/rbac-defaults.json</c> via
/// <see cref="RbacSeedData"/>.
/// </summary>
public static class RbacSeeder
{
    public static void Seed(AppDbContext db) => Seed(db, assemblies: null, seedData: null);

    /// <summary>
    /// Test hook: pass custom assemblies (for [RequirePermission] discovery)
    /// and/or a custom <see cref="RbacSeedData.Bundle"/> so tests can isolate
    /// from the shipped defaults.
    /// </summary>
    public static void Seed(AppDbContext db, IEnumerable<Assembly>? assemblies, RbacSeedData.Bundle? seedData = null)
    {
        var bundle = seedData ?? RbacSeedData.Default;
        var discovered = PermissionDiscovery.Discover(assemblies);
        var catalog = PermissionCatalog.Resolve(bundle.BaseCatalog, discovered);
        var newLegalTemplateActions = catalog.Where(entry => entry.Module == "permit.templates")
            .Select(entry => entry.Action).Except(db.Permissions.Where(permission => permission.Module == "permit.templates")
                .Select(permission => permission.Action)).ToHashSet();

        SeedPermissions(db, catalog);
        SeedRoleGroups(db, bundle);
        SeedInitialRoleGroupPermissionsIfMissing(db, catalog, bundle);
        SeedRoles(db, bundle);
        SeedInitialBusinessRoleGroupsIfMissing(db, bundle);
        ForceSyncSystemRolePermissions(db, catalog, bundle);
        SeedInitialBusinessRolePermissionsIfMissing(db, catalog, bundle);
        RemoveAccidentalDesignLeadDeletionGrant(db);
        SeedNewLegalTemplatePermissions(db, newLegalTemplateActions);
        BackfillUserRoleEntityIds(db);
    }

    private static void RemoveAccidentalDesignLeadDeletionGrant(AppDbContext db)
    {
        // Earlier design.** defaults granted this destructive capability to
        // existing Design Lead roles. Correct that specific unsafe default;
        // preserve every other grant and explicitly configured custom role.
        var grants = db.RolePermissions.Where(item => item.Role.Code == "DESIGN_LEAD" &&
            item.Permission.Module == "design.records" && item.Permission.Action == "hard-delete").ToList();
        if (grants.Count == 0) return;
        db.RolePermissions.RemoveRange(grants);
        db.SaveChanges();
    }

    private static void SeedNewLegalTemplatePermissions(AppDbContext db, HashSet<string> newActions)
    {
        // Introduce only newly discovered permissions once. Later administrator revocations stay revoked.
        if (newActions.Count == 0) return;
        var permissions = db.Permissions.Where(x => x.Module == "permit.templates").ToList();
        foreach (var role in db.Roles.Where(x => x.Code == "PM" || x.Code == "LEGAL_OFFICER").ToList())
        {
            var actions = role.Code == "PM" ? new[] { "view", "publish" } : new[] { "view", "manage" };
            foreach (var permission in permissions.Where(x => newActions.Contains(x.Action) && actions.Contains(x.Action)))
                if (!db.RolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, CreatedAt = DateTime.UtcNow });
        }
        db.SaveChanges();
    }

    private static void SeedRoleGroups(AppDbContext db, RbacSeedData.Bundle bundle)
    {
        var existing = db.RoleGroups.ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var definition in bundle.RoleGroups)
        {
            if (existing.TryGetValue(definition.Code, out var group))
            {
                // Runtime edits to a department's label and order are authoritative.
                // Seeded groups remain active because they are core RBAC defaults.
                group.IsActive = true;
                continue;
            }

            db.RoleGroups.Add(new RoleGroup
            {
                Code = definition.Code,
                LabelKey = definition.LabelKey,
                SortOrder = definition.SortOrder,
                IsActive = true,
                CreatedAt = now,
            });
        }

        db.SaveChanges();
    }

    private static void SeedInitialRoleGroupPermissionsIfMissing(
        AppDbContext db,
        IReadOnlyList<PermissionCatalog.Entry> catalog,
        RbacSeedData.Bundle bundle)
    {
        var permissionIdByCode = db.Permissions.ToDictionary(
            permission => RbacConventions.BuildCode(permission.Module, permission.Action),
            permission => permission.Id,
            StringComparer.OrdinalIgnoreCase);
        var allCodes = catalog.Select(entry => entry.Code).ToList();
        var defaultsByCode = bundle.RoleGroups.ToDictionary(
            group => group.Code,
            StringComparer.OrdinalIgnoreCase);
        var groups = db.RoleGroups
            .Where(group => group.IsActive && !group.InitialPermissionsSeeded)
            .Include(group => group.BaselinePermissions)
            .ToList();
        var now = DateTime.UtcNow;

        foreach (var group in groups)
        {
            // A populated legacy/runtime baseline is an administrator choice.
            // Mark it initialized without adding newly shipped defaults.
            if (group.BaselinePermissions.Count == 0 &&
                defaultsByCode.TryGetValue(group.Code, out var defaults))
            {
                var codes = PermissionCatalog.ExpandPatterns(defaults.Patterns, allCodes);
                group.BaselinePermissions = codes
                    .Where(permissionIdByCode.ContainsKey)
                    .Select(code => new RoleGroupPermission
                    {
                        RoleGroupId = group.Id,
                        PermissionId = permissionIdByCode[code],
                        CreatedAt = now,
                    })
                    .ToList();
            }

            group.InitialPermissionsSeeded = true;
            group.UpdatedAt = now;
        }

        if (groups.Count > 0) db.SaveChanges();
    }

    private static void SeedPermissions(AppDbContext db, IReadOnlyList<PermissionCatalog.Entry> catalog)
    {
        var existing = db.Permissions
            .Select(p => new { p.Module, p.Action })
            .ToHashSet();

        var toInsert = catalog
            .Where(e => !existing.Contains(new { e.Module, e.Action }))
            .Select(e => new Permission
            {
                Module = e.Module,
                Action = e.Action,
                DescriptionKey = e.DescriptionKey,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        if (toInsert.Count == 0) return;

        db.Permissions.AddRange(toInsert);
        db.SaveChanges();
    }

    private static void SeedRoles(AppDbContext db, RbacSeedData.Bundle bundle)
    {
        var existingCodes = db.Roles.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groupIdByCode = db.RoleGroups.ToDictionary(g => g.Code, g => g.Id, StringComparer.OrdinalIgnoreCase);
        var unknownGroupCode = bundle.BusinessRoles
            .Select(role => role.GroupCode)
            .FirstOrDefault(code => code != null && !groupIdByCode.ContainsKey(code));
        if (unknownGroupCode != null)
            throw new InvalidOperationException($"RBAC defaults reference unknown group '{unknownGroupCode}'.");
        var now = DateTime.UtcNow;
        var toAdd = new List<Role>();

        foreach (var code in SystemRoleCodes.All)
        {
            if (existingCodes.Contains(code)) continue;
            toAdd.Add(new Role
            {
                Code = code,
                Name = code,
                LabelKey = $"rbac.role.{code}.label",
                DescriptionKey = $"rbac.role.{code}.description",
                IsSystem = true,
                IsActive = true,
                InitialGroupSeeded = true,
                CreatedAt = now,
            });
        }

        foreach (var br in bundle.BusinessRoles)
        {
            if (existingCodes.Contains(br.Code)) continue;
            toAdd.Add(new Role
            {
                Code = br.Code,
                Name = br.Name,
                LabelKey = br.LabelKey,
                DescriptionKey = br.DescriptionKey,
                IsSystem = false,
                IsActive = true,
                RoleGroupId = br.GroupCode != null && groupIdByCode.TryGetValue(br.GroupCode, out var groupId)
                    ? groupId
                    : null,
                InitialGroupSeeded = true,
                CreatedAt = now,
            });
        }

        if (toAdd.Count == 0) return;
        db.Roles.AddRange(toAdd);
        db.SaveChanges();
    }

    private static void SeedInitialBusinessRoleGroupsIfMissing(AppDbContext db, RbacSeedData.Bundle bundle)
    {
        var defaultsByRoleCode = bundle.BusinessRoles.ToDictionary(
            role => role.Code,
            role => role.GroupCode,
            StringComparer.OrdinalIgnoreCase);
        var groupIdByCode = db.RoleGroups.ToDictionary(
            group => group.Code,
            group => group.Id,
            StringComparer.OrdinalIgnoreCase);
        var roles = db.Roles
            .Where(role => !role.IsSystem && !role.InitialGroupSeeded)
            .ToList();

        foreach (var role in roles)
        {
            if (defaultsByRoleCode.TryGetValue(role.Code, out var groupCode) && groupCode != null)
            {
                if (!groupIdByCode.TryGetValue(groupCode, out var groupId))
                    throw new InvalidOperationException(
                        $"RBAC role '{role.Code}' references unknown group '{groupCode}'.");
                role.RoleGroupId = groupId;
            }

            role.InitialGroupSeeded = true;
        }

        if (roles.Count > 0) db.SaveChanges();
    }

    private static void ForceSyncSystemRolePermissions(
        AppDbContext db,
        IReadOnlyList<PermissionCatalog.Entry> catalog,
        RbacSeedData.Bundle bundle)
    {
        var permissionIdByCode = db.Permissions.ToDictionary(
            p => RbacConventions.BuildCode(p.Module, p.Action),
            p => p.Id,
            StringComparer.OrdinalIgnoreCase);
        var allCodes = catalog.Select(e => e.Code).ToList();

        var systemRoles = db.Roles
            .Where(r => r.IsSystem)
            .ToList();

        foreach (var role in systemRoles)
        {
            // SUPER_ADMIN bypasses pattern expansion — always gets the full
            // catalog so an empty/missing pattern entry can never lock it out.
            HashSet<int> desiredIds;
            if (string.Equals(role.Code, SystemRoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                desiredIds = permissionIdByCode.Values.ToHashSet();
            }
            else if (bundle.RolePermissions.TryGetValue(role.Code, out var defaults))
            {
                var codes = PermissionCatalog.ExpandPatterns(defaults.Patterns, allCodes, defaults.Deny);
                desiredIds = codes
                    .Where(permissionIdByCode.ContainsKey)
                    .Select(c => permissionIdByCode[c])
                    .ToHashSet();
            }
            else
            {
                continue;
            }

            var currentIds = db.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToHashSet();

            var toAdd = desiredIds.Except(currentIds)
                .Select(id => new RolePermission { RoleId = role.Id, PermissionId = id, CreatedAt = DateTime.UtcNow });
            var toRemove = db.RolePermissions
                .Where(rp => rp.RoleId == role.Id && !desiredIds.Contains(rp.PermissionId))
                .ToList();

            db.RolePermissions.AddRange(toAdd);
            if (toRemove.Count > 0) db.RolePermissions.RemoveRange(toRemove);

            // System roles are deterministic: mark seeded so the business-role
            // initial-seed pass below skips them (it filters by IsSystem too,
            // but this keeps the flag truthful for any tooling that reads it).
            role.InitialPermissionsSeeded = true;
        }

        db.SaveChanges();
    }

    private static void SeedInitialBusinessRolePermissionsIfMissing(
        AppDbContext db,
        IReadOnlyList<PermissionCatalog.Entry> catalog,
        RbacSeedData.Bundle bundle)
    {
        var permissionIdByCode = db.Permissions
            .ToDictionary(p => RbacConventions.BuildCode(p.Module, p.Action), p => p.Id, StringComparer.OrdinalIgnoreCase);
        var allCodes = catalog.Select(e => e.Code).ToList();

        // System roles are handled by ForceSyncSystemRolePermissions.
        var businessRoles = db.Roles.Where(r => !r.IsSystem).ToList();

        foreach (var role in businessRoles)
        {
            if (!bundle.RolePermissions.TryGetValue(role.Code, out var defaults)) continue;

            var declaredCodes = PermissionCatalog
                .ExpandPatterns(defaults.Patterns, allCodes, defaults.Deny)
                .Where(permissionIdByCode.ContainsKey)
                .ToList();

            if (!role.InitialPermissionsSeeded)
            {
                // A role can already have part of its default matrix when an
                // earlier seed was interrupted. Add only the missing pairs so
                // the unique (RoleId, PermissionId) index is never violated.
                var existingIds = db.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToHashSet();
                var rows = declaredCodes
                    .Select(c => permissionIdByCode[c])
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = id,
                        CreatedAt = DateTime.UtcNow,
                    });
                db.RolePermissions.AddRange(rows);
                role.InitialPermissionsSeeded = true;
            }
            else
            {
                // Grow-only reconciliation: when a new permission is added to
                // the catalog AFTER the role was first seeded, a role whose
                // declared patterns match the new code stops receiving it
                // (admins would have to grant it by hand for every environment).
                // Add-only avoids silently un-doing admin overrides while
                // still letting new-catalog additions flow through.
                var existingIds = db.RolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToHashSet();
                var toAdd = declaredCodes
                    // Legal template grants are introduced once by SeedNewLegalTemplatePermissions.
                    // Do not re-grant them after an administrator explicitly revokes them.
                    .Where(code => !code.StartsWith("permit.templates.", StringComparison.OrdinalIgnoreCase))
                    .Select(c => permissionIdByCode[c])
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = id,
                        CreatedAt = DateTime.UtcNow,
                    })
                    .ToList();
                if (toAdd.Count > 0) db.RolePermissions.AddRange(toAdd);
            }
        }

        db.SaveChanges();
    }

    private static void BackfillUserRoleEntityIds(AppDbContext db)
    {
        var roleIdByCode = db.Roles
            .Where(r => r.IsSystem)
            .ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        var users = db.Users.Where(u => u.RoleEntityId == null).ToList();
        if (users.Count == 0) return;

        foreach (var u in users)
        {
            if (roleIdByCode.TryGetValue(UserRoleCodeMapper.ToCode(u.Role), out var id))
            {
                u.RoleEntityId = id;
            }
        }

        db.SaveChanges();
    }
}
