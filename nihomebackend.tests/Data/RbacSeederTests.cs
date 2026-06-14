using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

public class RbacSeederTests : IDisposable
{
    private readonly AppDbContext _db;

    public RbacSeederTests()
    {
        _db = DbContextFactory.Create();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Seed_PopulatesEntireCatalog()
    {
        RbacSeeder.Seed(_db);

        var expected = PermissionCatalog.Resolve();
        Assert.Equal(expected.Count, _db.Permissions.Count());
        foreach (var entry in expected)
        {
            Assert.Single(_db.Permissions.Where(p => p.Module == entry.Module && p.Action == entry.Action));
        }
    }

    [Fact]
    public void Seed_CreatesSystemAndBusinessRoles()
    {
        RbacSeeder.Seed(_db);

        Assert.Contains(_db.Roles, r => r.Code == SystemRoleCodes.SuperAdmin && r.IsSystem);
        Assert.Contains(_db.Roles, r => r.Code == SystemRoleCodes.Admin && r.IsSystem);
        Assert.Contains(_db.Roles, r => r.Code == SystemRoleCodes.User && r.IsSystem);

        foreach (var br in PermissionCatalog.DefaultBusinessRoles)
        {
            Assert.Single(_db.Roles.Where(r => r.Code == br.Code && !r.IsSystem));
        }
    }

    [Fact]
    public void Seed_SuperAdminAlwaysGetsEveryPermission()
    {
        RbacSeeder.Seed(_db);
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);

        Assert.Equal(_db.Permissions.Count(), _db.RolePermissions.Count(rp => rp.RoleId == sa.Id));
    }

    [Fact]
    public void Seed_DoesNotOverwriteAdminChangesAfterFirstSeed()
    {
        RbacSeeder.Seed(_db);

        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var adminPerms = _db.RolePermissions.Where(rp => rp.RoleId == admin.Id).ToList();
        Assert.NotEmpty(adminPerms);
        _db.RolePermissions.RemoveRange(adminPerms);
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Empty(_db.RolePermissions.Where(rp => rp.RoleId == admin.Id));
    }

    [Fact]
    public void Seed_SuperAdminPermissionsAreRestoredOnEveryBoot()
    {
        RbacSeeder.Seed(_db);
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        var perms = _db.RolePermissions.Where(rp => rp.RoleId == sa.Id).ToList();
        _db.RolePermissions.RemoveRange(perms);
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Equal(_db.Permissions.Count(), _db.RolePermissions.Count(rp => rp.RoleId == sa.Id));
    }

    [Fact]
    public void Seed_IsIdempotent()
    {
        RbacSeeder.Seed(_db);
        var roleCount = _db.Roles.Count();
        var permCount = _db.Permissions.Count();
        var rpCount = _db.RolePermissions.Count();

        RbacSeeder.Seed(_db);

        Assert.Equal(roleCount, _db.Roles.Count());
        Assert.Equal(permCount, _db.Permissions.Count());
        Assert.Equal(rpCount, _db.RolePermissions.Count());
    }

    [Fact]
    public void Seed_BackfillsUserRoleEntityIdFromEnum()
    {
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000001",
            FullName = "Alice",
            PasswordHash = "x",
            Role = UserRole.SUPER_ADMIN,
        });
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        var alice = _db.Users.Single(u => u.PhoneNumber == "0900000001");
        var saRoleId = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin).Id;
        Assert.Equal(saRoleId, alice.RoleEntityId);
    }

    [Fact]
    public void Seed_DoesNotOverwriteExistingUserRoleEntityId()
    {
        RbacSeeder.Seed(_db);
        var custom = _db.Roles.Single(r => r.Code == "SALE");
        _db.Users.Add(new ApplicationUser
        {
            PhoneNumber = "0900000002",
            FullName = "Bob",
            PasswordHash = "x",
            Role = UserRole.USER,
            RoleEntityId = custom.Id,
        });
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Equal(custom.Id, _db.Users.Single(u => u.PhoneNumber == "0900000002").RoleEntityId);
    }

    [Fact]
    public void Seed_PicksUpNewlyAddedCatalogEntriesOnRerun()
    {
        _db.Permissions.Add(new Permission { Module = "dashboard", Action = "view", IsActive = true });
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Equal(PermissionCatalog.Resolve().Count, _db.Permissions.Count());
    }
}
