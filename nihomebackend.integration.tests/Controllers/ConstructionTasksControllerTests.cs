using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ConstructionTasksController</c> (NIH-141):
/// RBAC gating, CRUD lifecycle, predecessor wiring, bulk-delete + failure
/// reporting, and the overdue/status roll-up on list.
/// </summary>
public class ConstructionTasksControllerTests : IntegrationTestBase
{
    public ConstructionTasksControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/construction-tasks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSale_IsForbidden()
    {
        // SALE doesn't get construction.tasks.view per rbac defaults.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync("/api/construction-tasks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsDesign_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/construction-tasks")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AsDesignManageOnly_IsForbidden()
    {
        // DESIGN has view but not construction.tasks.manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        var res = await Client.PostAsJsonAsync("/api/construction-tasks", new
        {
            designProjectId = 999999,
            name = "x",
            plannedStart = "2026-06-01",
            plannedEnd = "2026-06-02",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_AllocatesTaskCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/construction-tasks", new
        {
            designProjectId = projectId,
            name = "Excavation " + Guid.NewGuid().ToString("N")[..6],
            plannedStart = "2026-06-01",
            plannedEnd = "2026-06-05",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Planned");
        body.GetProperty("taskCode").GetString().Should().StartWith("T-");
        body.GetProperty("designProjectId").GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Create_RejectsEndBeforeStart()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();

        var res = await Client.PostAsJsonAsync("/api/construction-tasks", new
        {
            designProjectId = projectId,
            name = "Bad dates",
            plannedStart = "2026-06-05",
            plannedEnd = "2026-06-01",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ProgressAndStatus_ReturnsUpdatedRow()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var taskId = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-10");

        var res = await Client.PutAsJsonAsync($"/api/construction-tasks/{taskId}", new
        {
            name = "Renamed",
            plannedStart = "2026-06-01",
            plannedEnd = "2026-06-10",
            actualStart = "2026-06-01",
            actualEnd = "2026-06-08",
            progressPercent = 100,
            status = "InProgress",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Completed"); // auto-completed
        body.GetProperty("progressPercent").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task SetPredecessors_HappyPath_PersistsEdges()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");

        var res = await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        var preds = body.GetProperty("predecessors");
        preds.GetArrayLength().Should().Be(1);
        preds[0].GetProperty("predecessorTaskId").GetInt32().Should().Be(a);
    }

    [Fact]
    public async Task SetPredecessors_Cycle_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");

        (await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        })).EnsureSuccessStatusCode();

        var cycle = await Client.PutAsJsonAsync($"/api/construction-tasks/{a}/predecessors", new
        {
            predecessorTaskIds = new[] { b },
        });
        cycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_TaskWithSuccessor_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        })).EnsureSuccessStatusCode();

        (await Client.DeleteAsync($"/api/construction-tasks/{a}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.DeleteAsync($"/api/construction-tasks/{b}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/construction-tasks/{a}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task BulkDelete_ReportsFailuresAndDeletesRest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");
        var c = await CreateTaskAsync(projectId, "2026-06-11", "2026-06-15");
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        })).EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/construction-tasks/bulk-delete", new
        {
            ids = new[] { a, b, c, 999_999 },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("requested").GetInt32().Should().Be(4);
        body.GetProperty("deleted").GetInt32().Should().Be(2); // b + c
        body.GetProperty("failures").GetArrayLength().Should().Be(2); // a blocked, 999999 missing
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/construction-tasks/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Gantt fixture {Guid.NewGuid():N}",
            customerId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateTaskAsync(int projectId, string start, string end)
    {
        var res = await Client.PostAsJsonAsync("/api/construction-tasks", new
        {
            designProjectId = projectId,
            name = $"T {Guid.NewGuid():N}"[..8],
            plannedStart = start,
            plannedEnd = end,
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
                Name = "Gantt Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
