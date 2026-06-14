using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Data;

/// <summary>
/// Idempotent RBAC bootstrap. Runs on every startup and:
///   1. Upserts the canonical <see cref="PermissionCatalog"/> into the
///      <c>permissions</c> table (codes added later still get inserted).
///   2. Ensures the 3 system roles + default business roles exist. New roles
///      can ONLY be introduced here — there is no API to create roles.
///   3. Force-syncs <c>SUPER_ADMIN</c> to the full permission catalog every
///      boot. This is a lockout safety net so the role can never be stripped
///      of access.
///   4. Seeds the initial permission set for any OTHER role (system or
///      business) that currently has zero permissions. Once a role has at
///      least one permission row, admins are the source of truth and the
///      seeder does not touch it again.
///   5. Backfills <c>users.RoleEntityId</c> for any user whose enum role maps
///      to an existing system role row.
/// </summary>
public static class RbacSeeder
{
    /// <summary>Seeds RBAC using the default discovery assembly (the backend itself).</summary>
    public static void Seed(AppDbContext db) => Seed(db, assemblies: null);

    /// <summary>
    /// Seeds RBAC using a caller-supplied set of assemblies to scan for
    /// <see cref="Authorization.RequirePermissionAttribute"/>. Tests pass
    /// their own assembly so they can verify auto-discovery without polluting
    /// the runtime catalog.
    /// </summary>
    public static void Seed(AppDbContext db, IEnumerable<Assembly>? assemblies)
    {
        var discovered = PermissionDiscovery.Discover(assemblies);
        var catalog = PermissionCatalog.Resolve(discovered);

        SeedPermissions(db, catalog);
        SeedRoles(db);
        ForceSyncSuperAdminPermissions(db);
        SeedInitialRolePermissionsIfMissing(db, catalog);
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

    private static void SeedRoles(AppDbContext db)
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

        foreach (var br in PermissionCatalog.DefaultBusinessRoles)
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

    private static void ForceSyncSuperAdminPermissions(AppDbContext db)
    {
        var role = db.Roles.FirstOrDefault(r => r.Code == SystemRoleCodes.SuperAdmin);
        if (role == null) return;

        var allPermissionIds = db.Permissions.Select(p => p.Id).ToHashSet();
        var current = db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToList();
        var currentIds = current.Select(rp => rp.PermissionId).ToHashSet();

        var toAdd = allPermissionIds
            .Where(id => !currentIds.Contains(id))
            .Select(id => new RolePermission { RoleId = role.Id, PermissionId = id, CreatedAt = DateTime.UtcNow });
        db.RolePermissions.AddRange(toAdd);
        db.SaveChanges();
    }

    private static void SeedInitialRolePermissionsIfMissing(AppDbContext db, IReadOnlyList<PermissionCatalog.Entry> catalog)
    {
        var permissionIdByCode = db.Permissions
            .ToDictionary(p => p.Module + "." + p.Action, p => p.Id, StringComparer.OrdinalIgnoreCase);
        var allCodes = catalog.Select(e => e.Code).ToList();

        // Skip SUPER_ADMIN — handled by ForceSyncSuperAdminPermissions.
        // Once InitialPermissionsSeeded is true, admin edits (including
        // emptying the role) are preserved forever.
        var roles = db.Roles
            .Where(r => r.Code != SystemRoleCodes.SuperAdmin && !r.InitialPermissionsSeeded)
            .ToList();

        foreach (var role in roles)
        {
            var codes = PermissionCatalog.ExpandPatternsFor(role.Code, allCodes);
            var rows = codes
                .Where(permissionIdByCode.ContainsKey)
                .Select(c => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionIdByCode[c],
                    CreatedAt = DateTime.UtcNow,
                });
            db.RolePermissions.AddRange(rows);

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
            if (roleIdByCode.TryGetValue(u.Role.ToString(), out var id))
            {
                u.RoleEntityId = id;
            }
        }

        db.SaveChanges();
    }
}
