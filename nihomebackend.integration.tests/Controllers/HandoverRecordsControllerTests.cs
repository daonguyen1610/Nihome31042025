using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class HandoverRecordsControllerTests : IntegrationTestBase
{
    public HandoverRecordsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/handover-records"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        (await Client.GetAsync("/api/handover-records"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateListAndDetail_AsDesign_EnforceProjectScope()
    {
        var designUserId = await UserIdForRoleAsync("DESIGN");
        var pmUserId = await UserIdForRoleAsync("PM");
        var assignedProjectId = await CreateProjectAsync(designLeadUserId: designUserId);
        var outOfScopeProjectId = await CreateProjectAsync();
        var assignedTitle = $"Design handover {Guid.NewGuid():N}";

        await AuthenticateAsAsync("SUPER_ADMIN");
        var outOfScope = await CreateHandoverAsync(outOfScopeProjectId, pmUserId,
            $"Out of scope {Guid.NewGuid():N}");

        await AuthenticateAsAsync("DESIGN");
        var create = await Client.PostAsJsonAsync("/api/handover-records", new
        {
            designProjectId = assignedProjectId,
            title = assignedTitle,
            plannedHandoverDate = "2026-09-15",
            responsibleUserId = designUserId,
            checklistItems = new[] { new { name = "Site walk", isCompleted = false } },
            documents = Array.Empty<string>(),
            signatories = Array.Empty<string>(),
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(create);
        var createdId = created.GetProperty("id").GetInt32();
        created.GetProperty("handoverCode").GetString().Should().StartWith("HO-");
        created.GetProperty("designProjectId").GetInt32().Should().Be(assignedProjectId);
        created.GetProperty("status").GetString().Should().Be("Draft");

        var list = await Client.GetAsync("/api/handover-records?pageSize=200");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await ReadJsonAsync(list);
        var visibleIds = listBody.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32()).ToList();
        visibleIds.Should().Contain(createdId).And.NotContain(outOfScope.GetProperty("id").GetInt32());

        var detail = await Client.GetAsync($"/api/handover-records/{createdId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(detail)).GetProperty("title").GetString().Should().Be(assignedTitle);
        (await Client.GetAsync($"/api/handover-records/{outOfScope.GetProperty("id").GetInt32()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_AsDesign_IsForbidden_ButAsPmSucceedsWhenReady()
    {
        var pmUserId = await UserIdForRoleAsync("PM");
        var projectId = await CreateProjectAsync(projectManagerUserId: pmUserId);

        await AuthenticateAsAsync("SUPER_ADMIN");
        var created = await CreateHandoverAsync(projectId, pmUserId,
            $"Ready handover {Guid.NewGuid():N}", commissioningCompleted: true,
            checklistCompleted: true, includeSignatory: true);
        var handoverId = created.GetProperty("id").GetInt32();
        await SeedCanonicalReadinessAsync(projectId, pmUserId);

        var ready = await Client.PostAsJsonAsync($"/api/handover-records/{handoverId}/status", new
        {
            status = "ReadyForHandover",
        });
        ready.EnsureSuccessStatusCode();

        await AuthenticateAsAsync("DESIGN");
        (await Client.PostAsJsonAsync($"/api/handover-records/{handoverId}/complete", new
        {
            status = "HandedOver",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsAsync("PM");
        var complete = await Client.PostAsJsonAsync($"/api/handover-records/{handoverId}/complete", new
        {
            status = "HandedOver",
            note = "Client accepted the handover.",
        });

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(complete);
        body.GetProperty("status").GetString().Should().Be("HandedOver");
        body.GetProperty("actualHandoverDate").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("readiness").GetProperty("isReady").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ReadyForHandover_WhenCommissioningMissing_ReturnsBadRequest()
    {
        var responsibleUserId = await UserIdForRoleAsync("PM");
        var projectId = await CreateProjectAsync(projectManagerUserId: responsibleUserId);
        await AuthenticateAsAsync("SUPER_ADMIN");
        var created = await CreateHandoverAsync(projectId, responsibleUserId,
            $"Incomplete handover {Guid.NewGuid():N}");

        var response = await Client.PostAsJsonAsync(
            $"/api/handover-records/{created.GetProperty("id").GetInt32()}/status", new
            {
                status = "ReadyForHandover",
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("message").GetString()
            .Should().Contain("Commissioning");
    }

    [Fact]
    public async Task Export_ReturnsUtf8CsvWithExpectedHeadersAndRecord()
    {
        var responsibleUserId = await UserIdForRoleAsync("PM");
        var projectId = await CreateProjectAsync();
        var uniqueTerm = $"=1+1 Handover {Guid.NewGuid():N}";
        await AuthenticateAsAsync("SUPER_ADMIN");
        var created = await CreateHandoverAsync(projectId, responsibleUserId, uniqueTerm);

        var response = await Client.GetAsync(
            $"/api/handover-records/export?search={Uri.EscapeDataString(uniqueTerm)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentType.CharSet.Should().Be("utf-8");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().Contain("handover-records-");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().StartWith(Encoding.UTF8.GetPreamble());
        var csv = Encoding.UTF8.GetString(bytes[Encoding.UTF8.GetPreamble().Length..]);
        csv.Should().StartWith("Code,Project,Title,Planned date,Actual date,Responsible,Status,Ready,Open punch items");
        csv.Should().Contain(created.GetProperty("handoverCode").GetString());
        csv.Should().Contain($"'{uniqueTerm}");
    }

    private Task AuthenticateAsAsync(string roleCode) =>
        AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, roleCode));

    private async Task<int> UserIdForRoleAsync(string roleCode)
    {
        var phone = roleCode switch
        {
            "SUPER_ADMIN" => TestDataSeeder.SuperAdminPhone,
            _ => TestDataSeeder.BusinessRolePhonesByCode[roleCode],
        };
        return await WithDbAsync(db => db.Users.Where(user => user.PhoneNumber == phone)
            .Select(user => user.Id).SingleAsync());
    }

    private async Task<int> CreateProjectAsync(int? projectManagerUserId = null, int? designLeadUserId = null)
    {
        return await WithDbAsync(async db =>
        {
            var customer = await db.Customers.OrderBy(item => item.Id).FirstOrDefaultAsync();
            if (customer is null)
            {
                customer = new Customer
                {
                    Name = $"Handover customer {Guid.NewGuid():N}",
                    SourceCode = "referral",
                    RelationshipStatus = CustomerRelationshipStatus.InProgress,
                    Type = CustomerType.Company,
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            var suffix = Guid.NewGuid().ToString("N")[..10];
            var project = new DesignProject
            {
                ProjectCode = $"HO-IT-{suffix}",
                Name = $"Handover integration {suffix}",
                CustomerId = customer.Id,
                ProjectManagerUserId = projectManagerUserId,
                DesignLeadUserId = designLeadUserId,
            };
            db.DesignProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private async Task<System.Text.Json.JsonElement> CreateHandoverAsync(
        int projectId,
        int responsibleUserId,
        string title,
        bool commissioningCompleted = false,
        bool checklistCompleted = false,
        bool includeSignatory = false)
    {
        var response = await Client.PostAsJsonAsync("/api/handover-records", new
        {
            designProjectId = projectId,
            title,
            plannedHandoverDate = "2026-09-15",
            responsibleUserId,
            commissioningCompleted,
            checklistItems = new[] { new { name = "Final inspection", isCompleted = checklistCompleted } },
            documents = Array.Empty<string>(),
            signatories = includeSignatory ? new[] { "Client representative" } : Array.Empty<string>(),
        });
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task SeedCanonicalReadinessAsync(int projectId, int actorUserId)
    {
        await WithDbAsync(async db =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            db.AcceptanceRecords.Add(new AcceptanceRecord
            {
                DesignProjectId = projectId,
                AcceptanceCode = $"A-{suffix}",
                Title = "Approved partial acceptance",
                AcceptanceDate = new DateOnly(2026, 9, 10),
                Status = AcceptanceStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                ApprovedByUserId = actorUserId,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId,
            });
            db.AsBuiltDocuments.AddRange(AsBuiltCategoryExtensions.Required.Select((category, index) =>
                new AsBuiltDocument
                {
                    DesignProjectId = projectId,
                    DocumentCode = $"AB-{suffix}-{index}",
                    Title = $"Approved {category}",
                    Category = category,
                    Status = AsBuiltStatus.Approved,
                    ApprovedAt = DateTime.UtcNow,
                    ApprovedByUserId = actorUserId,
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId,
                }));
            await db.SaveChangesAsync();
        });
    }
}