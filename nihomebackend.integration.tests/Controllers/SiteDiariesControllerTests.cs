using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>SiteDiariesController</c> (NIH-142):
/// RBAC gates (view / manage / confirm), CRUD lifecycle, one-per-day
/// guard, Draft → Submitted → Confirmed workflow and bulk delete.
/// </summary>
public class SiteDiariesControllerTests : IntegrationTestBase
{
    public SiteDiariesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/site-diaries")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/site-diaries")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/site-diaries")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AsDesignViewOnly_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsJsonAsync("/api/site-diaries", new
        {
            designProjectId = 1,
            diaryDate = "2026-07-01",
            weatherCode = "sunny",
            workPerformed = "x",
            headcountLabor = 0,
            headcountEngineers = 0,
            headcountSupervisors = 0,
            headcountSubcontractors = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_ReturnsDraft()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/site-diaries", new
        {
            designProjectId = projectId,
            diaryDate = "2026-07-01",
            weatherCode = "sunny",
            workPerformed = "Đổ móng trục A",
            headcountLabor = 20,
            headcountEngineers = 2,
            headcountSupervisors = 1,
            headcountSubcontractors = 3,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("headcountTotal").GetInt32().Should().Be(26);
        body.GetProperty("weatherLabel").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_DuplicateDate_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        await CreateDiaryAsync(projectId, "2026-07-02");

        var res = await Client.PostAsJsonAsync("/api/site-diaries", new
        {
            designProjectId = projectId,
            diaryDate = "2026-07-02",
            weatherCode = "sunny",
            workPerformed = "dup",
            headcountLabor = 1,
            headcountEngineers = 0,
            headcountSupervisors = 0,
            headcountSubcontractors = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LifecycleFlow_Submit_Confirm_Reopen()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreateDiaryAsync(projectId, "2026-07-03");

        (await Client.PostAsync($"/api/site-diaries/{id}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsync($"/api/site-diaries/{id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Cannot re-confirm.
        (await Client.PostAsync($"/api/site-diaries/{id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Reopen wipes the submit/confirm stamps.
        (await Client.PostAsync($"/api/site-diaries/{id}/reopen", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var get = await Client.GetAsync($"/api/site-diaries/{id}");
        (await ReadJsonAsync(get)).GetProperty("status").GetString().Should().Be("Draft");
    }

    [Fact]
    public async Task Confirm_AsSalesManager_IsForbidden()
    {
        // SALES_MANAGER has no construction permissions at all.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreateDiaryAsync(projectId, "2026-07-04");
        (await Client.PostAsync($"/api/site-diaries/{id}/submit", null)).EnsureSuccessStatusCode();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.PostAsync($"/api/site-diaries/{id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AfterSubmit_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var id = await CreateDiaryAsync(projectId, "2026-07-05");
        (await Client.PostAsync($"/api/site-diaries/{id}/submit", null)).EnsureSuccessStatusCode();

        var res = await Client.PutAsJsonAsync($"/api/site-diaries/{id}", new
        {
            diaryDate = "2026-07-05",
            weatherCode = "sunny",
            workPerformed = "no",
            headcountLabor = 1,
            headcountEngineers = 0,
            headcountSupervisors = 0,
            headcountSubcontractors = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkDelete_DeletesDraftAndReportsFailures()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateDiaryAsync(projectId, "2026-07-06");
        var b = await CreateDiaryAsync(projectId, "2026-07-07");
        var c = await CreateDiaryAsync(projectId, "2026-07-08");
        (await Client.PostAsync($"/api/site-diaries/{b}/submit", null)).EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/site-diaries/bulk-delete", new
        {
            ids = new[] { a, b, c, 999_999 },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("requested").GetInt32().Should().Be(4);
        body.GetProperty("deleted").GetInt32().Should().Be(2); // a + c
        body.GetProperty("failures").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/site-diaries/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Diary fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateDiaryAsync(int projectId, string diaryDate)
    {
        var res = await Client.PostAsJsonAsync("/api/site-diaries", new
        {
            designProjectId = projectId,
            diaryDate,
            weatherCode = "sunny",
            workPerformed = "Test work",
            headcountLabor = 10,
            headcountEngineers = 1,
            headcountSupervisors = 1,
            headcountSubcontractors = 0,
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
                Name = "Diary Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
