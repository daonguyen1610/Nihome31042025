using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// A contract drafted before (or apart from) its quotation can later be tied
/// to the approved quote, so the agreed price stays traceable. Covers the
/// link endpoint and the approved-quote rule on contract create.
/// </summary>
public sealed class ContractQuoteLinkTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ApprovedQuote_LinksToExistingContract_AndBlockedCasesChangeNothing()
    {
        await LoginAsync("SALES_MANAGER");
        var deal = await ConvertLeadAsync("Kho lạnh Hậu Giang");
        var draftQuote = await CreateQuoteAsync(deal.OpportunityId, 900_000m);
        var approvedQuote = await ApproveAsync(await CreateQuoteAsync(deal.OpportunityId, 1_000_000m));
        var contract = await WriteAsync(HttpMethod.Post, "/api/contracts", ContractBody(deal));
        var contractId = Id(contract);

        using (var draft = await SendAsync(HttpMethod.Post, $"/api/contracts/{contractId}/link-quote",
                   new { quoteId = Id(draftQuote), rowVersion = Version(contract) }))
        {
            draft.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadJsonAsync(draft)).GetProperty("message").GetString().Should().Contain("chưa được duyệt");
        }
        var other = await ConvertLeadAsync("Nhà máy Minh Phúc");
        var otherQuote = await ApproveAsync(await CreateQuoteAsync(other.OpportunityId, 500_000m));
        using (var foreign = await SendAsync(HttpMethod.Post, $"/api/contracts/{contractId}/link-quote",
                   new { quoteId = Id(otherQuote), rowVersion = Version(contract) }))
        {
            foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        (await WithDbAsync(db => db.Contracts.SingleAsync(item => item.Id == contractId))).QuoteId.Should().BeNull();

        await LoginAsync("DESIGN");
        using (var forbidden = await SendAsync(HttpMethod.Post, $"/api/contracts/{contractId}/link-quote",
                   new { quoteId = Id(approvedQuote), rowVersion = Version(contract) }))
        {
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        await LoginAsync("SALES_MANAGER");
        var linked = await WriteAsync(HttpMethod.Post, $"/api/contracts/{contractId}/link-quote",
            new { quoteId = Id(approvedQuote), rowVersion = Version(contract) });
        linked.GetProperty("quoteId").GetInt32().Should().Be(Id(approvedQuote));
        linked.GetProperty("opportunityId").GetInt32().Should().Be(deal.OpportunityId);
        linked.GetProperty("operationalProjectId").GetInt32().Should().Be(deal.ProjectId);

        // One source quote per contract, and one live contract per quote.
        var second = await WriteAsync(HttpMethod.Post, "/api/contracts", ContractBody(deal));
        using (var reused = await SendAsync(HttpMethod.Post, $"/api/contracts/{Id(second)}/link-quote",
                   new { quoteId = Id(approvedQuote), rowVersion = Version(second) }))
        {
            reused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ReadJsonAsync(reused)).GetProperty("message").GetString().Should().Contain(linked.GetProperty("contractNumber").GetString());
        }
        var anotherApproved = await ApproveAsync(await CreateQuoteAsync(deal.OpportunityId, 1_100_000m));
        using (var replace = await SendAsync(HttpMethod.Post, $"/api/contracts/{contractId}/link-quote",
                   new { quoteId = Id(anotherApproved), rowVersion = Version(linked) }))
        {
            replace.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        (await WithDbAsync(db => db.Contracts.SingleAsync(item => item.Id == contractId))).QuoteId
            .Should().Be(Id(approvedQuote));
    }

    [Fact]
    public async Task CreateContract_WithUnapprovedQuote_IsRejected()
    {
        await LoginAsync("SALES_MANAGER");
        var deal = await ConvertLeadAsync("Xưởng cơ khí Bình Dương");
        var draftQuote = await CreateQuoteAsync(deal.OpportunityId, 750_000m);
        var before = await WithDbAsync(db => db.Contracts.CountAsync(item => item.CustomerId == deal.CustomerId));

        using var response = await SendAsync(HttpMethod.Post, "/api/contracts",
            ContractBody(deal, quoteId: Id(draftQuote)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString().Should().Contain("chưa được duyệt");
        (await WithDbAsync(db => db.Contracts.CountAsync(item => item.CustomerId == deal.CustomerId))).Should().Be(before);
    }

    private sealed record Deal(int CustomerId, int OpportunityId, int ProjectId);

    private static object ContractBody(Deal deal, int? quoteId = null) => new
    {
        customerId = deal.CustomerId,
        opportunityId = deal.OpportunityId,
        quoteId,
        direction = "Upstream",
        type = "DesignAndBuild",
        value = 1_080_000_000m,
        scopeOfWork = "Thiết kế và thi công kho lạnh",
    };

    private async Task<Deal> ConvertLeadAsync(string company)
    {
        var lead = await WriteAsync(HttpMethod.Post, "/api/leads", new
        {
            name = $"Đại diện {company}",
            phone = "09" + Random.Shared.Next(10_000_000, 99_999_999),
            sourceCode = "marketing",
        });
        var converted = await WriteAsync(HttpMethod.Post, $"/api/leads/{Id(lead)}/convert", new { note = company });
        var opportunityId = converted.GetProperty("convertedOpportunityId").GetInt32();
        var projectId = await WithDbAsync(db => db.Opportunities
            .Where(item => item.Id == opportunityId)
            .Select(item => item.OperationalProjectId!.Value)
            .SingleAsync());
        return new Deal(converted.GetProperty("convertedCustomerId").GetInt32(), opportunityId, projectId);
    }

    private Task<JsonElement> CreateQuoteAsync(int opportunityId, decimal unitPrice) =>
        WriteAsync(HttpMethod.Post, "/api/quotes", new
        {
            opportunityId,
            method = "Boq",
            items = new[] { new { itemCode = "KHO", name = "Kho lạnh panel", unit = "m2", quantity = 1000m, unitPrice } },
            vatPercent = 8m,
        });

    private async Task<JsonElement> ApproveAsync(JsonElement quote)
    {
        quote = await WriteAsync(HttpMethod.Post, $"/api/quotes/{Id(quote)}/submit", new { rowVersion = Version(quote) });
        return await WriteAsync(HttpMethod.Post, $"/api/quotes/{Id(quote)}/approve", new { rowVersion = Version(quote) });
    }

    private Task LoginAsync(string role) =>
        AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private static int Id(JsonElement value) => value.GetProperty("id").GetInt32();

    private static string Version(JsonElement value) => value.GetProperty("rowVersion").GetString()!;

    private async Task<JsonElement> WriteAsync(HttpMethod method, string path, object body)
    {
        using var response = await SendAsync(method, path, body);
        response.IsSuccessStatusCode.Should().BeTrue($"{path}: {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}
