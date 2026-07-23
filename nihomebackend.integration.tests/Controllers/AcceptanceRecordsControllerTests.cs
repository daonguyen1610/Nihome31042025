using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>AcceptanceRecordsController</c> (NIH-143):
/// RBAC gating, CRUD lifecycle, dual status / approve endpoints,
/// revision counter and bulk-delete rules.
/// </summary>
public class AcceptanceRecordsControllerTests : IntegrationTestBase
{
    public AcceptanceRecordsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/acceptance-records")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/acceptance-records")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/acceptance-records")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/acceptance-records", new
        {
            designProjectId = projectId,
            title = "Nghiệm thu cột trục A " + Guid.NewGuid().ToString("N")[..6],
            acceptanceDate = "2026-06-15",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("acceptanceCode").GetString().Should().StartWith("A-");
        body.GetProperty("designProjectId").GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Create_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/acceptance-records", new
        {
            designProjectId = 1,
            title = "x",
            acceptanceDate = "2026-06-15",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_Endpoint_RejectsApproved_PointsToVerify()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, id) = await CreateSubmittedAsync();

        var res = await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/status", new
        {
            status = "Approved",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(res);
        body.GetProperty("message").GetString().Should().Contain("/approve");
        _ = projectId;
    }

    [Fact]
    public async Task Approve_AsDesign_IsForbidden()
    {
        // DESIGN gets manage but not approve.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/approve", new
        {
            status = "Approved",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_AsPm_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/approve", new
        {
            status = "Approved",
            resolutionNote = "Đạt yêu cầu.",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Approved");
        body.GetProperty("resolutionNote").GetString().Should().Be("Đạt yêu cầu.");
    }

    [Fact]
    public async Task Reject_Then_Revise_Increments_Counter()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();

        (await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/status", new
        {
            status = "Rejected",
            resolutionNote = "Fix rỗ",
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var revised = await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/status", new
        {
            status = "Draft",
        });
        revised.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(revised);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("revisionCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Update_LockedAfterApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();
        (await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PutAsJsonAsync($"/api/acceptance-records/{id}", new
        {
            title = "Should be locked",
            acceptanceDate = "2026-06-15",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkDelete_SkipsApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var draftId = await CreateAsync(projectId);
        var (_, submittedId) = await CreateSubmittedAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/acceptance-records/{submittedId}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/acceptance-records/bulk-delete", new
        {
            ids = new[] { draftId, submittedId },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("deletedIds").EnumerateArray().Select(x => x.GetInt32()).Should().Contain(draftId);
        body.GetProperty("skippedIds").EnumerateArray().Select(x => x.GetInt32()).Should().Contain(submittedId);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/acceptance-records/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<(int projectId, int id)> CreateSubmittedAsync(int? projectId = null)
    {
        var pid = projectId ?? await CreateProjectAsync();
        var id = await CreateAsync(pid);
        var res = await Client.PostAsJsonAsync($"/api/acceptance-records/{id}/status", new
        {
            status = "Submitted",
        });
        res.EnsureSuccessStatusCode();
        return (pid, id);
    }

    private async Task<int> CreateAsync(int projectId)
    {
        var res = await Client.PostAsJsonAsync("/api/acceptance-records", new
        {
            designProjectId = projectId,
            title = $"Nghiệm thu {Guid.NewGuid():N}"[..12],
            acceptanceDate = "2026-06-15",
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Acceptance fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> FirstCustomerIdAsync()
    {
        return await WithDbAsync<int>(async db =>
        {
            var existing = await db.Customers.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing != null) return existing.Id;
            var customer = new Customer
            {
                Name = "Acc Test Customer " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "referral",
                RelationshipStatus = CustomerRelationshipStatus.InProgress,
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }
}
