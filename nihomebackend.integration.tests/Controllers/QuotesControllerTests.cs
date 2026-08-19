using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task Delete_SubmittedQuote_RemovesAggregateAndPreservesOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();
        var quote = await ReadJsonAsync(await Client.GetAsync($"/api/quotes/{quoteId}"));
        var opportunityId = quote.GetProperty("opportunityId").GetInt32();
        (await Client.PostAsJsonAsync($"/api/quotes/{quoteId}/submit", new { })).EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Client.GetAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/opportunities/{opportunityId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_MissingQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.DeleteAsync("/api/quotes/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.DeleteAsync("/api/quotes/9999999")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_OtherOwnersQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/quotes/{quoteId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_UploadListDelete_RoundTrips()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();

        var upload = await UploadDocumentAsync(quoteId, "proposal.pdf", "Customer proposal");

        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await ReadJsonAsync(upload);
        uploaded.GetProperty("originalFileName").GetString().Should().Be("proposal.pdf");
        uploaded.GetProperty("label").GetString().Should().Be("Customer proposal");
        uploaded.GetProperty("filePath").GetString().Should().StartWith($"/files/quotes/{quoteId}/");

        var list = await Client.GetAsync($"/api/quotes/{quoteId}/documents");
        list.EnsureSuccessStatusCode();
        var listed = await ReadJsonAsync(list);
        listed.GetArrayLength().Should().Be(1);

        var documentId = uploaded.GetProperty("id").GetInt32();
        var content = await Client.GetAsync($"/api/quotes/{quoteId}/documents/{documentId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("document");

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/quotes/{quoteId}/documents/{documentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/quotes/{quoteId}/documents/{documentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await Client.GetAsync($"/api/quotes/{quoteId}/documents")))
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Documents_UnsupportedExtension_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var quoteId = await CreateQuoteAsync();

        (await UploadDocumentAsync(quoteId, "proposal.exe", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Documents_OtherOwnersQuote_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var quoteId = await CreateQuoteAsync();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.GetAsync($"/api/quotes/{quoteId}/documents"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UploadDocumentAsync(quoteId, "proposal.pdf", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Documents_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await UploadDocumentAsync(9999999, "proposal.pdf", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------- helpers ----------

    private async Task<HttpResponseMessage> UploadDocumentAsync(int quoteId, string fileName, string? label)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("document"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);
        if (label is not null) content.Add(new StringContent(label), "label");
        return await Client.PostAsync($"/api/quotes/{quoteId}/documents", content);
    }

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
