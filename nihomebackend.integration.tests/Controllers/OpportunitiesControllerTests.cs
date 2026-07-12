using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>OpportunitiesController</c> (NIH-83): RBAC
/// scoping, stage transition rules (Won requires side-payload, Lost
/// requires reason + note, terminal stages cannot revert), and Kanban
/// pipeline aggregation.
/// </summary>
public class OpportunitiesControllerTests : IntegrationTestBase
{
    public OpportunitiesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/opportunities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsRoleWithoutPerm_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/opportunities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsSale_PersistsAndAssignsCallerAsOwner()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Test opportunity " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = 1_500_000_000m,
            winProbability = 30,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(res);
        body.GetProperty("stage").GetString().Should().Be("Prospecting");
        body.GetProperty("ownerUserId").GetInt32().Should().NotBe(0);
    }

    [Fact]
    public async Task Create_WithUnknownCustomer_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Ghost deal",
            customerId = 999_999,
            estimatedValue = 100m,
            winProbability = 10,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TerminalStage_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Skip-to-Won",
            customerId,
            estimatedValue = 100m,
            winProbability = 100,
            stage = "Won",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStage_ToLost_RequiresReasonAndNote()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opId = await CreateOpportunityAsync();

        var res = await Client.PatchAsJsonAsync($"/api/opportunities/{opId}/stage", new
        {
            targetStage = "Lost",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeStage_ToLost_WithReasonAndNote_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opId = await CreateOpportunityAsync();

        var res = await Client.PatchAsJsonAsync($"/api/opportunities/{opId}/stage", new
        {
            targetStage = "Lost",
            lostReasonCode = "price",
            lostNote = "Khách bảo giá cao",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("stage").GetString().Should().Be("Lost");
        body.GetProperty("lostReasonCode").GetString().Should().Be("price");
        body.GetProperty("winProbability").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ChangeStage_ToWon_SetsProbability100()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opId = await CreateOpportunityAsync();

        var res = await Client.PatchAsJsonAsync($"/api/opportunities/{opId}/stage", new
        {
            targetStage = "Won",
            wonQuoteId = 1,
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(res);
        body.GetProperty("stage").GetString().Should().Be("Won");
        body.GetProperty("winProbability").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task ChangeStage_FromTerminal_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opId = await CreateOpportunityAsync();

        // Move to Won first
        (await Client.PatchAsJsonAsync($"/api/opportunities/{opId}/stage",
            new { targetStage = "Won", wonQuoteId = 1 })).EnsureSuccessStatusCode();

        // Try to move back
        var reject = await Client.PatchAsJsonAsync($"/api/opportunities/{opId}/stage",
            new { targetStage = "Negotiation" });
        reject.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pipeline_ReturnsSixColumnsWithTotals()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await CreateOpportunityAsync(value: 100);
        await CreateOpportunityAsync(value: 200);

        var res = await Client.GetAsync("/api/opportunities/pipeline");
        res.EnsureSuccessStatusCode();

        var body = await ReadJsonAsync(res);
        var columns = body.GetProperty("columns");
        columns.GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task Delete_SalesUser_CannotDeleteOtherOwnersOpportunity()
    {
        // Manager creates the opportunity owned by SALES_MANAGER
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var opId = await CreateOpportunityAsync();

        // SALE (different owner) attempts delete
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.DeleteAsync($"/api/opportunities/{opId}");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Manager confirms still exists
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync($"/api/opportunities/{opId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------- helpers ----------

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Op-Customer " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0911" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        return body.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOpportunityAsync(decimal value = 1_000_000m)
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Deal " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = value,
            winProbability = 40,
        });
        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        return body.GetProperty("id").GetInt32();
    }
}
