using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;

namespace nihomebackend.tests.Data;

public class PermissionCatalogExpansionTests
{
    private static IReadOnlyList<string> AllCodes() =>
        PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog).Select(e => e.Code).ToList();

    private static IReadOnlyList<string> Expand(string roleCode)
    {
        var defaults = RbacSeedData.Default.RolePermissions[roleCode];
        return PermissionCatalog.ExpandPatterns(defaults.Patterns, AllCodes(), defaults.Deny);
    }

    [Theory]
    [InlineData("SALE")]
    [InlineData("DESIGN")]
    [InlineData("PM")]
    [InlineData("QS")]
    [InlineData("ACCOUNTANT")]
    [InlineData("WAREHOUSE")]
    [InlineData("BGD")]
    public void EveryBusinessRoleExpandsToNonEmptyPermissionSet(string roleCode)
    {
        var codes = Expand(roleCode);
        Assert.NotEmpty(codes);
        Assert.Contains("profile.me.view", codes);
        Assert.Contains("dashboard.view", codes);
    }

    [Fact]
    public void SaleCoversContactsAndApplicationsView()
    {
        var codes = Expand("SALE");
        Assert.Contains("contacts.view", codes);
        Assert.Contains("contacts.manage", codes);
        Assert.Contains("recruitment.applications.view", codes);
        Assert.DoesNotContain("recruitment.applications.manage", codes);
        Assert.DoesNotContain("content.projects.view", codes);
    }

    [Fact]
    public void DesignCoversAllContentButNotProcessesManage()
    {
        var codes = Expand("DESIGN");
        Assert.Contains("content.about.manage", codes);
        Assert.Contains("content.activities.manage", codes);
        Assert.Contains("content.news.manage", codes);
        Assert.Contains("content.projects.manage", codes);
        Assert.Contains("content.slideshow.manage", codes);
        Assert.Contains("processes.view", codes);
        Assert.DoesNotContain("processes.manage", codes);
        Assert.DoesNotContain("contacts.view", codes);
    }

    [Fact]
    public void PmCoversProjectsAndProcessesFully()
    {
        var codes = Expand("PM");
        Assert.Contains("content.projects.view", codes);
        Assert.Contains("content.projects.manage", codes);
        Assert.Contains("processes.view", codes);
        Assert.Contains("processes.manage", codes);
        Assert.Contains("recruitment.applications.view", codes);
        Assert.DoesNotContain("content.news.manage", codes);
        // processes.* is single-segment by design; uploads sit under processes.uploads.*
        Assert.DoesNotContain("processes.uploads.manage", codes);
    }

    [Fact]
    public void QsIsReadOnlyOnProjectsAndProcesses()
    {
        var codes = Expand("QS");
        Assert.Contains("content.projects.view", codes);
        Assert.Contains("processes.view", codes);
        Assert.DoesNotContain("content.projects.manage", codes);
        Assert.DoesNotContain("processes.manage", codes);
    }

    [Fact]
    public void AccountantCanReadContactsAndAudit()
    {
        var codes = Expand("ACCOUNTANT");
        Assert.Contains("contacts.view", codes);
        Assert.Contains("system.audit.view", codes);
        Assert.DoesNotContain("contacts.manage", codes);
        Assert.DoesNotContain("system.audit.manage", codes);
    }

    [Fact]
    public void WarehouseHasOnlyDashboardProcessesAndProfile()
    {
        var codes = Expand("WAREHOUSE").ToHashSet();
        Assert.Contains("dashboard.view", codes);
        Assert.Contains("processes.view", codes);
        Assert.Contains("profile.me.view", codes);
        Assert.DoesNotContain("processes.manage", codes);
        Assert.DoesNotContain("content.projects.view", codes);
    }

    [Fact]
    public void BgdCanReadEverythingButCannotManageNonAuditAreas()
    {
        var codes = Expand("BGD");
        Assert.Contains("content.projects.view", codes);
        Assert.Contains("content.news.view", codes);
        Assert.Contains("processes.view", codes);
        Assert.Contains("contacts.view", codes);
        Assert.Contains("recruitment.applications.view", codes);
        Assert.Contains("system.audit.view", codes);
        Assert.DoesNotContain("content.projects.manage", codes);
        Assert.DoesNotContain("users.manage", codes);
    }

    [Fact]
    public void AdminGetsEverythingExceptDeniedCodes()
    {
        var codes = Expand("ADMIN");
        Assert.Contains("users.view", codes);
        Assert.Contains("content.projects.manage", codes);
        Assert.Contains("processes.manage", codes);
        Assert.DoesNotContain("users.manage", codes);
        Assert.DoesNotContain("system.audit.manage", codes);
    }
}
