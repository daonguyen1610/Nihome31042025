using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class SalesFunnelControllerTests : IntegrationTestBase
{
    public SalesFunnelControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_RequiresOpportunityViewAndScopesByViewAll()
    {
        (await Client.GetAsync("/api/reports/sales-funnel")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));
        (await Client.GetAsync("/api/reports/sales-funnel")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var own = await Client.GetAsync("/api/reports/sales-funnel");
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(own)).GetProperty("includesAllOwners").GetBoolean().Should().BeFalse();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var all = await Client.GetAsync("/api/reports/sales-funnel?from=2026-01-01&to=2026-12-31");
        all.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(all);
        body.GetProperty("includesAllOwners").GetBoolean().Should().BeTrue();
        body.GetProperty("from").GetString().Should().Be("2026-01-01");
        body.GetProperty("to").GetString().Should().Be("2026-12-31");
    }

    [Fact]
    public async Task Get_ReversedRange_IsBadRequestWithVietnameseMessage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var res = await Client.GetAsync("/api/reports/sales-funnel?from=2026-12-31&to=2026-01-01");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Ngày bắt đầu");
    }
}
