using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>QuotesController</c> (NIH-84): RBAC scoping,
/// state-machine transitions, versioning, and totals arithmetic on the
/// wire. Sales/Sales Manager identity comes from the seeded test users.
/// </summary>
public class QuotesControllerTests : IntegrationTestBase
{
    public QuotesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/quotes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/quotes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_UnitCost_ComputesTotalsAndReturns201()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var oppId = await CreateOpportunityAsync();

        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "UnitCost",
            areaSqm = 50m,
            unitPricePerSqm = 8_000_000m,
            discountPercent = 5m,
            vatPercent = 10m,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("subtotal").GetDecimal().Should().Be(400_000_000m);
        // afterDiscount 380M ; vat 10% = 38M → grand 418M
        body.GetProperty("grandTotal").GetDecimal().Should().Be(418_000_000m);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("code").GetString().Should().StartWith("QT-");
        body.GetProperty("grandTotalInWords").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_UnknownOpportunity_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = 999_999,
            method = "UnitCost",
            areaSqm = 10m,
            unitPricePerSqm = 1_000_000m,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submit_Approve_Send_HappyPath()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var sendRes = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/send", new { });
        sendRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await ReadJsonAsync(sendRes);
        body.GetProperty("status").GetString().Should().Be("SentToCustomer");
    }

    [Fact]
    public async Task Approve_WithoutApprovePermission_IsForbidden()
    {
        // SALE has manage but not approve — SALES_MANAGER approves.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var res = await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AfterApproval_SpawnsVersionTwo()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { }))
            .EnsureSuccessStatusCode();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/approve", new { }))
            .EnsureSuccessStatusCode();

        var updateRes = await Client.PutAsJsonAsync($"/api/quotes/{quoteId}", new
        {
            areaSqm = 200m,
            unitPricePerSqm = 10_000_000m,
            discountPercent = 0m,
            vatPercent = 8m,
        });
        updateRes.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(updateRes);
        body.GetProperty("version").GetInt32().Should().Be(2);
        body.GetProperty("status").GetString().Should().Be("Draft");

        var versionsRes = await Client.GetAsync($"/api/quotes/{quoteId}/versions");
        versionsRes.EnsureSuccessStatusCode();
        var versionsBody = await ReadJsonAsync(versionsRes);
        versionsBody.GetProperty("versions").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Sale_CannotSeeAnotherSalesQuote()
    {
        // SALES_MANAGER creates the quote → owned by that user.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private async Task<int> CreateCustomerAsync()
    {
        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Q-Customer " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0922" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOpportunityAsync()
    {
        var customerId = await CreateCustomerAsync();
        var res = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Q-Deal " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            estimatedValue = 500_000_000m,
            winProbability = 40,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateQuoteAsync()
    {
        var oppId = await CreateOpportunityAsync();
        var res = await Client.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId = oppId,
            method = "UnitCost",
            areaSqm = 100m,
            unitPricePerSqm = 5_000_000m,
            discountPercent = 0m,
            vatPercent = 8m,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }
}
