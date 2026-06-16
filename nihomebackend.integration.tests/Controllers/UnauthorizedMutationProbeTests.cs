using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Safety net asserting every <c>[RequirePermission]</c>-guarded endpoint
/// (GET + mutations) rejects anonymous callers with 401 and USER-role callers
/// (whose only permissions are <c>profile.me.*</c>) with 403. The route list
/// is reflected from controller attributes at runtime by
/// <see cref="Infrastructure.ProtectedEndpointInventory"/>, so newly added
/// endpoints get coverage automatically.
/// </summary>
public class UnauthorizedMutationProbeTests : IntegrationTestBase
{
    public UnauthorizedMutationProbeTests(NihomeWebApplicationFactory factory) : base(factory) { }

    public static TheoryData<string, string, bool> ProtectedEndpoints
    {
        get
        {
            var data = new TheoryData<string, string, bool>();
            foreach (var endpoint in Infrastructure.ProtectedEndpointInventory.Discover())
            {
                data.Add(endpoint.HttpMethod, endpoint.Url, endpoint.ExpectsMultipart);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task Anonymous_OnProtectedEndpoint_ReturnsUnauthorized(string method, string url, bool multipart)
    {
        using var req = BuildRequest(method, url, multipart);
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: $"{method} {url} must reject anonymous callers");
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task Customer_OnProtectedEndpoint_ReturnsForbidden(string method, string url, bool multipart)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);

        using var req = BuildRequest(method, url, multipart);
        var res = await Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: $"{method} {url} must reject USER-role callers");
    }

    [Fact]
    public void Inventory_ContainsAtLeastTwentyEndpoints()
    {
        Infrastructure.ProtectedEndpointInventory.Discover()
            .Should().HaveCountGreaterThan(20, "scanner must keep discovering controller routes");
    }

    private static HttpRequestMessage BuildRequest(string method, string url, bool multipart)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (multipart)
        {
            // [Consumes("multipart/form-data")] gates routing via IActionConstraint;
            // attach a tiny multipart body so the request reaches the auth filter.
            req.Content = new MultipartFormDataContent($"---boundary-{Guid.NewGuid():N}");
        }
        else
        {
            req.Content = JsonContent.Create(new { });
        }
        return req;
    }
}
