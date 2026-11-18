using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DesignApprovalInboxControllerTests : IntegrationTestBase
{
    public DesignApprovalInboxControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("/api/design-approval-inbox/summary")]
    [InlineData("/api/design-approval-inbox/tasks")]
    [InlineData("/api/design-approval-inbox/reviews")]
    public async Task Get_RequiresSignInButNoModulePermission(string url)
    {
        (await Client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The inbox only lists work addressed to the caller.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));
        (await Client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lists_ArePagedAndEmptyForSomeoneWithNoDesignWork()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));

        var summary = await ReadJsonAsync(await Client.GetAsync("/api/design-approval-inbox/summary"));
        var tasks = await ReadJsonAsync(await Client.GetAsync("/api/design-approval-inbox/tasks?page=2&pageSize=5"));

        summary.GetProperty("taskCount").GetInt32().Should().Be(0);
        summary.GetProperty("reviewCount").GetInt32().Should().Be(0);
        summary.GetProperty("dossiers").GetArrayLength().Should().Be(0);
        tasks.GetProperty("page").GetInt32().Should().Be(2);
        tasks.GetProperty("pageSize").GetInt32().Should().Be(5);
        tasks.GetProperty("total").GetInt32().Should().Be(0);
        tasks.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Reviews_RejectUnknownStageAndOversizedPages()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));

        var stage = await Client.GetAsync("/api/design-approval-inbox/reviews?stage=Permit");
        stage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await stage.Content.ReadAsStringAsync()).Should().Contain("Giai đoạn không hợp lệ");
        (await Client.GetAsync("/api/design-approval-inbox/tasks?pageSize=500"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
