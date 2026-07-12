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

    // --- NIH-380: Nicon business roles + M1/M2/M3 permission catalog ---

    public static readonly TheoryData<string> NiconBusinessRoles = new()
    {
        "SALE", "SALES_MANAGER",
        "DESIGN", "DESIGN_LEAD",
        "ARCHITECT", "MEP_ENGINEER", "STRUCT_ENGINEER",
        "PM", "LEGAL_OFFICER",
        "QS", "ACCOUNTANT", "WAREHOUSE", "BGD",
    };

    [Theory]
    [MemberData(nameof(NiconBusinessRoles))]
    public void Default_DefinesAllNiconBusinessRolesWithPatterns(string code)
    {
        var bundle = RbacSeedData.Default;
        Assert.Contains(bundle.BusinessRoles, r => r.Code == code);
        Assert.True(
            bundle.RolePermissions.ContainsKey(code),
            $"missing rolePermissions entry for {code}");
        Assert.NotEmpty(bundle.RolePermissions[code].Patterns);
    }

    public static readonly TheoryData<string> Gd2CatalogModules = new()
    {
        // M0 Core
        "master-data.view", "master-data.manage",
        // M1 CRM
        "crm.leads.view", "crm.leads.manage", "crm.leads.convert",
        "crm.customers.view", "crm.customers.manage", "crm.customers.export",
        "crm.opportunities.view", "crm.opportunities.manage",
        "crm.quotes.view", "crm.quotes.manage", "crm.quotes.approve", "crm.quotes.send",
        "crm.tenders.view", "crm.tenders.manage", "crm.tenders.mark-result",
        "crm.capability-docs.view", "crm.capability-docs.manage",
        "crm.surveys.view", "crm.surveys.manage",
        "crm.contracts.view", "crm.contracts.manage", "crm.contracts.activate", "crm.contracts.appendix-manage",
        // M2 Design
        "design.projects.view", "design.projects.manage",
        "design.concepts.view", "design.concepts.manage", "design.concepts.finalize",
        "design.basic.view", "design.basic.manage", "design.basic.approve",
        "design.shop.view", "design.shop.manage", "design.shop.approve",
        "design.revisions.view", "design.revisions.manage",
        "design.ifc.view", "design.ifc.manage", "design.ifc.release",
        // M3 Permitting
        "permit.checklists.view", "permit.checklists.manage", "permit.checklists.mark-issued",
    };

    [Theory]
    [MemberData(nameof(Gd2CatalogModules))]
    public void Default_BaseCatalogContainsGd2Modules(string code)
    {
        Assert.Contains(RbacSeedData.Default.BaseCatalog, e => e.Code == code);
    }

    [Fact]
    public void SalesManager_ExpandsToWriteAccessOnQuotesAndContracts()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var mgr = bundle.RolePermissions["SALES_MANAGER"];
        var expanded = PermissionCatalog.ExpandPatterns(mgr.Patterns, allCodes, mgr.Deny);

        Assert.Contains("crm.quotes.approve", expanded);
        Assert.Contains("crm.contracts.activate", expanded);
        Assert.Contains("crm.contracts.appendix-manage", expanded);
        Assert.Contains("crm.tenders.mark-result", expanded);
    }

    [Fact]
    public void Sales_HasWriteOnPipelineButCannotApproveQuotes()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var sales = bundle.RolePermissions["SALE"];
        var expanded = PermissionCatalog.ExpandPatterns(sales.Patterns, allCodes, sales.Deny);

        Assert.Contains("crm.leads.manage", expanded);
        Assert.Contains("crm.customers.manage", expanded);
        Assert.Contains("crm.opportunities.manage", expanded);
        Assert.Contains("crm.quotes.manage", expanded);
        Assert.Contains("crm.quotes.send", expanded);
        Assert.DoesNotContain("crm.quotes.approve", expanded);
        Assert.DoesNotContain("crm.contracts.activate", expanded);
    }

    [Fact]
    public void DesignLead_CanFinalizeConceptAndReleaseIfc()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var lead = bundle.RolePermissions["DESIGN_LEAD"];
        var expanded = PermissionCatalog.ExpandPatterns(lead.Patterns, allCodes, lead.Deny);

        Assert.Contains("design.concepts.finalize", expanded);
        Assert.Contains("design.basic.approve", expanded);
        Assert.Contains("design.shop.approve", expanded);
        Assert.Contains("design.ifc.release", expanded);
    }

    [Fact]
    public void Design_CannotApproveOrReleaseIfc()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var design = bundle.RolePermissions["DESIGN"];
        var expanded = PermissionCatalog.ExpandPatterns(design.Patterns, allCodes, design.Deny);

        Assert.Contains("design.basic.manage", expanded);
        Assert.Contains("design.shop.manage", expanded);
        Assert.DoesNotContain("design.basic.approve", expanded);
        Assert.DoesNotContain("design.shop.approve", expanded);
        Assert.DoesNotContain("design.ifc.release", expanded);
        Assert.DoesNotContain("design.concepts.finalize", expanded);
    }

    [Fact]
    public void LegalOfficer_HasFullPermitAccessButNoDesignWrite()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var legal = bundle.RolePermissions["LEGAL_OFFICER"];
        var expanded = PermissionCatalog.ExpandPatterns(legal.Patterns, allCodes, legal.Deny);

        Assert.Contains("permit.checklists.view", expanded);
        Assert.Contains("permit.checklists.manage", expanded);
        Assert.Contains("permit.checklists.mark-issued", expanded);
        Assert.Contains("design.basic.view", expanded);
        Assert.DoesNotContain("design.basic.manage", expanded);
        Assert.DoesNotContain("crm.quotes.approve", expanded);
    }

    [Fact]
    public void Pm_HasFullPermitAndIfcAccessButNoDesignAuthoring()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var pm = bundle.RolePermissions["PM"];
        var expanded = PermissionCatalog.ExpandPatterns(pm.Patterns, allCodes, pm.Deny);

        Assert.Contains("permit.checklists.manage", expanded);
        Assert.Contains("design.ifc.release", expanded);
        Assert.Contains("crm.contracts.view", expanded);
        Assert.DoesNotContain("design.basic.manage", expanded);
        Assert.DoesNotContain("design.shop.manage", expanded);
        Assert.DoesNotContain("crm.customers.manage", expanded);
    }

    [Fact]
    public void Bgd_GetsEveryViewPermissionButNoWrite()
    {
        var bundle = RbacSeedData.Default;
        var allCodes = bundle.BaseCatalog.Select(e => e.Code).ToList();
        var bgd = bundle.RolePermissions["BGD"];
        var expanded = PermissionCatalog.ExpandPatterns(bgd.Patterns, allCodes, bgd.Deny);

        // Every .view code in the catalog should be included via the **.view pattern.
        foreach (var code in allCodes.Where(c => c.EndsWith(".view", StringComparison.Ordinal)))
        {
            Assert.Contains(code, expanded);
        }
        Assert.DoesNotContain("crm.quotes.approve", expanded);
        Assert.DoesNotContain("design.ifc.release", expanded);
    }

    [Fact]
    public void Default_BusinessRoleCodesAreUniqueAndUppercase()
    {
        var bundle = RbacSeedData.Default;
        var codes = bundle.BusinessRoles.Select(r => r.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var c in codes)
        {
            Assert.Equal(c.ToUpperInvariant(), c);
        }
    }
}
