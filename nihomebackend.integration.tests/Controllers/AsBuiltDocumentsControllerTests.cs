using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>AsBuiltDocumentsController</c> (NIH-145):
/// RBAC gating, CRUD lifecycle, dual status / approve endpoints,
/// completeness roll-up, and bulk-delete rules.
/// </summary>
public class AsBuiltDocumentsControllerTests : IntegrationTestBase
{
    public AsBuiltDocumentsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/as-built-documents")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/as-built-documents")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/as-built-documents")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title = "Bản vẽ hoàn công " + Guid.NewGuid().ToString("N")[..6],
            category = "Drawing",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("documentCode").GetString().Should().StartWith("AB-");
        body.GetProperty("category").GetString().Should().Be("Drawing");
        body.GetProperty("designProjectId").GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Create_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var res = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = 1,
            title = "x",
            category = "Drawing",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Status_endpoint_rejects_Approved_pointsToApproveEndpoint()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();

        var res = await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/status", new
        {
            status = "Approved",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(res);
        body.GetProperty("message").GetString().Should().Contain("/approve");
    }

    [Fact]
    public async Task Approve_AsDesign_IsForbidden()
    {
        // DESIGN gets manage but NOT approve.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new
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
        var res = await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new
        {
            status = "Approved",
            note = "Đạt yêu cầu bàn giao.",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task Update_LockedAfterApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PutAsJsonAsync($"/api/as-built-documents/{id}", new
        {
            title = "Should be locked",
            category = "Drawing",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Archive_from_Approved_marks_ArchivedAt()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (_, id) = await CreateSubmittedAsync();
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/status", new
        {
            status = "Archived",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Archived");
        body.GetProperty("archivedAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task BulkDelete_SkipsApproved()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var draftId = await CreateAsync(projectId);
        var (_, submittedId) = await CreateSubmittedAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{submittedId}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/as-built-documents/bulk-delete", new
        {
            ids = new[] { draftId, submittedId },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("deletedIds").EnumerateArray().Select(x => x.GetInt32()).Should().Contain(draftId);
        body.GetProperty("skippedIds").EnumerateArray().Select(x => x.GetInt32()).Should().Contain(submittedId);
    }

    [Fact]
    public async Task List_completeness_reflects_approved_categories()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var drawId = await CreateAsync(projectId, "Drawing");
        var testId = await CreateAsync(projectId, "TestReport");
        foreach (var id in new[] { drawId, testId })
        {
            (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/status", new { status = "Submitted" })).EnsureSuccessStatusCode();
            (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new { status = "Approved" })).EnsureSuccessStatusCode();
        }

        var res = await Client.GetAsync($"/api/as-built-documents?designProjectId={projectId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("totalRequiredCategories").GetInt32().Should().Be(4);
        body.GetProperty("completedRequiredCategories").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/as-built-documents/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<(int projectId, int id)> CreateSubmittedAsync(int? projectId = null)
    {
        var pid = projectId ?? await CreateProjectAsync();
        var id = await CreateAsync(pid);
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/status", new { status = "Submitted" })).EnsureSuccessStatusCode();
        return (pid, id);
    }

    private async Task<int> CreateAsync(int projectId, string category = "Drawing")
    {
        var res = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title = $"Doc {Guid.NewGuid():N}"[..12],
            category,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"As-built fixture {Guid.NewGuid():N}",
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
                Name = "AsBuilt Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
