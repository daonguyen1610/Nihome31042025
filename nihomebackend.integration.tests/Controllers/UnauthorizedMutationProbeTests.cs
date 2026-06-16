using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Safety net asserting every protected admin/SA mutation endpoint rejects a
/// USER-role caller with 403 and an anonymous caller with 401. Guards against
/// regressions when controllers migrate from <c>[Authorize(Roles=...)]</c> to
/// <c>[RequirePermission]</c> in later RBAC phases.
/// </summary>
public class UnauthorizedMutationProbeTests : IntegrationTestBase
{
    public UnauthorizedMutationProbeTests(NihomeWebApplicationFactory factory) : base(factory) { }

    public static readonly TheoryData<string, string> ProtectedMutationEndpoints = new()
    {
        { "DELETE", "/api/about-sections/1" },
        { "PUT",    "/api/about-sections/1" },
        { "DELETE", "/api/news/1" },
        { "PUT",    "/api/news/1" },
        { "DELETE", "/api/projects/1" },
        { "DELETE", "/api/project-categories/1" },
        { "DELETE", "/api/services/1" },
        { "DELETE", "/api/logos/1" },
        { "DELETE", "/api/activities/1" },
        { "DELETE", "/api/activity-categories/1" },
        { "DELETE", "/api/processes/1" },
        { "POST",   "/api/Mail/send" },
        { "DELETE", "/api/contacts/1" },
        { "DELETE", "/api/users/1" },
        { "PUT",    "/api/users/1" },
        { "DELETE", "/api/audit-logs/1" },
        { "PUT",    "/api/audit-logs/config" },
        { "DELETE", "/api/admin/rbac/roles/1" },
        { "PUT",    "/api/admin/rbac/roles/1" },
        { "DELETE", "/api/job-positions/1" },
        { "DELETE", "/api/employment-types/1" },
        { "PUT",    "/api/site-settings/otp-settings" },
    };

    [Theory]
    [MemberData(nameof(ProtectedMutationEndpoints))]
    public async Task Anonymous_OnMutation_ReturnsUnauthorized(string method, string url)
    {
        using var req = BuildRequest(method, url);
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"{method} {url} must reject anonymous callers");
    }

    [Theory]
    [MemberData(nameof(ProtectedMutationEndpoints))]
    public async Task Customer_OnMutation_ReturnsForbidden(string method, string url)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);

        using var req = BuildRequest(method, url);
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"{method} {url} must reject USER-role callers");
    }

    private static HttpRequestMessage BuildRequest(string method, string url) =>
        new(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(new { }),
        };
}
