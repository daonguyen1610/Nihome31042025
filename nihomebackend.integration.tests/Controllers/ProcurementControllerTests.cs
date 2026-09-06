using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ProcurementControllerTests : IntegrationTestBase
{
    public ProcurementControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task BoqLifecycle_ApprovesImmutableRevision()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var created = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions", new
        {
            currency = "VND",
            lines = new[] { new { itemCode = "MAT-001", description = "Concrete", unit = "m3", approvedQuantity = 10m, budgetUnitPrice = 1_000m } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await ReadJsonAsync(created);
        var id = createdJson.GetProperty("id").GetInt32();

        var invalidDecision = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision", new
        {
            approved = true,
            rowVersion = createdJson.GetProperty("rowVersion").GetString(),
        });
        invalidDecision.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id)))
            .Status.Should().Be(ProjectBoqRevisionStatus.Draft);

        var submitted = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/submit", new
        {
            rowVersion = createdJson.GetProperty("rowVersion").GetString(),
        });
        submitted.EnsureSuccessStatusCode();
        var submittedJson = await ReadJsonAsync(submitted);
        var approved = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}/decision", new
        {
            approved = true,
            rowVersion = submittedJson.GetProperty("rowVersion").GetString(),
        });
        approved.EnsureSuccessStatusCode();
        var approvedJson = await ReadJsonAsync(approved);
        approvedJson.GetProperty("status").GetString().Should().Be("Approved");
        approvedJson.GetProperty("costTotal").GetDecimal().Should().Be(10_000m);

        var updateApproved = await SendAsync(HttpMethod.Put, $"/api/operational-projects/{projectId}/procurement/boq-revisions/{id}", new
        {
            currency = "VND",
            rowVersion = approvedJson.GetProperty("rowVersion").GetString(),
            lines = new[] { new { itemCode = "MAT-001", description = "Changed", unit = "m3", approvedQuantity = 20m, budgetUnitPrice = 1_000m } },
        });
        updateApproved.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking().SingleAsync(item => item.Id == id)))
            .CostTotal.Should().Be(10_000m);
    }

    [Fact]
    public async Task Workspace_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/operational-projects/1/procurement"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VendorRating_ComponentAboveOneHundred_IsRejectedByApiContract()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{projectId}/procurement/vendor-ratings", new
        {
            contractId = 1,
            qualityScore = 101m,
            scheduleScore = 80m,
            costScore = 80m,
            hseScore = 80m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.VendorRatings.CountAsync())).Should().Be(0);
    }

    private async Task<int> CreateProjectAsync() => await WithDbAsync(async db =>
    {
        var pmUserId = await db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync();
        var customer = new Customer { Type = CustomerType.Company, Name = "Procurement Customer", SourceCode = "referral" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-PROC-{Guid.NewGuid():N}"[..30],
            Name = "Procurement Test Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = pmUserId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    });

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}