using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;

namespace nihomebackend.tests.Data;

/// <summary>
/// Sanity tests for the JSON-backed RBAC seed bundle so a malformed defaults
/// file (typo, missing role, broken pattern) is caught at unit-test time
/// rather than at production startup.
/// </summary>
public class RbacSeedDataTests
{
    [Fact]
    public void Default_ExposesBaseCatalogWithRequiredCrossCuttingCodes()
    {
        var bundle = RbacSeedData.Default;

        Assert.Contains(bundle.BaseCatalog, e => e.Code == "dashboard.view");
        Assert.Contains(bundle.BaseCatalog, e => e.Code == "rbac.roles.view");
        Assert.Contains(bundle.BaseCatalog, e => e.Code == "rbac.roles.manage");
        Assert.Contains(bundle.BaseCatalog, e => e.Code == "profile.me.view");
        Assert.Contains(bundle.BaseCatalog, e => e.Code == "profile.me.update");
    }

    [Fact]
    public void Default_DerivesPermissionDescriptionKeysByConvention()
    {
        var bundle = RbacSeedData.Default;
        var dash = bundle.BaseCatalog.Single(e => e.Code == "dashboard.view");
        Assert.Equal("rbac.perm.dashboard.view", dash.DescriptionKey);
    }

    [Fact]
    public void Default_LabelKeysFollowRoleConvention()
    {
        var bundle = RbacSeedData.Default;
        var sale = bundle.BusinessRoles.Single(r => r.Code == "SALE");
        Assert.Equal("rbac.role.SALE.label", sale.LabelKey);
        Assert.Equal("rbac.role.SALE.description", sale.DescriptionKey);
    }

    [Fact]
    public void Default_DefinesPatternsForEverySystemRole()
    {
        var bundle = RbacSeedData.Default;
        foreach (var code in SystemRoleCodes.All)
        {
            Assert.True(bundle.RolePermissions.ContainsKey(code), $"missing role permissions for {code}");
        }
    }

    [Fact]
    public void Default_AdminHasDenyForUsersManage()
    {
        var admin = RbacSeedData.Default.RolePermissions[SystemRoleCodes.Admin];
        Assert.Contains("users.manage", admin.Deny);
    }
}
