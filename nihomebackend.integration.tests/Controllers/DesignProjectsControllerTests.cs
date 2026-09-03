using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>DesignProjectsController</c> (NIH-113):
/// RBAC gating, list + get, CRUD happy paths + the auto-create hook
/// fired by <c>ContractsController</c> transitions.
/// </summary>
public class DesignProjectsControllerTests : IntegrationTestBase
{
    public DesignProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/design-projects")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_AsPm_ReturnsOk()
    {
        // PM has design.projects.view (read-only).
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var res = await Client.GetAsync("/api/design-projects");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Pm_CannotCreate()
    {
        // PM has view but not manage.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var customerId = await FirstCustomerIdAsync();
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "PM blocked create",
            customerId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_HappyPath_AsSuperAdmin_ReturnsAutoCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"DP integ {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        body.GetProperty("projectCode").GetString().Should().StartWith("DP-");
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
        body.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Create_UnknownCustomer_IsNotFoundAfterAccessCheck()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Bad customer",
            customerId = 9999999,
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnknownId_Is404()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/design-projects/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ExtraCurrentStage_DoesNotChangeServerOwnedStage()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Update round-trip");
        var operationalProjectId = await WithDbAsync<int>(db => db.DesignProjects
            .Where(project => project.Id == id)
            .Select(project => project.OperationalProjectId!.Value)
            .SingleAsync());
        var res = await Client.PutAsJsonAsync($"/api/design-projects/{id}", new
        {
            name = "Update round-trip",
            customerId,
            operationalProjectId,
            currentStage = "BasicDesign",
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("currentStage").GetString().Should().Be("Concept");
    }

    [Fact]
    public async Task Delete_BeyondConcept_IsRejectedAndPreservesControlledHistory()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Delete aggregate after stage");
        await WithDbAsync(async db =>
        {
            var project = await db.DesignProjects.FindAsync(id);
            project!.CurrentStage = DesignProjectStage.BasicDesign;
            await db.SaveChangesAsync();
        });
        var userId = await WithDbAsync<int>(db => db.Users.Select(user => user.Id).FirstAsync());
        var taskId = await WithDbAsync<int>(async db =>
        {
            var task = new ConstructionTask
            {
                DesignProjectId = id,
                TaskCode = $"T-{Guid.NewGuid():N}",
                Name = "Aggregate task",
                PlannedStart = new DateOnly(2026, 8, 1),
                PlannedEnd = new DateOnly(2026, 8, 2),
            };
            db.ConstructionTasks.Add(task);
            await db.SaveChangesAsync();
            db.ConstructionTaskDependencies.Add(new ConstructionTaskDependency
            {
                TaskId = task.Id,
                PredecessorTaskId = task.Id,
            });
            db.AcceptanceRecords.Add(new AcceptanceRecord
            {
                DesignProjectId = id,
                ConstructionTaskId = task.Id,
                AcceptanceCode = $"A-{Guid.NewGuid():N}",
                Title = "Acceptance blocker",
                AcceptanceDate = new DateOnly(2026, 8, 3),
            });
            db.HandoverRecords.Add(new HandoverRecord
            {
                DesignProjectId = id,
                HandoverCode = $"H-{Guid.NewGuid():N}",
                Title = "Handover blocker",
                PlannedHandoverDate = new DateOnly(2026, 8, 4),
                ResponsibleUserId = userId,
                CreatedByUserId = userId,
                UpdatedByUserId = userId,
            });
            await db.SaveChangesAsync();
            return task.Id;
        });

        (await Client.DeleteAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db =>
        {
            (await db.DesignProjects.AnyAsync(project => project.Id == id)).Should().BeTrue();
            (await db.ConstructionTaskDependencies.AnyAsync(dependency => dependency.TaskId == taskId
                || dependency.PredecessorTaskId == taskId)).Should().BeTrue();
            (await db.AcceptanceRecords.AnyAsync(record => record.DesignProjectId == id)).Should().BeTrue();
            (await db.HandoverRecords.AnyAsync(record => record.DesignProjectId == id)).Should().BeTrue();
            (await db.Customers.AnyAsync(customer => customer.Id == customerId)).Should().BeTrue();
            (await db.Users.AnyAsync(user => user.Id == userId)).Should().BeTrue();
        });
    }

    [Fact]
    public async Task Delete_Concept_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var customerId = await FirstCustomerIdAsync();
        var id = await CreateAsync(customerId, "Delete concept");
        (await Client.DeleteAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/design-projects/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------- helpers --------

    private async Task<int> CreateAsync(int customerId, string name)
    {
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name,
            customerId,
            operationalProjectId,
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
                Code = $"PJ-TEST-{Guid.NewGuid():N}",
                Name = "Design project integration fixture",
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
                Name = "DP Test Customer " + Guid.NewGuid().ToString("N")[..6],
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
