using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

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
    public async Task Drawings_LinkRetryConflictAndUnlink_PreserveReleasedDrawing()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        var item = await CreateIfcItemAsync(project, IfcReleaseStatus.Released);
        var initial = (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!;
        initial.Available.Should().ContainSingle(x => x.IfcReleaseItemId == item);
        initial.Linked.Should().BeEmpty();
        var request = new { version = initial.Version, ifcReleaseItemIds = new[] { item } };
        var saved = await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings", request);
        saved.EnsureSuccessStatusCode();
        var linked = (await saved.Content.ReadFromJsonAsync<ConstructionTaskDrawingsResponse>())!;
        linked.Linked.Should().ContainSingle(x => x.IfcReleaseItemId == item);
        linked.Version.Should().NotBe(initial.Version);
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings", request)).StatusCode.Should().Be(HttpStatusCode.OK);
        var stale = await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = initial.Version, ifcReleaseItemIds = Array.Empty<int>() });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ConstructionTaskDrawings.CountAsync(x => x.ConstructionTaskId == task))).Should().Be(1);
        var removed = await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = linked.Version, ifcReleaseItemIds = Array.Empty<int>() });
        removed.EnsureSuccessStatusCode();
        (await removed.Content.ReadFromJsonAsync<ConstructionTaskDrawingsResponse>())!.Linked.Should().BeEmpty();
        (await WithDbAsync(db => db.IfcReleaseItems.AnyAsync(x => x.Id == item))).Should().BeTrue();
        (await Client.GetAsync($"/api/construction-tasks/{task}/drawings/{item}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("cancelled")]
    [InlineData("other-project")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("negative")]
    [InlineData("too-many")]
    [InlineData("unreleased-drawing")]
    public async Task Drawings_InvalidSelection_IsRejectedWithoutMutation(string scenario)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        var releaseStatus = scenario == "draft" ? IfcReleaseStatus.Draft :
            scenario == "cancelled" ? IfcReleaseStatus.Cancelled : IfcReleaseStatus.Released;
        var item = await CreateIfcItemAsync(scenario == "other-project" ? await CreateProjectAsync() : project, releaseStatus);
        if (scenario == "unreleased-drawing")
            await WithDbAsync(async db =>
            {
                var drawing = await db.IfcReleaseItems.Include(x => x.ShopDrawing).SingleAsync(x => x.Id == item);
                drawing.ShopDrawing.Status = ShopDrawingStatus.Drafting;
                await db.SaveChangesAsync();
                return 0;
            });
        var initial = (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!;
        var ids = scenario switch
        {
            "duplicate" => new[] { item, item },
            "missing" => new[] { int.MaxValue },
            "negative" => new[] { -1 },
            "too-many" => Enumerable.Range(1, 201).ToArray(),
            _ => new[] { item },
        };
        var result = await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings", new { version = initial.Version, ifcReleaseItemIds = ids });
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ConstructionTaskDrawings.AnyAsync(x => x.ConstructionTaskId == task))).Should().BeFalse();
        (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!.Version.Should().Be(initial.Version);
    }

    [Theory]
    [InlineData("SALE", HttpStatusCode.Forbidden)]
    [InlineData("DESIGN", HttpStatusCode.Forbidden)]
    public async Task Drawings_ManageRequiresConstructionPermission(string role, HttpStatusCode expected)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, role));
        (await Client.PutAsJsonAsync("/api/construction-tasks/999999/drawings",
            new { version = Guid.NewGuid(), ifcReleaseItemIds = Array.Empty<int>() })).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Drawings_ProjectScopeAndUnauthenticatedAccess_AreEnforced()
    {
        (await Client.GetAsync("/api/construction-tasks/999999/drawings")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectForPmAsync(assignToPm: false);
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        (await Client.GetAsync($"/api/construction-tasks/{task}/drawings")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = Guid.NewGuid(), ifcReleaseItemIds = Array.Empty<int>() })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/construction-tasks/{task}/drawings/1/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> CreateIfcItemAsync(int projectId, IfcReleaseStatus status)
    {
        return await WithDbAsync(async db =>
        {
            var release = new IfcRelease
            {
                DesignProjectId = projectId,
                ReleaseNumber = $"IFC-{Guid.NewGuid():N}",
                Title = "Phát hành kết cấu móng nhà xưởng",
                Status = status,
                ReleaseDate = status == IfcReleaseStatus.Released ? new DateTime(2026, 6, 1) : null,
            };
            var item = new IfcReleaseItem
            {
                IfcRelease = release,
                ShopDrawing = new ShopDrawing
                {
                    DesignProjectId = projectId,
                    DrawingCode = $"KC-{Guid.NewGuid():N}",
                    Title = "Chi tiết cốt thép móng F1",
                    ConstructionItem = "Thi công móng nhà xưởng",
                    DisciplineCode = "structure",
                    Status = status == IfcReleaseStatus.Released ? ShopDrawingStatus.Released : ShopDrawingStatus.Approved,
                },
            };
            db.IfcReleaseItems.Add(item);
            await db.SaveChangesAsync();
            return item.Id;
        });
    }

    [Fact]
    public async Task Drawings_RevisionAtReleaseAndTaskDeletion_PreserveSources()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        var item = await CreateIfcItemAsync(project, IfcReleaseStatus.Released);
        await WithDbAsync(async db =>
        {
            var source = await db.IfcReleaseItems.SingleAsync(x => x.Id == item);
            var user = await db.Users.Select(x => x.Id).FirstAsync();
            db.DrawingRevisions.AddRange(
                new DrawingRevision
                {
                    TargetType = DrawingRevisionTargetType.ShopDrawing,
                    TargetId = source.ShopDrawingId,
                    RevisionNumber = 1,
                    CreatedAt = new DateTime(2026, 5, 31),
                    CreatedByUserId = user
                },
                new DrawingRevision
                {
                    TargetType = DrawingRevisionTargetType.ShopDrawing,
                    TargetId = source.ShopDrawingId,
                    RevisionNumber = 2,
                    CreatedAt = new DateTime(2026, 6, 2),
                    CreatedByUserId = user,
                    IsCurrent = true
                });
            await db.SaveChangesAsync();
            return 0;
        });
        var initial = (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!;
        initial.Available.Single().RevisionNumber.Should().Be(1);
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = initial.Version, ifcReleaseItemIds = new[] { item } })).EnsureSuccessStatusCode();
        (await Client.DeleteAsync($"/api/construction-tasks/{task}")).EnsureSuccessStatusCode();
        (await WithDbAsync(db => db.ConstructionTaskDrawings.AnyAsync(x => x.ConstructionTaskId == task))).Should().BeFalse();
        (await WithDbAsync(db => db.IfcReleaseItems.AnyAsync(x => x.Id == item))).Should().BeTrue();
    }

    [Fact]
    public async Task Drawings_ContentUsesLinkedIssuedFile_AndRejectsReplacement()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        var item = await CreateIfcItemAsync(project, IfcReleaseStatus.Draft);
        var drawingId = await WithDbAsync(async db =>
        {
            var drawing = await db.IfcReleaseItems.Include(x => x.ShopDrawing).ThenInclude(x => x.DesignProject).SingleAsync(x => x.Id == item);
            drawing.ShopDrawing.Status = ShopDrawingStatus.Drafting;
            drawing.ShopDrawing.DesignProject.CurrentStage = DesignProjectStage.ShopDrawing;
            await db.SaveChangesAsync();
            return drawing.ShopDrawingId;
        });
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("NICON issued foundation drawing"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "foundation-issued.pdf");
        (await Client.PostAsync($"/api/shop-drawings/{drawingId}/upload", form)).EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            var issued = await db.IfcReleaseItems.Include(x => x.ShopDrawing).Include(x => x.IfcRelease).SingleAsync(x => x.Id == item);
            issued.IfcRelease.Status = IfcReleaseStatus.Released;
            issued.IfcRelease.ReleaseDate = DateTime.UtcNow;
            issued.ShopDrawing.Status = ShopDrawingStatus.Released;
            await db.SaveChangesAsync();
        });
        var initial = (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!;
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = initial.Version, ifcReleaseItemIds = new[] { item } })).EnsureSuccessStatusCode();
        var content = await Client.GetAsync($"/api/construction-tasks/{task}/drawings/{item}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await content.Content.ReadAsStringAsync()).Should().Be("NICON issued foundation drawing");
        using var replacement = new MultipartFormDataContent();
        replacement.Add(new ByteArrayContent([1, 2, 3]), "file", "unissued.pdf");
        (await Client.PostAsync($"/api/shop-drawings/{drawingId}/upload", replacement)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await (await Client.GetAsync($"/api/construction-tasks/{task}/drawings/{item}/content")).Content.ReadAsStringAsync())
            .Should().Be("NICON issued foundation drawing");
    }

    [Fact]
    public async Task Drawings_ProjectDeletionImpactIncludesLinks_AndDeletesOwnedLinks()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project, "2026-06-01", "2026-06-10");
        var item = await CreateIfcItemAsync(project, IfcReleaseStatus.Released);
        var initial = (await Client.GetFromJsonAsync<ConstructionTaskDrawingsResponse>($"/api/construction-tasks/{task}/drawings"))!;
        var before = (await Client.GetFromJsonAsync<DeletionImpactResponse>($"/api/design-projects/{project}/deletion-impact"))!;
        var operationalProjectId = await WithDbAsync(db => db.DesignProjects.Where(x => x.Id == project)
            .Select(x => x.OperationalProjectId).SingleAsync());
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{task}/drawings",
            new { version = initial.Version, ifcReleaseItemIds = new[] { item } })).EnsureSuccessStatusCode();
        var after = (await Client.GetFromJsonAsync<DeletionImpactResponse>($"/api/design-projects/{project}/deletion-impact"))!;
        after.Items.Should().ContainSingle(x => x.Key == "design.taskDrawings" && x.Count == 1 && x.Action == "Delete");
        after.PlanToken.Should().NotBe(before.PlanToken);
        using var stale = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/design-projects/{project}")
        {
            Content = JsonContent.Create(new { planToken = before.PlanToken, confirmation = before.RequiredConfirmation }),
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ConstructionTaskDrawings.AnyAsync(x => x.ConstructionTaskId == task))).Should().BeTrue();
        (await DeleteWithImpactAsync($"/api/design-projects/{project}")).EnsureSuccessStatusCode();
        (await WithDbAsync(db => db.ConstructionTaskDrawings.AnyAsync(x => x.ConstructionTaskId == task))).Should().BeFalse();
        (await WithDbAsync(db => db.IfcReleaseItems.AnyAsync(x => x.Id == item))).Should().BeFalse();
        (await WithDbAsync(db => db.OperationalProjects.AnyAsync(x => x.Id == operationalProjectId)))
            .Should().BeTrue("the independent Operational Project is preserved");
    }

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
    public async Task Delete_TaskWithSuccessor_RemovesEdgesAndNullsAcceptanceLink()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        })).EnsureSuccessStatusCode();
        var acceptanceId = await WithDbAsync(async db =>
        {
            var record = new AcceptanceRecord
            {
                DesignProjectId = projectId,
                ConstructionTaskId = a,
                AcceptanceCode = $"A-{Guid.NewGuid():N}"[..20],
                Title = "Task acceptance",
                AcceptanceDate = new DateOnly(2026, 6, 5),
                Status = AcceptanceStatus.Approved,
            };
            db.AcceptanceRecords.Add(record);
            await db.SaveChangesAsync();
            return record.Id;
        });

        (await Client.DeleteAsync($"/api/construction-tasks/{a}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/construction-tasks/{b}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.AcceptanceRecords
            .Where(record => record.Id == acceptanceId)
            .Select(record => record.ConstructionTaskId)
            .SingleAsync())).Should().BeNull();
        (await WithDbAsync(db => db.ConstructionTaskDependencies.AnyAsync(edge =>
            edge.TaskId == a || edge.PredecessorTaskId == a))).Should().BeFalse();
    }

    [Fact]
    public async Task BulkDelete_CollapsesInternalEdges_ReportsMissingId()
    {
        // A -> B, plus isolated C. When all three are selected the whole
        // dependency chain must go — the service must not block A just
        // because B (also being deleted) still depends on it.
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
        body.GetProperty("deleted").GetInt32().Should().Be(3); // a + b + c collapse together
        var failures = body.GetProperty("failures");
        failures.GetArrayLength().Should().Be(1);
        failures[0].GetProperty("id").GetInt32().Should().Be(999_999);
    }

    [Fact]
    public async Task BulkDelete_RemovesExternalEdgeAndPreservesDependentTask()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var projectId = await CreateProjectAsync();
        var a = await CreateTaskAsync(projectId, "2026-06-01", "2026-06-05");
        var b = await CreateTaskAsync(projectId, "2026-06-06", "2026-06-10");
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{b}/predecessors", new
        {
            predecessorTaskIds = new[] { a },
        })).EnsureSuccessStatusCode();

        var res = await Client.PostAsJsonAsync("/api/construction-tasks/bulk-delete", new
        {
            ids = new[] { a, 999_999 },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("deleted").GetInt32().Should().Be(1);
        body.GetProperty("failures").GetArrayLength().Should().Be(1);
        (await Client.GetAsync($"/api/construction-tasks/{b}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await WithDbAsync(db => db.ConstructionTaskDependencies.AnyAsync(edge =>
            edge.TaskId == a || edge.PredecessorTaskId == a))).Should().BeFalse();
    }

    [Fact]
    public async Task Get_UnknownId_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        (await Client.GetAsync("/api/construction-tasks/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pm_ListAndDetail_AreRestrictedToAccessibleProjects()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SUPER_ADMIN"));
        var accessible = await CreateProjectForPmAsync(assignToPm: true);
        var inaccessible = await CreateProjectForPmAsync(assignToPm: false);
        var accessibleTask = await CreateTaskAsync(accessible, "2026-06-01", "2026-06-05");
        var inaccessibleTask = await CreateTaskAsync(inaccessible, "2026-06-01", "2026-06-05");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));

        var list = await Client.GetAsync("/api/construction-tasks?pageSize=200");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt32());
        ids.Should().Contain(accessibleTask).And.NotContain(inaccessibleTask);
        (await Client.GetAsync($"/api/construction-tasks/{inaccessibleTask}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PutAsJsonAsync($"/api/construction-tasks/{inaccessibleTask}", new
        {
            name = "Unauthorized update",
            plannedStart = "2026-06-01",
            plannedEnd = "2026-06-05",
            progressPercent = 0,
            status = "Planned",
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.ConstructionTasks
            .Where(item => item.Id == inaccessibleTask)
            .Select(item => item.Name)
            .SingleAsync())).Should().NotBe("Unauthorized update");
    }

    // -------- helpers --------

    private async Task<int> CreateProjectAsync()
    {
        var customerId = await FirstCustomerIdAsync();
        var operationalProjectId = await CreateOperationalProjectAsync(customerId);
        var res = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Gantt fixture {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        res.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateProjectForPmAsync(bool assignToPm)
    {
        var customerId = await FirstCustomerIdAsync();
        var pmId = await WithDbAsync(db => db.Users
            .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(item => item.Id)
            .SingleAsync());
        var operationalProjectId = await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-GANTT-SCOPE-{Guid.NewGuid():N}"[..40],
                Name = "Construction scope fixture",
                CustomerId = customerId,
                ProjectManagerUserId = assignToPm ? pmId : null,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
        var response = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = $"Gantt scope {Guid.NewGuid():N}",
            customerId,
            operationalProjectId,
        });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateOperationalProjectAsync(int customerId)
    {
        return await WithDbAsync<int>(async db =>
        {
            var project = new OperationalProject
            {
                Code = $"PJ-GANTT-{Guid.NewGuid():N}"[..40],
                Name = "Construction task integration fixture",
                CustomerId = customerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
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
