using System.Reflection;
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

        SeedPermissions(db, catalog);
        SeedRoles(db, bundle);
        ForceSyncSystemRolePermissions(db, catalog, bundle);
        SeedInitialBusinessRolePermissionsIfMissing(db, catalog, bundle);
        BackfillUserRoleEntityIds(db);
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
                CreatedAt = now,
            });
        }

        if (toAdd.Count == 0) return;
        db.Roles.AddRange(toAdd);
        db.SaveChanges();
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
        var roles = db.Roles
            .Where(r => !r.IsSystem && !r.InitialPermissionsSeeded)
            .ToList();

        foreach (var role in roles)
        {
            if (bundle.RolePermissions.TryGetValue(role.Code, out var defaults))
            {
                var codes = PermissionCatalog.ExpandPatterns(defaults.Patterns, allCodes, defaults.Deny);
                var rows = codes
                    .Where(permissionIdByCode.ContainsKey)
                    .Select(c => new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permissionIdByCode[c],
                        CreatedAt = DateTime.UtcNow,
                    });
                db.RolePermissions.AddRange(rows);
            }

            role.InitialPermissionsSeeded = true;
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
