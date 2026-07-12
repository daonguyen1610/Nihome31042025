using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Proves each seeded business-role test user reaches the API with the
/// permission set its JSON pattern (rbac-defaults.json) prescribes. Exercises
/// the full pipeline introduced in NIH-367: TestDataSeeder → RbacSeeder →
/// AuthTestHelper.LoginAsRoleAsync → JWT → PermissionService.
/// </summary>
public class BusinessRolePermissionsTests : IntegrationTestBase
{
    public BusinessRolePermissionsTests(NihomeWebApplicationFactory factory) : base(factory) { }

    public static readonly TheoryData<string, string[], string[]> RoleExpectations = new()
    {
        // role, mustInclude, mustExclude
        { "SALE",       new[] { "dashboard.view", "contacts.view", "contacts.manage", "recruitment.applications.view", "profile.me.view", "crm.leads.view", "crm.leads.manage" },
                        new[] { "users.view", "users.manage", "content.news.manage", "processes.manage", "crm.leads.view.all" } },
        { "SALES_MANAGER", new[] { "dashboard.view", "contacts.view", "crm.leads.view", "crm.leads.manage", "crm.leads.view.all", "profile.me.view" },
                        new[] { "users.view", "users.manage", "content.news.manage", "processes.manage" } },
        { "DESIGN",     new[] { "dashboard.view", "content.news.view", "content.news.manage", "content.projects.manage", "processes.view", "profile.me.view" },
                        new[] { "users.view", "users.manage", "processes.manage", "contacts.manage" } },
        { "PM",         new[] { "dashboard.view", "content.projects.view", "content.projects.manage", "processes.view", "processes.manage", "recruitment.applications.view", "profile.me.view" },
                        new[] { "users.view", "users.manage", "content.news.manage" } },
        { "QS",         new[] { "dashboard.view", "content.projects.view", "processes.view", "profile.me.view" },
                        new[] { "content.projects.manage", "processes.manage", "users.view" } },
        { "ACCOUNTANT", new[] { "dashboard.view", "contacts.view", "system.audit.view", "profile.me.view" },
                        new[] { "contacts.manage", "system.audit.manage", "users.view" } },
        { "WAREHOUSE",  new[] { "dashboard.view", "processes.view", "profile.me.view" },
                        new[] { "processes.manage", "users.view", "contacts.view" } },
        { "BGD",        new[] { "dashboard.view", "users.view", "content.projects.view", "processes.view", "system.audit.view", "profile.me.view" },
                        new[] { "users.manage", "content.projects.manage", "processes.manage", "system.audit.manage" } },
    };

    [Theory]
    [MemberData(nameof(RoleExpectations))]
    public async Task BusinessRole_PermissionsMatchSeededPatterns(
        string roleCode, string[] mustInclude, string[] mustExclude)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, roleCode));

        var res = await Client.GetAsync("/api/users/me/permissions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        var perms = body.GetProperty("permissions").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        perms.Should().Contain(mustInclude, $"{roleCode} pattern expansion must include these codes");
        foreach (var denied in mustExclude)
        {
            perms.Should().NotContain(denied, $"{roleCode} must not grant {denied}");
        }
    }

    [Theory]
    [InlineData("SALE")]
    [InlineData("SALES_MANAGER")]
    [InlineData("DESIGN")]
    [InlineData("PM")]
    [InlineData("QS")]
    [InlineData("ACCOUNTANT")]
    [InlineData("WAREHOUSE")]
    [InlineData("BGD")]
    public async Task BusinessRole_CanReadDashboard_CannotMutateUsers(string roleCode)
    {
        // Smoke check that the new test users are real, active, and that the
        // permission filter is wired the same way for them as for system roles.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, roleCode));

        (await Client.GetAsync("/api/users/me/permissions")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createUser = await Client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = "0900099999",
            fullName = "Blocked",
            password = "P@ssword1",
            role = "ADMIN",
        });
        createUser.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
