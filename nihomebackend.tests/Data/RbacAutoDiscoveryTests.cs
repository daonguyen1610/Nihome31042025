using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Models.Rbac;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Data;

/// <summary>
/// Validates the auto-discovery + wildcard-defaults pipeline so that a new
/// page (controller annotated with [RequirePermission]) is auto-registered
/// in the permissions table and inherited by any role whose default pattern
/// matches it — without editing PermissionCatalog.
/// </summary>
public class RbacAutoDiscoveryTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();

    public void Dispose() => _db.Dispose();

    // Fake controllers living in the test assembly to simulate "a new page".
    [RequirePermission("content.testimonials", "view")]
    [RequirePermission("content.testimonials", "manage")]
    private sealed class FakeTestimonialsController { public void Noop() { } }

    [Fact]
    public void Discover_FindsAttributesOnTypesInScannedAssembly()
    {
        var discovered = PermissionDiscovery.Discover(new[] { typeof(RbacAutoDiscoveryTests).Assembly });

        Assert.Contains(discovered, e => e.Code == "content.testimonials.view");
        Assert.Contains(discovered, e => e.Code == "content.testimonials.manage");
    }

    [Fact]
    public void Resolve_MergesBaseCatalogWithDiscoveredEntries()
    {
        var discovered = PermissionDiscovery.Discover(new[] { typeof(RbacAutoDiscoveryTests).Assembly });
        var catalog = PermissionCatalog.Resolve(RbacSeedData.Default.BaseCatalog, discovered);

        Assert.Contains(catalog, e => e.Code == "dashboard.view"); // from base
        Assert.Contains(catalog, e => e.Code == "content.testimonials.manage"); // discovered
    }

    [Fact]
    public void Seed_RegistersDiscoveredPermissionsFromCallerAssembly()
    {
        RbacSeeder.Seed(_db, new[] { typeof(RbacAutoDiscoveryTests).Assembly });

        Assert.Contains(_db.Permissions, p => p.Module == "content.testimonials" && p.Action == "view");
        Assert.Contains(_db.Permissions, p => p.Module == "content.testimonials" && p.Action == "manage");
    }

    [Fact]
    public void WildcardDefaults_GrantNewlyDiscoveredPermissionsToMatchingRoles()
    {
        // DESIGN's default pattern is "content.**" so it must inherit any new
        // content.* permission discovered at boot — zero edits to the catalog.
        RbacSeeder.Seed(_db, new[] { typeof(RbacAutoDiscoveryTests).Assembly });

        var design = _db.Roles.Single(r => r.Code == "DESIGN");
        var designPerms = _db.RolePermissions
            .Where(rp => rp.RoleId == design.Id)
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Module + "." + p.Action)
            .ToList();

        Assert.Contains("content.testimonials.view", designPerms);
        Assert.Contains("content.testimonials.manage", designPerms);
    }

    [Fact]
    public void SuperAdminGetsDiscoveredPermissionsToo()
    {
        RbacSeeder.Seed(_db, new[] { typeof(RbacAutoDiscoveryTests).Assembly });

        var sa = _db.Roles.Single(r => r.Code == SystemRoleCodes.SuperAdmin);
        Assert.Equal(_db.Permissions.Count(), _db.RolePermissions.Count(rp => rp.RoleId == sa.Id));
    }

    [Fact]
    public void AdminDefaultsDoNotIncludeUsersManageByPolicy()
    {
        RbacSeeder.Seed(_db);

        var admin = _db.Roles.Single(r => r.Code == SystemRoleCodes.Admin);
        var hasUsersManage = _db.RolePermissions
            .Where(rp => rp.RoleId == admin.Id)
            .Join(_db.Permissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Module + "." + p.Action)
            .Any(c => c == "users.manage");

        Assert.False(hasUsersManage);
    }

    [Fact]
    public void Expand_SingleStarMatchesOneSegmentOnly()
    {
        var codes = new[] { "content.projects.view", "content.projects.manage", "profile.me.view" };
        // "profile.me.*" should match profile.me.view; "content.*" pattern (single star) must NOT cross dots.
        var matched = PermissionCatalog.ExpandPatterns(["profile.me.*", "content.*"], codes);
        Assert.Contains("profile.me.view", matched);
        Assert.DoesNotContain("content.projects.view", matched);
    }

    [Fact]
    public void Expand_DenyListSubtractsFromAllowMatches()
    {
        var codes = new[] { "users.view", "users.manage", "profile.me.view" };
        var matched = PermissionCatalog.ExpandPatterns(["**"], codes, denyPatterns: ["users.manage"]);
        Assert.Contains("users.view", matched);
        Assert.Contains("profile.me.view", matched);
        Assert.DoesNotContain("users.manage", matched);
    }
}
