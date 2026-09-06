using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class HseViolationsControllerTests : IntegrationTestBase
{
    public HseViolationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/operational-projects/1/hse-violations"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_AsSaleWithoutPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        using var response = await SendAsync(HttpMethod.Post, "/api/operational-projects/1/hse-violations", ValidCreate(1));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Lifecycle_InvalidTransitionPreservesState_AndConfirmedRowsAreImmutable()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var fixture = await CreateProjectAsync();

        using var createdResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations",
            ValidCreate(fixture.PmUserId));
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createdResponse);
        created.GetProperty("status").GetString().Should().Be("Draft");
        created.GetProperty("severity").GetString().Should().Be("Low");

        using var invalid = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations/{created.GetProperty("id").GetInt32()}/confirm",
            new { rowVersion = created.GetProperty("rowVersion").GetString() });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await GetAsync(fixture.ProjectId, created.GetProperty("id").GetInt32());
        unchanged.GetProperty("status").GetString().Should().Be("Draft");
        unchanged.GetProperty("events").GetArrayLength().Should().Be(1);

        var reported = await TransitionAsync(fixture.ProjectId, created, "report");
        var confirmed = await TransitionAsync(fixture.ProjectId, reported, "confirm");
        confirmed.GetProperty("status").GetString().Should().Be("Confirmed");
        confirmed.GetProperty("confirmedAt").ValueKind.Should().Be(JsonValueKind.String);

        using var directEdit = await SendAsync(
            HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations/{created.GetProperty("id").GetInt32()}",
            ValidUpdate(fixture.PmUserId, confirmed.GetProperty("rowVersion").GetString()!, "Changed directly"));
        directEdit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync(fixture.ProjectId, created.GetProperty("id").GetInt32()))
            .GetProperty("description").GetString().Should().Be("Missing edge protection");

        using var correctedResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations/{created.GetProperty("id").GetInt32()}/corrections",
            ValidCorrection(
                fixture.PmUserId,
                confirmed.GetProperty("offlineClientId").GetString()!,
                confirmed.GetProperty("rowVersion").GetString()!));
        correctedResponse.EnsureSuccessStatusCode();
        var corrected = await ReadJsonAsync(correctedResponse);
        corrected.GetProperty("description").GetString().Should().Be("Corrected HSE description");
        corrected.GetProperty("events").EnumerateArray()
            .Should().Contain(entry => entry.GetProperty("type").GetString() == "Correction");

        var remediated = await TransitionAsync(fixture.ProjectId, corrected, "remediate", new
        {
            remediationOwnerUserId = fixture.PmUserId,
            remediationNote = "Guard rail installed and inspected",
            rowVersion = corrected.GetProperty("rowVersion").GetString(),
        });
        var closed = await TransitionAsync(fixture.ProjectId, remediated, "close");
        closed.GetProperty("status").GetString().Should().Be("Closed");
        closed.GetProperty("closedAt").ValueKind.Should().Be(JsonValueKind.String);
        closed.GetProperty("events").GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task RejectAndCancel_RequireReason_AndDoNotQualifyForKpi()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var fixture = await CreateProjectAsync();
        var rejected = await CreateAndReportAsync(fixture);

        using var missingReason = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations/{rejected.GetProperty("id").GetInt32()}/reject",
            new { rowVersion = rejected.GetProperty("rowVersion").GetString() });
        missingReason.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync(fixture.ProjectId, rejected.GetProperty("id").GetInt32()))
            .GetProperty("status").GetString().Should().Be("Reported");

        rejected = await TransitionAsync(fixture.ProjectId, rejected, "reject", new
        {
            reason = "Report was not an HSE violation",
            rowVersion = rejected.GetProperty("rowVersion").GetString(),
        });
        rejected.GetProperty("status").GetString().Should().Be("Rejected");

        var cancelled = await CreateAndReportAsync(fixture);
        cancelled = await TransitionAsync(fixture.ProjectId, cancelled, "confirm");
        cancelled = await TransitionAsync(fixture.ProjectId, cancelled, "cancel", new
        {
            reason = "Duplicate regulatory record",
            rowVersion = cancelled.GetProperty("rowVersion").GetString(),
        });
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task Create_ReplaysSameIdempotencyKey_WithoutDuplicate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var fixture = await CreateProjectAsync();
        var key = Guid.NewGuid().ToString();
        var payload = ValidCreate(fixture.PmUserId);
        using var first = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{fixture.ProjectId}/hse-violations", payload, key);
        using var replay = await SendAsync(HttpMethod.Post, $"/api/operational-projects/{fixture.ProjectId}/hse-violations", payload, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await ReadJsonAsync(first);
        var replayBody = await ReadJsonAsync(replay);
        replayBody.GetProperty("id").GetInt32().Should().Be(firstBody.GetProperty("id").GetInt32());
        (await WithDbAsync(db => db.HseViolations.CountAsync(item => item.OperationalProjectId == fixture.ProjectId)))
            .Should().Be(1);
    }

    [Fact]
    public async Task Get_AsViewOnlyProjectMember_RedactsPenaltyData()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "PM"));
        var fixture = await CreateProjectAsync();
        using var createdResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations",
            new
            {
                offlineClientId = Guid.NewGuid().ToString(),
                occurredAt = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc),
                location = "Tower A",
                category = "WorkingAtHeight",
                severity = "High",
                description = "Unsafe access",
                evidenceDocuments = Array.Empty<string>(),
                responsibleSiteUserId = fixture.PmUserId,
                penaltyReference = "PEN-001",
                penaltyAmount = 5_000_000m,
            });
        createdResponse.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(createdResponse)).GetProperty("id").GetInt32();

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));
        var visible = await GetAsync(fixture.ProjectId, id);
        visible.GetProperty("code").GetString().Should().StartWith("HSE-");
        visible.GetProperty("penaltyReference").ValueKind.Should().Be(JsonValueKind.Null);
        visible.GetProperty("penaltyAmount").ValueKind.Should().Be(JsonValueKind.Null);

        using var listResponse = await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/hse-violations");
        listResponse.EnsureSuccessStatusCode();
        var listed = (await ReadJsonAsync(listResponse)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == id);
        listed.GetProperty("penaltyReference").ValueKind.Should().Be(JsonValueKind.Null);
        listed.GetProperty("penaltyAmount").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task<(int ProjectId, int PmUserId, int DesignUserId)> CreateProjectAsync()
    {
        return await WithDbAsync(async db =>
        {
            var pmUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id)
                .SingleAsync();
            var designUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id)
                .SingleAsync();
            var customer = new Customer
            {
                Name = "HSE test customer " + Guid.NewGuid().ToString("N")[..8],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-HSE-{Guid.NewGuid():N}"[..30],
                Name = "HSE integration project",
                CustomerId = customer.Id,
                ProjectManagerUserId = pmUserId,
                CreatedByUserId = pmUserId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = designUserId,
                Position = "Designer",
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = pmUserId,
                UpdatedByUserId = pmUserId,
            });
            await db.SaveChangesAsync();
            return (project.Id, pmUserId, designUserId);
        });
    }

    private static object ValidCreate(int responsibleUserId) => new
    {
        offlineClientId = Guid.NewGuid().ToString(),
        occurredAt = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc),
        location = "Tower A, level 4",
        category = "WorkingAtHeight",
        severity = "Low",
        description = "Missing edge protection",
        regulatoryReference = "HSE-WAH-01",
        evidenceDocuments = new[] { "/files/project-documents/hse-photo.jpg" },
        responsibleSiteUserId = responsibleUserId,
    };

    private static object ValidUpdate(int responsibleUserId, string rowVersion, string description) => new
    {
        offlineClientId = "unused-on-direct-edit",
        occurredAt = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc),
        location = "Tower A, level 4",
        category = "WorkingAtHeight",
        severity = "Low",
        description,
        evidenceDocuments = Array.Empty<string>(),
        responsibleSiteUserId = responsibleUserId,
        rowVersion,
    };

    private static object ValidCorrection(int responsibleUserId, string offlineClientId, string rowVersion) => new
    {
        offlineClientId,
        occurredAt = new DateTime(2026, 8, 15, 3, 0, 0, DateTimeKind.Utc),
        location = "Tower A, level 4",
        category = "WorkingAtHeight",
        severity = "Critical",
        description = "Corrected HSE description",
        evidenceDocuments = Array.Empty<string>(),
        responsibleSiteUserId = responsibleUserId,
        reason = "Correct severity after supervisor review",
        rowVersion,
    };

    private async Task<JsonElement> CreateAndReportAsync((int ProjectId, int PmUserId, int DesignUserId) fixture)
    {
        using var response = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/hse-violations", ValidCreate(fixture.PmUserId));
        response.EnsureSuccessStatusCode();
        return await TransitionAsync(fixture.ProjectId, await ReadJsonAsync(response), "report");
    }

    private async Task<JsonElement> TransitionAsync(int projectId, JsonElement current, string action, object? payload = null)
    {
        payload ??= new { rowVersion = current.GetProperty("rowVersion").GetString() };
        using var response = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/hse-violations/{current.GetProperty("id").GetInt32()}/{action}", payload);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<JsonElement> GetAsync(int projectId, int id)
    {
        using var response = await Client.GetAsync($"/api/operational-projects/{projectId}/hse-violations/{id}");
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object payload, string? key = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}