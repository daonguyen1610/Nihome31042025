using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignApprovalInboxControllerTests : IntegrationTestBase
{
    public DesignApprovalInboxControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_RequiresSignInAndReturnsTheCallersOwnLists()
    {
        (await Client.GetAsync("/api/design-approval-inbox")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // No module permission is needed: the inbox only lists work addressed to the caller.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));
        var res = await Client.GetAsync("/api/design-approval-inbox");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("tasks").GetArrayLength().Should().Be(0);
        body.GetProperty("reviews").GetArrayLength().Should().Be(0);
    }
}
