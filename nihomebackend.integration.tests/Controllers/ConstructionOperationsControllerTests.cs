using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ConstructionOperationsControllerTests : IntegrationTestBase
{
    public ConstructionOperationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ListResources_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/operational-projects/1/construction/resource-requests"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResourceReferences_ProjectQsCanReadWithoutProcurementPermission()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));

        using var response = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-references");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ReadJsonAsync(response);
        payload.TryGetProperty("boqLines", out _).Should().BeTrue();
        payload.TryGetProperty("siteUsers", out _).Should().BeTrue();
        payload.TryGetProperty("procurementUsers", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ResourceRequest_CreateReplaySubmitAndCmApproval_PreservesSingleRecord()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));
        var key = Guid.NewGuid().ToString("N");
        var payload = ValidEquipmentRequest();

        using var first = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            payload,
            key);
        using var replay = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests",
            payload,
            key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(first);
        var replayed = await ReadJsonAsync(replay);
        replayed.GetProperty("id").GetInt32().Should().Be(created.GetProperty("id").GetInt32());

        var submitted = await TransitionAsync(fixture.ProjectId, created, "submit");
        submitted.GetProperty("status").GetString().Should().Be("PendingCmReview");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        using var approvedResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{created.GetProperty("id").GetInt32()}/cm-decision",
            new
            {
                approved = true,
                reason = "Resource plan and construction schedule verified",
                rowVersion = submitted.GetProperty("rowVersion").GetString(),
            });
        approvedResponse.EnsureSuccessStatusCode();
        var approved = await ReadJsonAsync(approvedResponse);
        approved.GetProperty("status").GetString().Should().Be("CmApproved");
        approved.GetProperty("handoffStatus").GetString().Should().Be("ReadyForProcurementAndFinance");
        (await WithDbAsync(db => db.ConstructionResourceRequests.CountAsync(x => x.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task CmDecision_WhenReviewerIsAuthor_ReturnsBadRequestAndPreservesPendingState()
    {
        var fixture = await CreateProjectAsync();
        var requestId = await WithDbAsync(async db =>
        {
            var row = new ConstructionResourceRequest
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "RR-SOD-001",
                Type = ConstructionResourceType.Equipment,
                WbsCode = "WBS-01",
                NeedByDate = DateTime.UtcNow.AddDays(10),
                Quantity = 1,
                Unit = "set",
                Reason = "Tower crane for structural works",
                Status = ConstructionReviewStatus.PendingCmReview,
                CreatedByUserId = fixture.PmUserId,
            };
            db.ConstructionResourceRequests.Add(row);
            await db.SaveChangesAsync();
            return row.Id;
        });
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var rowVersion = await WithDbAsync(db => db.ConstructionResourceRequests
            .Where(x => x.Id == requestId)
            .Select(x => Convert.ToBase64String(x.RowVersion))
            .SingleAsync());

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/construction/resource-requests/{requestId}/cm-decision",
            new { approved = true, reason = "Self approval is forbidden", rowVersion });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionResourceRequests
            .Where(x => x.Id == requestId)
            .Select(x => x.Status)
            .SingleAsync())).Should().Be(ConstructionReviewStatus.PendingCmReview);
    }

    [Fact]
    public async Task UpdateQs_WithoutConcurrencyToken_ReturnsBadRequest()
    {
        var fixture = await CreateProjectAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "QS"));

        using var response = await SendAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/construction/qs-batches/999",
            new
            {
                direction = "Downstream",
                contractId = 1,
                wbsCode = "WBS-01",
                periodFrom = DateTime.UtcNow.Date,
                periodTo = DateTime.UtcNow.Date,
                certifiedAmount = 1m,
                description = "Progress certification",
                lines = new[] { new { itemCode = "QS-01", description = "Concrete", unit = "m3", quantity = 1m, unitPrice = 1m } },
                evidenceDocumentIds = Array.Empty<long>(),
            });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(int ProjectId, int PmUserId)> CreateProjectAsync() =>
        await WithDbAsync(async db =>
        {
            var pm = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"]);
            var qs = await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["QS"]);
            var customer = new Customer
            {
                Name = "Module 4 test customer " + Guid.NewGuid().ToString("N")[..8],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-M4-{Guid.NewGuid():N}"[..30],
                Name = "NICON construction operations test",
                CustomerId = customer.Id,
                ProjectManagerUserId = pm.Id,
                CreatedByUserId = pm.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = qs.Id,
                Position = "Site QS",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = pm.Id,
                UpdatedByUserId = pm.Id,
            });
            await db.SaveChangesAsync();
            return (project.Id, pm.Id);
        });

    private static object ValidEquipmentRequest() => new
    {
        type = "Equipment",
        wbsCode = "WBS-STR-01",
        needByDate = DateTime.UtcNow.AddDays(14),
        quantity = 2m,
        unit = "set",
        reason = "Tower cranes required by the approved structural resource plan",
    };

    private async Task<System.Text.Json.JsonElement> TransitionAsync(
        int projectId,
        System.Text.Json.JsonElement row,
        string action)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{projectId}/construction/resource-requests/{row.GetProperty("id").GetInt32()}/{action}",
            new { rowVersion = row.GetProperty("rowVersion").GetString() });
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object payload, string? key = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }
}
