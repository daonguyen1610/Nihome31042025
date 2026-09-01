using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class CrmMultiUserConcurrencyTests : IntegrationTestBase
{
    public CrmMultiUserConcurrencyTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Lead_TwoActorsUpdatingSameVersion_OneWinsWithoutLostUpdate()
    {
        var (manager, admin) = await CreateActorClientsAsync();
        using var created = await manager.PostAsJsonAsync("/api/leads", new
        {
            name = "Concurrent lead",
            phone = "0901" + Random.Shared.Next(100000, 999999),
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var original = await ReadJsonAsync(created);
        var id = original.GetProperty("id").GetInt32();
        var rowVersion = GetRequiredString(original, "rowVersion");
        var ownerUserId = original.GetProperty("ownerUserId").GetInt32();

        var results = await SendCompetingPutsAsync(
            manager,
            admin,
            $"/api/leads/{id}",
            new
            {
                rowVersion,
                name = "Lead manager winner",
                phone = "0901000001",
                sourceCode = "marketing",
                status = "Contacted",
                ownerUserId,
            },
            new
            {
                name = "Lead admin winner",
                phone = "0901000002",
                sourceCode = "marketing",
                status = "Interested",
                ownerUserId,
            },
            rowVersion);

        await AssertSingleWinnerAsync(results, "name", manager, $"/api/leads/{id}", rowVersion);
    }

    [Fact]
    public async Task Customer_TwoActorsUpdatingSameVersion_OneWinsWithoutLostUpdate()
    {
        var (manager, admin) = await CreateActorClientsAsync();
        var original = await CreateCustomerAsync(manager);
        var id = original.GetProperty("id").GetInt32();
        var rowVersion = GetRequiredString(original, "rowVersion");

        var results = await SendCompetingPutsAsync(
            manager,
            admin,
            $"/api/customers/{id}",
            new
            {
                rowVersion,
                type = "Individual",
                name = "Customer manager winner",
                sourceCode = "marketing",
                relationshipStatus = "InProgress",
            },
            new
            {
                type = "Individual",
                name = "Customer admin winner",
                sourceCode = "marketing",
                relationshipStatus = "Signed",
            },
            rowVersion);

        await AssertSingleWinnerAsync(results, "name", manager, $"/api/customers/{id}", rowVersion);
    }

    [Fact]
    public async Task Opportunity_TwoActorsUpdatingSameVersion_OneWinsWithoutLostUpdate()
    {
        var (manager, admin) = await CreateActorClientsAsync();
        var customer = await CreateCustomerAsync(manager);
        var customerId = customer.GetProperty("id").GetInt32();
        using var created = await manager.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Concurrent opportunity",
            customerId,
            estimatedValue = 100_000_000m,
            winProbability = 20,
        });
        created.EnsureSuccessStatusCode();
        var original = await ReadJsonAsync(created);
        var id = original.GetProperty("id").GetInt32();
        var rowVersion = GetRequiredString(original, "rowVersion");

        var results = await SendCompetingPutsAsync(
            manager,
            admin,
            $"/api/opportunities/{id}",
            new
            {
                rowVersion,
                name = "Opportunity manager winner",
                customerId,
                estimatedValue = 200_000_000m,
                winProbability = 40,
            },
            new
            {
                name = "Opportunity admin winner",
                customerId,
                estimatedValue = 300_000_000m,
                winProbability = 60,
            },
            rowVersion);

        await AssertSingleWinnerAsync(results, "name", manager, $"/api/opportunities/{id}", rowVersion);
    }

    [Fact]
    public async Task Quote_TwoActorsUpdatingSameVersion_OneWinsWithoutLostUpdate()
    {
        var (manager, admin) = await CreateActorClientsAsync();
        var customer = await CreateCustomerAsync(manager);
        var customerId = customer.GetProperty("id").GetInt32();
        using var opportunityResponse = await manager.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Quote concurrency opportunity",
            customerId,
            estimatedValue = 500_000_000m,
            winProbability = 40,
        });
        opportunityResponse.EnsureSuccessStatusCode();
        var opportunityId = (await ReadJsonAsync(opportunityResponse)).GetProperty("id").GetInt32();
        using var created = await manager.PostAsJsonAsync("/api/quotes", new
        {
            opportunityId,
            method = "Boq",
            items = new[]
            {
                new { name = "Concurrency item", unit = "lot", quantity = 1m, unitPrice = 500_000_000m },
            },
            discountPercent = 0m,
            vatPercent = 8m,
        });
        created.EnsureSuccessStatusCode();
        var original = await ReadJsonAsync(created);
        var id = original.GetProperty("id").GetInt32();
        var rowVersion = GetRequiredString(original, "rowVersion");

        var results = await SendCompetingPutsAsync(
            manager,
            admin,
            $"/api/quotes/{id}",
            new
            {
                rowVersion,
                items = new[]
                {
                    new { name = "Concurrency item", unit = "lot", quantity = 1m, unitPrice = 500_000_000m },
                },
                discountPercent = 0m,
                vatPercent = 8m,
                note = "Quote manager winner",
            },
            new
            {
                items = new[]
                {
                    new { name = "Concurrency item", unit = "lot", quantity = 1m, unitPrice = 500_000_000m },
                },
                discountPercent = 0m,
                vatPercent = 8m,
                note = "Quote admin winner",
            },
            rowVersion);

        await AssertSingleWinnerAsync(results, "note", manager, $"/api/quotes/{id}", rowVersion);
    }

    [Fact]
    public async Task Contract_TwoActorsUpdatingSameVersion_OneWinsWithoutLostUpdate()
    {
        var (manager, admin) = await CreateActorClientsAsync();
        var customer = await CreateCustomerAsync(manager);
        var customerId = customer.GetProperty("id").GetInt32();
        using var created = await manager.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            status = "Draft",
            value = 100_000_000m,
        });
        created.EnsureSuccessStatusCode();
        var original = await ReadJsonAsync(created);
        var id = original.GetProperty("id").GetInt32();
        var contractNumber = GetRequiredString(original, "contractNumber");
        var rowVersion = GetRequiredString(original, "rowVersion");

        var results = await SendCompetingPutsAsync(
            manager,
            admin,
            $"/api/contracts/{id}",
            new
            {
                rowVersion,
                contractNumber,
                customerId,
                status = "Draft",
                value = 200_000_000m,
                scopeOfWork = "Contract manager winner",
            },
            new
            {
                contractNumber,
                customerId,
                status = "Draft",
                value = 300_000_000m,
                scopeOfWork = "Contract admin winner",
            },
            rowVersion);

        await AssertSingleWinnerAsync(results, "scopeOfWork", manager, $"/api/contracts/{id}", rowVersion);
    }

    private async Task<(HttpClient Manager, HttpClient Admin)> CreateActorClientsAsync()
    {
        var manager = Factory.CreateClient();
        var admin = Factory.CreateClient();
        await AuthTestHelper.AuthenticateAsync(manager, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await AuthTestHelper.AuthenticateAsync(admin, AuthTestHelper.LoginAsAdminAsync);
        return (manager, admin);
    }

    private static async Task<JsonElement> CreateCustomerAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Concurrent customer " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0911" + Random.Shared.Next(100000, 999999),
                isPrimary = true,
            },
        });
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private static async Task<CompetingResults> SendCompetingPutsAsync(
        HttpClient manager,
        HttpClient admin,
        string url,
        object managerPayload,
        object adminPayload,
        string rowVersion)
    {
        using var managerRequest = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(managerPayload),
        };
        using var adminRequest = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(adminPayload),
        };
        adminRequest.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");

        var responses = await Task.WhenAll(
            manager.SendAsync(managerRequest),
            admin.SendAsync(adminRequest));
        var bodies = await Task.WhenAll(responses.Select(ReadJsonAsync));
        return new CompetingResults(responses, bodies);
    }

    private static async Task AssertSingleWinnerAsync(
        CompetingResults results,
        string valueProperty,
        HttpClient reader,
        string detailUrl,
        string staleRowVersion)
    {
        results.Responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var winnerIndex = Array.FindIndex(results.Responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflictIndex = Array.FindIndex(results.Responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var winnerValue = GetRequiredString(results.Bodies[winnerIndex], valueProperty);
        var winnerRowVersion = GetRequiredString(results.Bodies[winnerIndex], "rowVersion");
        winnerRowVersion.Should().NotBe(staleRowVersion);
        results.Responses[winnerIndex].Headers.ETag?.Tag.Should().Be($"\"{winnerRowVersion}\"");
        results.Bodies[conflictIndex].GetProperty("code").GetString().Should().Be("crm_concurrency_conflict");

        using var currentResponse = await reader.GetAsync(detailUrl);
        currentResponse.EnsureSuccessStatusCode();
        var current = await ReadJsonAsync(currentResponse);
        GetRequiredString(current, valueProperty).Should().Be(winnerValue);
        var currentRowVersion = GetRequiredString(current, "rowVersion");
        currentRowVersion.Should().Be(winnerRowVersion);
        currentResponse.Headers.ETag?.Tag.Should().Be($"\"{currentRowVersion}\"");

        foreach (var response in results.Responses)
        {
            response.Dispose();
        }
    }

    private static string GetRequiredString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString().Should().NotBeNullOrWhiteSpace().And.Subject!;

    private sealed record CompetingResults(HttpResponseMessage[] Responses, JsonElement[] Bodies);
}
