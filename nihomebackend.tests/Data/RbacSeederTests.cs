using Microsoft.EntityFrameworkCore;
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
    public void PermanentDesignDeletion_ExcludesOrdinaryDefaultsAndCorrectsExistingLeadGrant()
    {
        RbacSeeder.Seed(_db);
        var permission = _db.Permissions.Single(item => item.Module == "design.records" && item.Action == "hard-delete");
        var lead = _db.Roles.Single(item => item.Code == "DESIGN_LEAD");
        Assert.DoesNotContain(_db.RolePermissions, item => item.PermissionId == permission.Id &&
            (item.Role.Code == "DESIGN_LEAD" || item.Role.Code == "ADMIN"));
        foreach (var code in new[] { "BGD", "SUPER_ADMIN" })
            Assert.Contains(_db.RolePermissions, item => item.PermissionId == permission.Id && item.Role.Code == code);

        var custom = new Role { Code = "NICON_DIRECTOR", Name = "NICON director", IsActive = true };
        _db.Add(custom);
        _db.RolePermissions.AddRange(
            new RolePermission { RoleId = lead.Id, PermissionId = permission.Id },
            new RolePermission { Role = custom, PermissionId = permission.Id });
        _db.SaveChanges();
        RbacSeeder.Seed(_db);
        RbacSeeder.Seed(_db);

        Assert.DoesNotContain(_db.RolePermissions, item => item.RoleId == lead.Id && item.PermissionId == permission.Id);
        Assert.Contains(_db.RolePermissions, item => item.RoleId == custom.Id && item.PermissionId == permission.Id);
        Assert.Contains(_db.RolePermissions, item => item.RoleId == lead.Id && item.Permission.Module == "design.projects" &&
            item.Permission.Action == "manage");
    }

    [Fact]
    public void NewLegalTemplatePermissionsBackfillExistingRolesOnceAndPreserveRevocation()
    {
        RbacSeeder.Seed(_db);
        var permissionIds = _db.Permissions.Where(x => x.Module == "permit.templates").Select(x => x.Id).ToList();
        _db.RolePermissions.RemoveRange(_db.RolePermissions.Where(x => permissionIds.Contains(x.PermissionId)));
        _db.RoleGroupPermissions.RemoveRange(_db.RoleGroupPermissions.Where(x => permissionIds.Contains(x.PermissionId)));
        _db.Permissions.RemoveRange(_db.Permissions.Where(x => permissionIds.Contains(x.Id)));
        _db.SaveChanges();
        RbacSeeder.Seed(_db);
        var pm = _db.Roles.Single(x => x.Code == "PM");
        var legal = _db.Roles.Single(x => x.Code == "LEGAL_OFFICER");
        var publish = _db.Permissions.Single(x => x.Module == "permit.templates" && x.Action == "publish");
        var manage = _db.Permissions.Single(x => x.Module == "permit.templates" && x.Action == "manage");
        Assert.Contains(_db.RolePermissions, x => x.RoleId == pm.Id && x.PermissionId == publish.Id);
        Assert.Contains(_db.RolePermissions, x => x.RoleId == legal.Id && x.PermissionId == manage.Id);
        Assert.DoesNotContain(_db.RolePermissions, x => x.RoleId == legal.Id && x.PermissionId == publish.Id);
        _db.RolePermissions.Remove(_db.RolePermissions.Single(x => x.RoleId == pm.Id && x.PermissionId == publish.Id));
        _db.SaveChanges();
        RbacSeeder.Seed(_db);
        Assert.DoesNotContain(_db.RolePermissions, x => x.RoleId == pm.Id && x.PermissionId == publish.Id);
    }

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
    public void Seed_AssignsBusinessRolesToConfiguredNiconGroups()
    {
        RbacSeeder.Seed(_db);
        var groupByRole = _db.Roles.Include(role => role.RoleGroup)
            .Where(role => !role.IsSystem)
            .ToDictionary(role => role.Code, role => role.RoleGroup == null ? null : role.RoleGroup.Code);

        Assert.Equal("CRM", groupByRole["SALE"]);
        Assert.Equal("CONSTRUCTION", groupByRole["LEGAL_OFFICER"]);
        Assert.Equal("DESIGN", groupByRole["DESIGN_LEAD"]);
        Assert.Equal("PROCUREMENT", groupByRole["WAREHOUSE"]);
        Assert.Equal("PROCUREMENT", groupByRole["PROCUREMENT"]);
        Assert.Equal("FINANCE", groupByRole["ACCOUNTANT"]);
        Assert.Equal("HR", groupByRole["HR"]);
        Assert.Null(groupByRole["BGD"]);
        Assert.All(_db.Roles.Where(role => !role.IsSystem), role => Assert.True(role.InitialGroupSeeded));
    }

    [Fact]
    public void Seed_HrGroupAndRoleDoNotInheritFinancePermissions()
    {
        RbacSeeder.Seed(_db);

        var hrGroup = _db.RoleGroups.Single(group => group.Code == "HR");
        var hrRole = _db.Roles.Single(role => role.Code == "HR");
        var groupCodes = _db.RoleGroupPermissions
            .Where(item => item.RoleGroupId == hrGroup.Id)
            .Select(item => item.Permission.Module + "." + item.Permission.Action)
            .ToList();
        var roleCodes = _db.RolePermissions
            .Where(item => item.RoleId == hrRole.Id)
            .Select(item => item.Permission.Module + "." + item.Permission.Action)
            .ToList();

        Assert.Contains("recruitment.applications.view", groupCodes);
        Assert.Contains("recruitment.applications.manage", roleCodes);
        Assert.DoesNotContain(groupCodes, code => code.StartsWith("finance."));
        Assert.DoesNotContain(roleCodes, code => code.StartsWith("finance."));
    }

    [Fact]
    public void Seed_PreservesAdministratorRoleGroupChangesAfterInitialAssignment()
    {
        RbacSeeder.Seed(_db);
        var sale = _db.Roles.Single(role => role.Code == "SALE");
        sale.RoleGroupId = null;
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Null(_db.Roles.Single(role => role.Code == "SALE").RoleGroupId);
        Assert.True(sale.InitialGroupSeeded);
    }

    [Fact]
    public void Seed_RepairsMissingInitialRoleGroupExactlyOnce()
    {
        RbacSeeder.Seed(_db);
        var sale = _db.Roles.Single(role => role.Code == "SALE");
        sale.RoleGroupId = null;
        sale.InitialGroupSeeded = false;
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        Assert.Equal("CRM", _db.RoleGroups.Single(group => group.Id == sale.RoleGroupId).Code);
        Assert.True(sale.InitialGroupSeeded);
    }

    [Fact]
    public void Seed_SuperAdminAlwaysGetsEveryPermission()
    {
        RbacSeeder.Seed(_db);
        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);

        Assert.Equal(_db.Permissions.Count(), _db.RolePermissions.Count(rp => rp.RoleId == sa.Id));
    }

    [Fact]
    public void Seed_ProjectReportPermissionsMatchApprovedRoleMatrix()
    {
        RbacSeeder.Seed(_db);
        var permissionCodesByRole = _db.Roles.ToDictionary(
            role => role.Code,
            role => _db.RolePermissions.Where(item => item.RoleId == role.Id)
                .Select(item => item.Permission.Module + "." + item.Permission.Action)
                .ToHashSet());

        foreach (var roleCode in new[] { "SUPER_ADMIN", "ADMIN", "BGD", "ACCOUNTANT" })
        {
            Assert.Contains("reports.projects.view", permissionCodesByRole[roleCode]);
            Assert.Contains("reports.projects.export", permissionCodesByRole[roleCode]);
        }
        Assert.Contains("reports.projects.view", permissionCodesByRole["PM"]);
        Assert.DoesNotContain("reports.projects.export", permissionCodesByRole["PM"]);
        foreach (var roleCode in new[] { "SALE", "DESIGN", "LEGAL_OFFICER", "QS", "WAREHOUSE" })
        {
            Assert.DoesNotContain("reports.projects.view", permissionCodesByRole[roleCode]);
            Assert.DoesNotContain("reports.projects.export", permissionCodesByRole[roleCode]);
        }
    }

    [Fact]
    public void Seed_TopsUpNewlyDeclaredBusinessRolePermissions_ButPreservesAdminGrants()
    {
        // Boot 1 — seed the whole default matrix.
        RbacSeeder.Seed(_db);

        var sale = _db.Roles.Single(r => r.Code == "SALE");
        var originalDeclared = _db.RolePermissions
            .Where(rp => rp.RoleId == sale.Id)
            .Select(rp => rp.PermissionId)
            .ToList();
        Assert.NotEmpty(originalDeclared);

        // Simulate an admin GRANT of a permission the SALE defaults don't include.
        // pick any permission the role does not currently have.
        var extra = _db.Permissions
            .First(p => !originalDeclared.Contains(p.Id));
        _db.RolePermissions.Add(new RolePermission
        {
            RoleId = sale.Id,
            PermissionId = extra.Id,
            CreatedAt = DateTime.UtcNow,
        });

        // Simulate the catalog + defaults being extended between boots. The
        // reseed should top up any newly-declared permission that this role's
        // pattern would match, while NEVER removing the admin's manual grant.
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        var afterReseed = _db.RolePermissions.Where(rp => rp.RoleId == sale.Id)
            .Select(rp => rp.PermissionId)
            .ToList();

        // (a) the admin's manual grant survives the reseed.
        Assert.Contains(extra.Id, afterReseed);
        // (b) every originally-declared permission is still present.
        foreach (var id in originalDeclared)
        {
            Assert.Contains(id, afterReseed);
        }
        // (c) the flag stays true — first-seed already happened.
        Assert.True(_db.Roles.Single(r => r.Id == sale.Id).InitialPermissionsSeeded);
    }

    [Fact]
    public void Seed_InitialBusinessRolePermissionsRepairsPartialStateWithoutDuplicates()
    {
        RbacSeeder.Seed(_db);
        var sale = _db.Roles.Single(role => role.Code == "SALE");
        var existing = _db.RolePermissions.Where(item => item.RoleId == sale.Id).ToList();
        Assert.True(existing.Count > 1);
        _db.RolePermissions.RemoveRange(existing.Skip(1));
        sale.InitialPermissionsSeeded = false;
        _db.SaveChanges();

        RbacSeeder.Seed(_db);

        var allCodes = PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog)
            .Select(entry => entry.Code)
            .ToList();
        Assert.Equal(ExpectedCount("SALE", allCodes), _db.RolePermissions.Count(item => item.RoleId == sale.Id));
        Assert.Equal(
            _db.RolePermissions.Count(item => item.RoleId == sale.Id),
            _db.RolePermissions.Where(item => item.RoleId == sale.Id).Select(item => item.PermissionId).Distinct().Count());
        Assert.True(sale.InitialPermissionsSeeded);
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
        var grown = new RbacSeedData.Bundle(
            expandedCatalog,
            extended.BusinessRoles,
            extended.RolePermissions,
            extended.RoleGroups);

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
