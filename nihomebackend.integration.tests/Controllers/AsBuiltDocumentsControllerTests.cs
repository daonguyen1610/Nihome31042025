using System.Net;
using System.Net.Http.Headers;
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
    public async Task Create_DuplicateTitleInSameProject_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var title = $"Duplicate {Guid.NewGuid():N}";

        var first = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title,
            category = "Drawing",
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title = $"  {title.ToUpperInvariant()}  ",
            category = "Drawing",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(duplicate);
        body.GetProperty("message").GetString().Should().Contain("đã tồn tại");
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
    public async Task Delete_Approved_HardDeletes()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var (projectId, id) = await CreateSubmittedAsync();
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{id}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/api/as-built-documents/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/as-built-documents/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/design-projects/{projectId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BulkDelete_AllStatuses_SkipsOnlyMissing()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var draftId = await CreateAsync(projectId);
        var (_, submittedId) = await CreateSubmittedAsync(projectId);
        (await Client.PostAsJsonAsync($"/api/as-built-documents/{submittedId}/approve", new { status = "Approved" }))
            .EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/as-built-documents/bulk-delete", new
        {
            ids = new[] { draftId, submittedId, 999_999 },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("deletedIds").EnumerateArray().Select(x => x.GetInt32())
            .Should().BeEquivalentTo(new[] { draftId, submittedId });
        body.GetProperty("skippedIds").EnumerateArray().Select(x => x.GetInt32())
            .Should().Equal(999_999);
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

    [Fact]
    public async Task Content_IsReadableOnlyThroughPersistedDocumentResource()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        using var form = FileForm("as-built evidence", "as-built.pdf");
        var upload = await Client.PostAsync("/api/business-documents/as-built", form);
        upload.EnsureSuccessStatusCode();
        var path = (await ReadJsonAsync(upload)).GetProperty("path").GetString()!;
        var projectId = await CreateProjectAsync();
        var create = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title = $"As-built content {Guid.NewGuid():N}",
            category = "Drawing",
            fileUrl = path,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var content = await Client.GetAsync($"/api/as-built-documents/{id}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("as-built evidence");
        (await Client.GetAsync("/api/as-built-documents/2147483647/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static MultipartFormDataContent FileForm(string content, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Export_preserves_filters_and_returns_csv()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        await CreateAsync(projectId, "Drawing");
        await CreateAsync(projectId, "TestReport");

        var response = await Client.GetAsync(
            $"/api/as-built-documents/export?designProjectId={projectId}&category=Drawing&sortBy=code&sortDirection=desc");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("Drawing");
        csv.Should().NotContain("TestReport");
    }

    [Fact]
    public async Task Submit_creates_admin_notification()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreateAsync(projectId);

        var response = await Client.PostAsJsonAsync(
            $"/api/as-built-documents/{id}/status",
            new { status = "Submitted" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notificationExists = await WithDbAsync(async db =>
            await db.Notifications.AnyAsync(notification =>
                notification.Module == "AsBuiltDocument"
                && notification.LinkUrl == "/admin/construction/asbuilt"));
        notificationExists.Should().BeTrue();
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
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var projectManagerUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id)
            .SingleAsync());
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"As-built fixture {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
            projectManagerUserId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-ASBUILT-{Guid.NewGuid():N}"[..40],
                Name = "As-built integration fixture",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
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
