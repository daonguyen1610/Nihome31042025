using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class CrmBusinessPipelineTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("Won")]
    [InlineData("RevisedWon")]
    [InlineData("Lost")]
    public async Task LeadConversion_QuoteDecision_ContractAndOpportunityClosure_PreserveCommercialContext(string outcome)
    {
        await LoginAsync("SALES_MANAGER");
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);
        var lead = await WriteAsync(HttpMethod.Post, "/api/leads", new
        {
            name = UniqueSlug("Factory investor"),
            phone,
            sourceCode = "marketing",
        });
        var leadPath = $"/api/leads/{Id(lead)}";
        var convertKey = Guid.NewGuid().ToString();
        lead = await WriteAsync(HttpMethod.Post, leadPath + "/convert", new { note = "Investor confirmed design-build requirements" }, convertKey);
        var replay = await WriteAsync(HttpMethod.Post, leadPath + "/convert", new { note = "Investor confirmed design-build requirements" }, convertKey);
        var customerId = lead.GetProperty("convertedCustomerId").GetInt32();
        var opportunityId = lead.GetProperty("convertedOpportunityId").GetInt32();
        replay.GetProperty("convertedCustomerId").GetInt32().Should().Be(customerId);
        replay.GetProperty("convertedOpportunityId").GetInt32().Should().Be(opportunityId);
        (await WithDbAsync(db => db.Customers.CountAsync(x => x.Id == customerId))).Should().Be(1);
        var opportunityPath = $"/api/opportunities/{opportunityId}";
        var opportunity = await ReadAsync(opportunityPath);
        opportunity.GetProperty("stage").GetString().Should().Be("Prospecting");
        var project = await WriteAsync(HttpMethod.Post, "/api/operational-projects", new { name = UniqueSlug("Factory delivery"), customerId });
        var projectId = Id(project);
        var update = JsonNode.Parse(opportunity.GetRawText())!.AsObject();
        update["operationalProjectId"] = projectId;
        opportunity = await WriteAsync(HttpMethod.Put, opportunityPath, update);

        await RejectAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Won", rowVersion = Version(opportunity) }, HttpStatusCode.BadRequest);
        (await ReadAsync(opportunityPath)).GetProperty("stage").GetString().Should().Be("Prospecting");
        foreach (var stage in new[] { "Qualification", "Proposal" })
            opportunity = await WriteAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = stage, rowVersion = Version(opportunity) });

        var quote = await WriteAsync(HttpMethod.Post, "/api/quotes", new
        {
            opportunityId,
            method = "Boq",
            packageDescription = "Factory shell design and construction",
            items = new[] { new { itemCode = "FACTORY", name = "Factory shell", unit = "m2", quantity = 100m, unitPrice = 1_000_000m } },
            discountPercent = 5m,
            vatPercent = 8m,
        });
        var originalQuoteId = Id(quote);
        quote.GetProperty("grandTotal").GetDecimal().Should().Be(102_600_000m);
        quote.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        quote = await MoveQuoteAsync(quote, "submit");
        await LoginAsync("SALE");
        await RejectAsync(HttpMethod.Post, $"/api/quotes/{Id(quote)}/approve", new { rowVersion = Version(quote) }, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.Quotes.SingleAsync(x => x.Id == Id(quote)))).Status.Should().Be(QuoteStatus.PendingApproval);
        await LoginAsync("SALES_MANAGER");
        quote = await MoveQuoteAsync(quote, "approve");
        if (outcome == "RevisedWon")
        {
            quote = await WriteAsync(HttpMethod.Put, $"/api/quotes/{Id(quote)}", new
            {
                rowVersion = Version(quote),
                discountPercent = 5m,
                vatPercent = 8m,
                items = new[] { new { itemCode = "FACTORY", name = "Factory shell revised scope", unit = "m2", quantity = 100m, unitPrice = 900_000m } },
            });
            Id(quote).Should().Be(originalQuoteId);
            quote.GetProperty("version").GetInt32().Should().Be(2);
            var versions = (await ReadAsync($"/api/quotes/{originalQuoteId}/versions")).GetProperty("versions").EnumerateArray().ToArray();
            versions.Should().HaveCount(2);
            versions.Single(x => x.GetProperty("version").GetInt32() == 1).GetProperty("grandTotal").GetDecimal().Should().Be(102_600_000m);
            quote.GetProperty("grandTotal").GetDecimal().Should().Be(92_340_000m);
            quote = await MoveQuoteAsync(quote, "submit");
            quote = await MoveQuoteAsync(quote, "approve");
        }
        quote = await MoveQuoteAsync(quote, "send");
        quote.GetProperty("status").GetString().Should().Be("SentToCustomer");
        opportunity = await ReadAsync(opportunityPath);
        opportunity = await WriteAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Negotiation", rowVersion = Version(opportunity) });
        await RejectAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Won", wonQuoteId = Id(quote), rowVersion = Version(opportunity) }, HttpStatusCode.BadRequest);
        (await ReadAsync(opportunityPath)).GetProperty("stage").GetString().Should().Be("Negotiation");
        if (outcome == "Lost")
        {
            await RejectAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Lost", rowVersion = Version(opportunity) }, HttpStatusCode.BadRequest);
            opportunity = await WriteAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Lost", lostReasonCode = "price", lostNote = "Investor selected another proposal", rowVersion = Version(opportunity) });
            (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OpportunityId == opportunityId))).Should().Be(0);
        }
        else
        {
            quote = await MoveQuoteAsync(quote, "customer-approve");
            var contract = await WriteAsync(HttpMethod.Post, "/api/contracts", new
            {
                customerId,
                operationalProjectId = projectId,
                opportunityId,
                quoteId = Id(quote),
                direction = "Upstream",
                type = "DesignAndBuild",
                value = quote.GetProperty("grandTotal").GetDecimal(),
                scopeOfWork = "Factory shell design and construction",
            });
            contract = await WriteAsync(HttpMethod.Post, $"/api/contracts/{Id(contract)}/transition", new { newStatus = "Signed", rowVersion = Version(contract) });
            contract.GetProperty("status").GetString().Should().Be("Signed");
            contract.GetProperty("customerId").GetInt32().Should().Be(customerId);
            contract.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
            contract.GetProperty("quoteId").GetInt32().Should().Be(Id(quote));
            await RejectAsync(HttpMethod.Post, $"/api/contracts/{Id(contract)}/transition", new { newStatus = "InProgress", rowVersion = Version(contract) }, HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.DesignProjects.CountAsync(x => x.ContractId == Id(contract)))).Should().Be(0);
            using (var form = new MultipartFormDataContent())
            {
                var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\nSigned factory contract test evidence\n%%EOF"));
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                form.Add(file, "file", "signed-factory-contract.pdf");
                form.Add(new StringContent("SignedScan"), "kind");
                using var uploaded = await Client.PostAsync($"/api/contracts/{Id(contract)}/attachments", form);
                uploaded.IsSuccessStatusCode.Should().BeTrue(await uploaded.Content.ReadAsStringAsync());
            }
            contract = await ReadAsync($"/api/contracts/{Id(contract)}");
            var executeKey = Guid.NewGuid().ToString();
            var execute = new { newStatus = "InProgress", rowVersion = Version(contract) };
            contract = await WriteAsync(HttpMethod.Post, $"/api/contracts/{Id(contract)}/transition", execute, executeKey);
            await WriteAsync(HttpMethod.Post, $"/api/contracts/{Id(contract)}/transition", execute, executeKey);
            var design = await WithDbAsync(db => db.DesignProjects.SingleAsync(x => x.ContractId == Id(contract)));
            design.CustomerId.Should().Be(customerId);
            design.OperationalProjectId.Should().Be(projectId);
            design.CurrentStage.Should().Be(DesignProjectStage.Concept);
            opportunity = await ReadAsync(opportunityPath);
            if (opportunity.GetProperty("stage").GetString() != "Won")
                opportunity = await WriteAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Won", wonQuoteId = Id(quote), rowVersion = Version(opportunity) });
            (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OpportunityId == opportunityId && x.Status == ContractStatus.InProgress))).Should().Be(1);
        }
        var finalStage = outcome == "Lost" ? "Lost" : "Won";
        opportunity.GetProperty("stage").GetString().Should().Be(finalStage);
        await RejectAsync(HttpMethod.Patch, opportunityPath + "/stage", new { targetStage = "Proposal", rowVersion = Version(opportunity) }, HttpStatusCode.BadRequest);
        var final = await ReadAsync(opportunityPath);
        final.GetProperty("stage").GetString().Should().Be(finalStage);
        final.GetProperty("customerId").GetInt32().Should().Be(customerId);
        final.GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
        final.GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await ReadAsync(leadPath)).GetProperty("convertedOpportunityId").GetInt32().Should().Be(opportunityId);
    }

    private Task LoginAsync(string role) => AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    private static int Id(JsonElement value) => value.GetProperty("id").GetInt32();
    private static string Version(JsonElement value) => value.GetProperty("rowVersion").GetString()!;
    private Task<JsonElement> MoveQuoteAsync(JsonElement quote, string action) => WriteAsync(HttpMethod.Post, $"/api/quotes/{Id(quote)}/{action}", new { rowVersion = Version(quote) });
    private async Task<JsonElement> ReadAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }
    private async Task<JsonElement> WriteAsync(HttpMethod method, string path, object body, string? key = null)
    {
        using var response = await SendAsync(method, path, body, key);
        response.IsSuccessStatusCode.Should().BeTrue($"{path}: {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }
    private async Task RejectAsync(HttpMethod method, string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(method, path, body);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
    }
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}
