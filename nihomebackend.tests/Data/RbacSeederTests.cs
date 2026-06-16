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

        var expected = PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog);
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

        foreach (var br in RbacSeedData.Default.BusinessRoles)
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
    public void Seed_DoesNotOverwriteBusinessRoleChangesAfterFirstSeed()
    {
        RbacSeeder.Seed(_db);

        var sale = _db.Roles.Single(r => r.Code == "SALE");
        var salePerms = _db.RolePermissions.Where(rp => rp.RoleId == sale.Id).ToList();
        Assert.NotEmpty(salePerms);
        _db.RolePermissions.RemoveRange(salePerms);
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Empty(_db.RolePermissions.Where(rp => rp.RoleId == sale.Id));
    }

    [Fact]
    public void Seed_SystemRolePermissionsAreRestoredOnEveryBoot()
    {
        RbacSeeder.Seed(_db);

        foreach (var code in new[] { SystemRoleCodes.SuperAdmin, SystemRoleCodes.Admin, SystemRoleCodes.User })
        {
            var role = _db.Roles.Single(r => r.Code == code);
            var perms = _db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToList();
            Assert.NotEmpty(perms);
            _db.RolePermissions.RemoveRange(perms);
        }
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        var allCodes = PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog).Select(e => e.Code).ToList();
        foreach (var (code, expectedCount) in new[]
        {
            (SystemRoleCodes.SuperAdmin, allCodes.Count),
            (SystemRoleCodes.Admin, ExpectedCount(SystemRoleCodes.Admin, allCodes)),
            (SystemRoleCodes.User, ExpectedCount(SystemRoleCodes.User, allCodes)),
        })
        {
            var role = _db.Roles.Single(r => r.Code == code);
            Assert.Equal(expectedCount, _db.RolePermissions.Count(rp => rp.RoleId == role.Id));
        }
    }

    [Fact]
    public void Seed_AdminPicksUpNewCatalogCodesAddedAfterInitialSeed()
    {
        RbacSeeder.Seed(_db);
        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var beforeCount = _db.RolePermissions.Count(rp => rp.RoleId == admin.Id);

        // Simulate the catalog growing between boots: a new module/action that
        // matches ADMIN's `**` pattern (and is not in its deny list) appears.
        var extended = RbacSeedData.Default;
        var expandedCatalog = extended.BaseCatalog
            .Append(new PermissionCatalog.Entry("synthetic", "manage", "rbac.perm.synthetic.manage"))
            .ToList();
        var grown = new RbacSeedData.Bundle(expandedCatalog, extended.BusinessRoles, extended.RolePermissions);

        RbacSeeder.Seed(_db, assemblies: null, seedData: grown);

        Assert.Equal(beforeCount + 1, _db.RolePermissions.Count(rp => rp.RoleId == admin.Id));
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

        Assert.Equal(PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog).Count, _db.Permissions.Count());
    }

    private static int ExpectedCount(string roleCode, IReadOnlyList<string> allCodes)
    {
        var defaults = RbacSeedData.Default.RolePermissions[roleCode];
        return PermissionCatalog.ExpandPatterns(defaults.Patterns, allCodes, defaults.Deny).Count;
    }
}
