using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class DetailDesignScheduleControllerTests : IntegrationTestBase
{
    public DetailDesignScheduleControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_EnforcesAuthenticationPermissionAndProjectMembership()
    {
        var projectId = await CreateProjectAsync(await UserIdAsync("DESIGN_LEAD"));

        (await Client.GetAsync($"/api/operational-projects/{projectId}/design-schedule"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthenticateAsync("USER");
        (await Client.GetAsync($"/api/operational-projects/{projectId}/design-schedule"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await AuthenticateAsync("PM");
        (await Client.GetAsync($"/api/operational-projects/{projectId}/design-schedule"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("ACCOUNTANT")]
    [InlineData("BGD")]
    public async Task PortfolioViewWithoutScheduleManage_IsReadOnly(string roleCode)
    {
        var fixture = await CreateFixtureAsync("DESIGN_LEAD");
        await AuthenticateAsync(roleCode);

        using var getResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(getResponse)).GetProperty("canManage").GetBoolean().Should().BeFalse();

        using var mutationResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize",
            InitializePayload(), $"read-only-{Guid.NewGuid():N}");
        mutationResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignedProjectManager_CanInitializeSchedule()
    {
        var fixture = await CreateFixtureAsync("PM");
        await AuthenticateAsync("PM");

        using var response = await InitializeAsync(fixture.ProjectId, InitializePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_CanInitializeExistingUnassignedProject()
    {
        var fixture = await CreateFixtureAsync("DESIGN_LEAD");
        await AuthenticateAsync("ADMIN");

        using var response = await InitializeAsync(fixture.ProjectId, InitializePayload());

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DesignLeadScopeMatrix_IsEnforcedThroughMutationApi()
    {
        var direct = await CreateFixtureAsync("PM");
        var designModule = await CreateFixtureAsync("PM");
        var disciplineOnly = await CreateFixtureAsync("PM");
        var expired = await CreateFixtureAsync("PM");
        var designLeadId = await UserIdAsync("DESIGN_LEAD");
        await WithDbAsync(async db =>
        {
            var directDesignProject = await db.DesignProjects.SingleAsync(item =>
                item.OperationalProjectId == direct.ProjectId);
            directDesignProject.DesignLeadUserId = designLeadId;

            foreach (var assignment in new[]
            {
                (designModule.ProjectId, ProjectRoleScope.Module, "Design", (DateTime?)null),
                (disciplineOnly.ProjectId, ProjectRoleScope.Discipline, "architecture", (DateTime?)null),
                (expired.ProjectId, ProjectRoleScope.Project, (string?)null, DateTime.UtcNow.AddMinutes(-1)),
            })
            {
                db.OperationalProjectMembers.Add(new OperationalProjectMember
                {
                    OperationalProjectId = assignment.ProjectId,
                    UserId = designLeadId,
                    Position = "Design Lead",
                    StartedAt = DateTime.UtcNow.AddDays(-1),
                    CreatedByUserId = designLeadId,
                    UpdatedByUserId = designLeadId,
                    Roles =
                    [
                        new OperationalProjectMemberRole
                        {
                            RoleCode = ProjectTeamRoleCode.DesignLead,
                            Scope = assignment.Item2,
                            ScopeValue = assignment.Item3,
                            StartedAt = DateTime.UtcNow.AddDays(-1),
                            EndedAt = assignment.Item4,
                        },
                    ],
                });
            }
            await db.SaveChangesAsync();
        });
        await AuthenticateAsync("DESIGN_LEAD");

        using var directResponse = await InitializeAsync(direct.ProjectId, InitializePayload());
        using var moduleResponse = await InitializeAsync(designModule.ProjectId, InitializePayload());
        using var disciplineResponse = await InitializeAsync(disciplineOnly.ProjectId, InitializePayload());
        using var expiredResponse = await InitializeAsync(expired.ProjectId, InitializePayload());

        directResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await directResponse.Content.ReadAsStringAsync());
        moduleResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await moduleResponse.Content.ReadAsStringAsync());
        disciplineResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.DesignSchedulePhases.CountAsync(item =>
            item.OperationalProjectId == disciplineOnly.ProjectId ||
            item.OperationalProjectId == expired.ProjectId))).Should().Be(0);
    }

    [Fact]
    public async Task Mutations_EnforceAuthenticationPermissionAndProjectManagement()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateFixtureAsync("DESIGN_LEAD");
        using var initializedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize",
            InitializePayload(), $"authorization-init-{Guid.NewGuid():N}");
        initializedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var phase = await GetPhaseAsync(fixture.ProjectId, "Concept");
        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}/tasks",
            TaskPayload("AUTH-001", fixture.MemberId, 0), $"authorization-create-{Guid.NewGuid():N}");
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = await GetTaskAsync(fixture.ProjectId, "AUTH-001");
        var historyCount = await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId));

        Client.DefaultRequestHeaders.Authorization = null;
        await AssertMutationStatusesAsync(HttpStatusCode.Unauthorized, fixture, phase, task);
        await AuthenticateAsync("USER");
        await AssertMutationStatusesAsync(HttpStatusCode.Forbidden, fixture, phase, task);
        await AuthenticateAsync("PM");
        await AssertMutationStatusesAsync(HttpStatusCode.NotFound, fixture, phase, task);

        await AuthenticateAsync("DESIGN_LEAD");
        using var designLeadUpdate = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{task.Id}",
            TaskPayload("AUTH-001", fixture.MemberId, 10, "InProgress",
                new DateOnly(2026, 9, 1), task.RowVersion),
            $"authorization-design-lead-{Guid.NewGuid():N}");
        designLeadUpdate.StatusCode.Should().Be(HttpStatusCode.OK,
            await designLeadUpdate.Content.ReadAsStringAsync());

        (await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId))).Should().Be(historyCount + 1);
    }

    [Fact]
    public async Task InitializeCreateUpdateAndFilter_ReturnsTraceableWeightedRollup()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateFixtureAsync();
        using var initializedResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize",
            InitializePayload(), $"schedule-init-{Guid.NewGuid():N}");
        initializedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await initializedResponse.Content.ReadAsStringAsync());
        var initialized = await ReadJsonAsync(initializedResponse);
        initialized.GetProperty("phases").GetArrayLength().Should().Be(3);
        var initializedPhases = initialized.GetProperty("phases").EnumerateArray().ToList();
        initializedPhases[0].GetProperty("plannedStart").GetString().Should().Be("2026-09-01");
        initializedPhases[^1].GetProperty("plannedEnd").GetString().Should().Be("2026-12-01");
        for (var index = 1; index < initializedPhases.Count; index++)
        {
            var previousEnd = DateOnly.Parse(initializedPhases[index - 1].GetProperty("plannedEnd").GetString()!);
            var currentStart = DateOnly.Parse(initializedPhases[index].GetProperty("plannedStart").GetString()!);
            currentStart.Should().Be(previousEnd.AddDays(1));
        }

        var phases = initialized.GetProperty("phases").EnumerateArray().ToDictionary(
            item => item.GetProperty("code").GetString()!, item => item.GetProperty("id").GetInt32());
        foreach (var phase in phases)
        {
            using var created = await SendJsonWithKeyAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Value}/tasks",
                TaskPayload($"{phase.Key}-001", fixture.MemberId, 0),
                $"schedule-task-{Guid.NewGuid():N}");
            created.StatusCode.Should().Be(HttpStatusCode.Created,
                await created.Content.ReadAsStringAsync());
        }

        var conceptTask = await GetTaskAsync(fixture.ProjectId, "Concept-001");
        using var updatedResponse = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{conceptTask.Id}",
            TaskPayload("Concept-001", fixture.MemberId, 75, "InProgress",
                new DateOnly(2026, 9, 1), conceptTask.RowVersion),
            $"schedule-update-{Guid.NewGuid():N}");
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            await updatedResponse.Content.ReadAsStringAsync());

        var filteredResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule" +
            "?phase=Concept&departmentCode=design&status=InProgress" +
            "&plannedFrom=2026-09-02&plannedTo=2026-09-03&page=1&pageSize=10");
        filteredResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var filtered = await ReadJsonAsync(filteredResponse);
        filtered.GetProperty("baselineReady").GetBoolean().Should().BeTrue();
        filtered.GetProperty("progressPercent").GetDecimal().Should().Be(25.5m);
        filtered.GetProperty("rollupPolicyVersion").GetString().Should().Be("design-schedule-weighted-v1");
        filtered.GetProperty("rollupSources").GetArrayLength().Should().Be(3);
        filtered.GetProperty("rollupSources")[0].GetProperty("taskSources")[0]
            .GetProperty("taskId").GetInt32().Should().Be(conceptTask.Id);
        filtered.GetProperty("tasks").GetProperty("totalCount").GetInt32().Should().Be(1);
        filtered.GetProperty("tasks").GetProperty("items")[0].GetProperty("code")
            .GetString().Should().Be("Concept-001");
        var history = await WithDbAsync(db => db.DesignScheduleHistory.AsNoTracking()
            .Where(item => item.OperationalProjectId == fixture.ProjectId)
            .Select(item => new { item.EntityType, item.Action })
            .ToListAsync());
        history.Should().HaveCount(7);
        history.Should().ContainEquivalentOf(new { EntityType = "Phase", Action = "Initialized" });
        history.Should().ContainEquivalentOf(new { EntityType = "Task", Action = "Created" });
        history.Should().ContainEquivalentOf(new { EntityType = "Task", Action = "Updated" });

        IReadOnlyList<AuditLog> auditRows = [];
        for (var attempt = 0; attempt < 20 && auditRows.Count < 5; attempt++)
        {
            await Task.Delay(250);
            auditRows = await WithDbAsync(db => db.AuditLogs.AsNoTracking()
                .Where(item => item.ResourceType == "DetailDesignSchedule" &&
                    item.ResourceId != null && item.ResourceId.StartsWith($"{fixture.ProjectId}:") &&
                    item.Action.StartsWith("design-schedule."))
                .ToListAsync());
        }
        auditRows.Should().HaveCount(5);
        auditRows.Should().ContainSingle(item => item.Action == "design-schedule.initialize");
        auditRows.Count(item => item.Action == "design-schedule.task.create").Should().Be(3);
        auditRows.Should().ContainSingle(item => item.Action == "design-schedule.task.update");
        auditRows.Should().OnlyContain(item => item.ActorUserId.HasValue &&
            !string.IsNullOrWhiteSpace(item.NewValueJson));
    }

    [Fact]
    public async Task UpdatePhase_ValidUpdateSucceedsButInvalidWeightPreservesStateAndHistory()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phase = await GetPhaseAsync(fixture.ProjectId, "Concept");

        using var valid = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}",
            PhasePayload(phase.RowVersion, 34, "InProgress", new DateOnly(2026, 9, 2)),
            $"phase-valid-{Guid.NewGuid():N}");
        valid.StatusCode.Should().Be(HttpStatusCode.OK, await valid.Content.ReadAsStringAsync());
        valid.Headers.ETag.Should().NotBeNull();
        var updated = await ReadJsonAsync(valid);
        var updatedVersion = updated.GetProperty("rowVersion").GetString()!;
        var historyCount = await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId));

        using var invalidWeight = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}",
            PhasePayload(updatedVersion, 50, "InProgress", new DateOnly(2026, 9, 2)),
            $"phase-invalid-weight-{Guid.NewGuid():N}");

        invalidWeight.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var persisted = await GetPhaseAsync(fixture.ProjectId, "Concept");
        persisted.RowVersion.Should().Be(updatedVersion);
        (await WithDbAsync(db => db.DesignSchedulePhases.Where(item =>
            item.OperationalProjectId == fixture.ProjectId).SumAsync(item => item.Weight))).Should().Be(100);
        (await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId))).Should().Be(historyCount);
    }

    [Fact]
    public async Task Initialize_RejectsInvalidInputsAndPartialBaselineWithoutPartialWrites()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var missingDates = await CreateFixtureAsync(useDefaultDates: false);
        var shortRange = await CreateFixtureAsync(
            startDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            deadline: new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
        var reversed = await CreateFixtureAsync(
            startDate: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc),
            deadline: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var invalidDefinitions = await CreateFixtureAsync();
        var partial = await CreateFixtureAsync();
        await WithDbAsync(async db =>
        {
            var designProjectId = await db.DesignProjects.Where(item =>
                    item.OperationalProjectId == partial.ProjectId)
                .Select(item => item.Id).SingleAsync();
            db.DesignSchedulePhases.Add(new DesignSchedulePhase
            {
                OperationalProjectId = partial.ProjectId,
                DesignProjectId = designProjectId,
                Code = DesignSchedulePhaseCode.Concept,
                PlannedStart = new DateOnly(2026, 9, 1),
                PlannedEnd = new DateOnly(2026, 9, 30),
                Status = DesignScheduleStatus.NotStarted,
                Weight = 34,
                CreatedByUserId = 1,
                UpdatedByUserId = 1,
            });
            await db.SaveChangesAsync();
        });

        using var missingDatesResponse = await InitializeAsync(missingDates.ProjectId, InitializePayload());
        using var shortRangeResponse = await InitializeAsync(shortRange.ProjectId, InitializePayload());
        using var reversedResponse = await InitializeAsync(reversed.ProjectId, InitializePayload());
        using var invalidWeightsResponse = await InitializeAsync(invalidDefinitions.ProjectId, new
        {
            phases = new[]
            {
                new { code = "Concept", weight = 34 },
                new { code = "BasicDesign", weight = 33 },
                new { code = "ShopDrawing", weight = 32 },
            },
        });
        using var duplicateCodesResponse = await InitializeAsync(invalidDefinitions.ProjectId, new
        {
            phases = new[]
            {
                new { code = "Concept", weight = 34 },
                new { code = "Concept", weight = 33 },
                new { code = "ShopDrawing", weight = 33 },
            },
        });
        using var partialResponse = await InitializeAsync(partial.ProjectId, InitializePayload());

        new[] { missingDatesResponse, shortRangeResponse, reversedResponse, invalidWeightsResponse,
                duplicateCodesResponse, partialResponse }
            .Should().OnlyContain(response => response.StatusCode == HttpStatusCode.BadRequest);
        var partialPhase = await GetPhaseAsync(partial.ProjectId, "Concept");
        using var partialUpdateResponse = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{partial.ProjectId}/design-schedule/phases/{partialPhase.Id}",
            PhasePayload(partialPhase.RowVersion, 100, "NotStarted"),
            $"partial-update-{Guid.NewGuid():N}");
        partialUpdateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignSchedulePhases.CountAsync(item =>
            item.OperationalProjectId != partial.ProjectId &&
            new[] { missingDates.ProjectId, shortRange.ProjectId, reversed.ProjectId,
                invalidDefinitions.ProjectId }.Contains(item.OperationalProjectId)))).Should().Be(0);
        (await WithDbAsync(db => db.DesignSchedulePhases.CountAsync(item =>
            item.OperationalProjectId == partial.ProjectId))).Should().Be(1);
        (await GetPhaseAsync(partial.ProjectId, "Concept")).RowVersion.Should().Be(partialPhase.RowVersion);
        (await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == partial.ProjectId))).Should().Be(0);
    }

    [Fact]
    public async Task UpdateTask_CyclicDependencyPreservesExistingGraphTaskAndHistory()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phaseId = await PhaseIdAsync(fixture.ProjectId, "Concept");
        using var createdA = await CreateTaskAsync(fixture, phaseId, TaskPayload("CHAIN-A", fixture.MemberId, 0));
        var taskA = await GetTaskAsync(fixture.ProjectId, "CHAIN-A");
        using var createdB = await CreateTaskAsync(fixture, phaseId,
            TaskPayload("CHAIN-B", fixture.MemberId, 0) with { PredecessorTaskIds = [taskA.Id] });
        var taskB = await GetTaskAsync(fixture.ProjectId, "CHAIN-B");
        using var createdC = await CreateTaskAsync(fixture, phaseId,
            TaskPayload("CHAIN-C", fixture.MemberId, 0) with { PredecessorTaskIds = [taskB.Id] });
        var taskC = await GetTaskAsync(fixture.ProjectId, "CHAIN-C");
        new[] { createdA, createdB, createdC }.Should()
            .OnlyContain(response => response.StatusCode == HttpStatusCode.Created);
        var historyCount = await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId));

        using var cycle = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{taskA.Id}",
            TaskPayload("CHAIN-A", fixture.MemberId, 0, rowVersion: taskA.RowVersion) with
            { PredecessorTaskIds = [taskC.Id], Name = "Should not persist" },
            $"cycle-{Guid.NewGuid():N}");

        cycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var persistedA = await GetTaskAsync(fixture.ProjectId, "CHAIN-A");
        persistedA.Name.Should().Be("Design package");
        persistedA.RowVersion.Should().Be(taskA.RowVersion);
        var edges = await WithDbAsync(db => db.DesignScheduleTaskDependencies.AsNoTracking()
            .Where(item => item.OperationalProjectId == fixture.ProjectId)
            .Select(item => new { item.TaskId, item.PredecessorTaskId }).ToListAsync());
        edges.Should().BeEquivalentTo(new[]
        {
            new { TaskId = taskB.Id, PredecessorTaskId = taskA.Id },
            new { TaskId = taskC.Id, PredecessorTaskId = taskB.Id },
        });
        (await WithDbAsync(db => db.DesignScheduleHistory.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId))).Should().Be(historyCount);
    }

    [Fact]
    public async Task Get_QueryBoundariesPaginationAndOverdueKeepRollupStable()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phaseId = await PhaseIdAsync(fixture.ProjectId, "Concept");
        using var first = await CreateTaskAsync(fixture, phaseId,
            TaskPayload("FILTER-A", fixture.MemberId, 0) with
            {
                PlannedStart = new DateOnly(2026, 9, 1),
                PlannedEnd = new DateOnly(2026, 9, 2),
                Weight = 40,
            });
        using var second = await CreateTaskAsync(fixture, phaseId,
            TaskPayload("FILTER-B", fixture.MemberId, 100, "Completed",
                new DateOnly(2026, 9, 2)) with
            {
                PlannedStart = new DateOnly(2026, 9, 2),
                PlannedEnd = new DateOnly(2026, 9, 2),
                ActualEnd = new DateOnly(2026, 9, 2),
                Weight = 60,
            });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);

        using var baselineResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule?page=1&pageSize=1");
        using var boundaryResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule" +
            $"?assigneeMemberId={fixture.MemberId}&plannedFrom=2026-09-02&plannedTo=2026-09-02&page=2&pageSize=1");
        using var overdueResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule?overdueOnly=true&pageSize=100");
        using var undefinedPhase = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule?phase=999");
        using var maximumPage = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule?page={int.MaxValue}&pageSize=100");

        baselineResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        boundaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        overdueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        undefinedPhase.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        maximumPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var baseline = await ReadJsonAsync(baselineResponse);
        var boundary = await ReadJsonAsync(boundaryResponse);
        var overdue = await ReadJsonAsync(overdueResponse);
        var maximum = await ReadJsonAsync(maximumPage);
        boundary.GetProperty("tasks").GetProperty("totalCount").GetInt32().Should().Be(2);
        boundary.GetProperty("tasks").GetProperty("items")[0].GetProperty("code")
            .GetString().Should().Be("FILTER-B");
        overdue.GetProperty("tasks").GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("code").GetString()).Should().Contain("FILTER-A").And.NotContain("FILTER-B");
        boundary.GetProperty("rollupSources").GetRawText().Should()
            .Be(baseline.GetProperty("rollupSources").GetRawText());
        maximum.GetProperty("tasks").GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task CreateTask_InvalidPayloadAndCrossProjectDependency_LeaveStateUnchanged()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var first = await CreateInitializedFixtureAsync();
        var second = await CreateInitializedFixtureAsync();
        var secondPhaseId = await PhaseIdAsync(second.ProjectId, "Concept");
        using var otherCreated = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{second.ProjectId}/design-schedule/phases/{secondPhaseId}/tasks",
            TaskPayload("OTHER-001", second.MemberId, 0), $"other-task-{Guid.NewGuid():N}");
        var otherTaskId = (await ReadJsonAsync(otherCreated)).GetProperty("id").GetInt32();
        var firstPhaseId = await PhaseIdAsync(first.ProjectId, "Concept");
        var before = await WithDbAsync(db => db.DesignScheduleTasks.CountAsync(item =>
            item.OperationalProjectId == first.ProjectId));

        var invalidMilestone = TaskPayload("BAD-MILESTONE", first.MemberId, 0) with
        {
            IsMilestone = true,
            PlannedEnd = new DateOnly(2026, 9, 3),
        };
        using var invalidResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{first.ProjectId}/design-schedule/phases/{firstPhaseId}/tasks",
            invalidMilestone, $"bad-milestone-{Guid.NewGuid():N}");
        using var crossProjectResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{first.ProjectId}/design-schedule/phases/{firstPhaseId}/tasks",
            TaskPayload("BAD-CROSS", first.MemberId, 0) with { PredecessorTaskIds = [otherTaskId] },
            $"bad-cross-{Guid.NewGuid():N}");
        using var missingDatesResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{first.ProjectId}/design-schedule/phases/{firstPhaseId}/tasks",
            new
            {
                code = "BAD-DATES",
                name = "Missing planned dates",
                departmentCode = "design",
                assigneeMemberId = first.MemberId,
                status = "NotStarted",
                progressPercent = 0,
                weight = 100,
                predecessorTaskIds = Array.Empty<int>(),
            }, $"bad-dates-{Guid.NewGuid():N}");
        using var nullPredecessorsResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{first.ProjectId}/design-schedule/phases/{firstPhaseId}/tasks",
            TaskPayload("BAD-PREDECESSORS", first.MemberId, 0) with { PredecessorTaskIds = null },
            $"bad-predecessors-{Guid.NewGuid():N}");

        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        crossProjectResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missingDatesResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        nullPredecessorsResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignScheduleTasks.CountAsync(item =>
            item.OperationalProjectId == first.ProjectId))).Should().Be(before);
        (await WithDbAsync(db => db.DesignScheduleTaskDependencies.CountAsync(item =>
            item.OperationalProjectId == first.ProjectId))).Should().Be(0);
    }

    [Fact]
    public async Task CreateTask_FieldBoundaries_AcceptLimitsAndRejectOutsidePartitions()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phaseId = await PhaseIdAsync(fixture.ProjectId, "Concept");
        var uri = $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks";
        var validMinimum = TaskPayload("A", fixture.MemberId, 0) with
        {
            Name = "N",
            Weight = 1,
            PlannedEnd = new DateOnly(2026, 9, 1),
        };
        var validMaximum = TaskPayload(new string('C', 80), fixture.MemberId, 100, "Completed",
            new DateOnly(2026, 9, 1)) with
        {
            Name = new string('N', 300),
            ActualEnd = new DateOnly(2026, 9, 3),
            Weight = 100,
        };

        using var minimumResponse = await SendJsonWithKeyAsync(HttpMethod.Post, uri,
            validMinimum, $"boundary-min-{Guid.NewGuid():N}");
        using var maximumResponse = await SendJsonWithKeyAsync(HttpMethod.Post, uri,
            validMaximum, $"boundary-max-{Guid.NewGuid():N}");
        minimumResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await minimumResponse.Content.ReadAsStringAsync());
        maximumResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            await maximumResponse.Content.ReadAsStringAsync());
        var persistedBeforeInvalid = await WithDbAsync(db => db.DesignScheduleTasks.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId));

        var invalidPayloads = new TaskPayloadModel[]
        {
            TaskPayload(" ", fixture.MemberId, 0),
            TaskPayload(new string('C', 81), fixture.MemberId, 0),
            TaskPayload("BAD-NAME", fixture.MemberId, 0) with { Name = new string('N', 301) },
            TaskPayload("BAD-PROGRESS-LOW", fixture.MemberId, -1),
            TaskPayload("BAD-PROGRESS-HIGH", fixture.MemberId, 101),
            TaskPayload("BAD-WEIGHT-LOW", fixture.MemberId, 0) with { Weight = 0 },
            TaskPayload("BAD-WEIGHT-HIGH", fixture.MemberId, 0) with { Weight = 101 },
            TaskPayload("BAD-ASSIGNEE", 0, 0),
            TaskPayload("BAD-STATUS", fixture.MemberId, 0, "Unsupported"),
            TaskPayload("BAD-ORDER", fixture.MemberId, 0) with
            {
                PlannedStart = new DateOnly(2026, 9, 3),
                PlannedEnd = new DateOnly(2026, 9, 2),
            },
            TaskPayload("BAD-ACTUAL", fixture.MemberId, 0) with
            {
                ActualEnd = new DateOnly(2026, 9, 2),
            },
            TaskPayload("BAD-PREDECESSOR-LIMIT", fixture.MemberId, 0) with
            {
                PredecessorTaskIds = Enumerable.Range(1, 101).ToList(),
            },
        };
        foreach (var payload in invalidPayloads)
        {
            using var response = await SendJsonWithKeyAsync(HttpMethod.Post, uri,
                payload, $"boundary-invalid-{Guid.NewGuid():N}");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                $"payload {payload.Code} returned {await response.Content.ReadAsStringAsync()}");
        }

        (await WithDbAsync(db => db.DesignScheduleTasks.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId))).Should().Be(persistedBeforeInvalid);
    }

    [Fact]
    public async Task Mutations_RequireValidIdempotencyKey()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateFixtureAsync();
        var uri = $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize";

        using var missing = await Client.PostAsJsonAsync(uri, InitializePayload());
        using var oversizedRequest = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(InitializePayload()),
        };
        oversizedRequest.Headers.Add("Idempotency-Key", new string('x', 121));
        using var oversized = await Client.SendAsync(oversizedRequest);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.DesignSchedulePhases.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
    }

    [Fact]
    public async Task Updates_RequireValidMatchingConcurrencyToken()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phase = await GetPhaseAsync(fixture.ProjectId, "Concept");
        var phaseUri = $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}";

        using var missing = await SendJsonWithKeyAsync(HttpMethod.Put, phaseUri,
            PhasePayload(null, 34, "InProgress"), $"token-missing-{Guid.NewGuid():N}");
        using var malformed = await SendJsonWithKeyAsync(HttpMethod.Put, phaseUri,
            PhasePayload("not-base64", 34, "InProgress"), $"token-malformed-{Guid.NewGuid():N}");
        using var mismatchRequest = new HttpRequestMessage(HttpMethod.Put, phaseUri)
        {
            Content = JsonContent.Create(PhasePayload(phase.RowVersion, 34, "InProgress")),
        };
        mismatchRequest.Headers.Add("Idempotency-Key", $"token-mismatch-{Guid.NewGuid():N}");
        mismatchRequest.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAAAA=\"");
        using var mismatch = await Client.SendAsync(mismatchRequest);

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetPhaseAsync(fixture.ProjectId, "Concept")).RowVersion.Should().Be(phase.RowVersion);
    }

    [Fact]
    public async Task CreateTask_IdempotentReplayAndKeyConflict_DoNotDuplicate()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phaseId = await PhaseIdAsync(fixture.ProjectId, "Concept");
        var key = $"schedule-create-{Guid.NewGuid():N}";
        var payload = TaskPayload("IDEMPOTENT-001", fixture.MemberId, 0);

        using var first = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks",
            payload, key);
        using var replay = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks",
            payload, key);
        using var conflict = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks",
            payload with { Name = "Different payload" }, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.DesignScheduleTasks.CountAsync(item =>
            item.OperationalProjectId == fixture.ProjectId && item.Code == "IDEMPOTENT-001"))).Should().Be(1);
    }

    [Fact]
    public async Task UpdateTask_StaleRowVersionCanRetryWithSameIdempotencyKey()
    {
        await AuthenticateAsync("SUPER_ADMIN");
        var fixture = await CreateInitializedFixtureAsync();
        var phaseId = await PhaseIdAsync(fixture.ProjectId, "Concept");
        using var createdResponse = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks",
            TaskPayload("STALE-001", fixture.MemberId, 0), $"stale-create-{Guid.NewGuid():N}");
        var created = await ReadJsonAsync(createdResponse);
        var taskId = created.GetProperty("id").GetInt32();
        var staleVersion = created.GetProperty("rowVersion").GetString();
        await WithDbAsync(async db =>
        {
            var task = await db.DesignScheduleTasks.FindAsync(taskId);
            task!.Name = "Competing update";
            await db.SaveChangesAsync();
        });
        var key = $"stale-update-{Guid.NewGuid():N}";
        using var stale = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{taskId}",
            TaskPayload("STALE-001", fixture.MemberId, 20, "InProgress",
                new DateOnly(2026, 9, 1), staleVersion), key);

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
        var current = await GetTaskAsync(fixture.ProjectId, "STALE-001");
        current.Name.Should().Be("Competing update");
        using var retry = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{taskId}",
            TaskPayload("STALE-001", fixture.MemberId, 20, "InProgress",
                new DateOnly(2026, 9, 1), current.RowVersion), key);
        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
    }

    private Task AuthenticateAsync(string roleCode) => AuthTestHelper.AuthenticateAsync(
        Client, client => AuthTestHelper.LoginAsRoleAsync(client, roleCode));

    private async Task<Fixture> CreateInitializedFixtureAsync()
    {
        var fixture = await CreateFixtureAsync();
        using var response = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize",
            InitializePayload(), $"schedule-init-{Guid.NewGuid():N}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return fixture;
    }

    private async Task<Fixture> CreateFixtureAsync(
        string managerRoleCode = "PM",
        DateTime? startDate = default,
        DateTime? deadline = default,
        bool useDefaultDates = true)
    {
        var managerId = await UserIdAsync(managerRoleCode);
        var memberUserId = await UserIdAsync("ARCHITECT");
        return await WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                Name = $"Schedule customer {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-DS-{Guid.NewGuid():N}"[..30],
                Name = "Detail design schedule fixture",
                CustomerId = customer.Id,
                ProjectManagerUserId = managerId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var designProject = new DesignProject
            {
                OperationalProjectId = project.Id,
                ProjectCode = $"DP-DS-{Guid.NewGuid():N}"[..30],
                Name = "Detail design schedule",
                CustomerId = customer.Id,
                StartDate = useDefaultDates
                    ? startDate ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
                    : startDate,
                Deadline = useDefaultDates
                    ? deadline ?? new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)
                    : deadline,
            };
            db.DesignProjects.Add(designProject);
            var member = new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = memberUserId,
                Position = "Architect",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                CreatedByUserId = managerId,
                UpdatedByUserId = managerId,
            };
            db.OperationalProjectMembers.Add(member);
            await db.SaveChangesAsync();
            return new Fixture(project.Id, member.Id);
        });
    }

    private async Task<int> CreateProjectAsync(int managerUserId) => (await WithDbAsync(async db =>
    {
        var customer = new Customer
        {
            Name = $"Restricted schedule customer {Guid.NewGuid():N}",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-RESTRICT-{Guid.NewGuid():N}"[..30],
            Name = "Restricted schedule project",
            CustomerId = customer.Id,
            ProjectManagerUserId = managerUserId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        return project;
    })).Id;

    private async Task<int> UserIdAsync(string roleCode)
    {
        var phone = roleCode == "USER"
            ? TestDataSeeder.CustomerPhone
            : TestDataSeeder.BusinessRolePhonesByCode[roleCode];
        return await WithDbAsync(db => db.Users.Where(user => user.PhoneNumber == phone)
            .Select(user => user.Id).SingleAsync());
    }

    private async Task<int> PhaseIdAsync(int projectId, string code)
    {
        return (await GetPhaseAsync(projectId, code)).Id;
    }

    private async Task<PhaseView> GetPhaseAsync(int projectId, string code)
    {
        var response = await Client.GetAsync($"/api/operational-projects/{projectId}/design-schedule");
        var body = await ReadJsonAsync(response);
        var phase = body.GetProperty("phases").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == code);
        return new PhaseView(
            phase.GetProperty("id").GetInt32(),
            phase.GetProperty("rowVersion").GetString()!);
    }

    private async Task<TaskView> GetTaskAsync(int projectId, string code)
    {
        var response = await Client.GetAsync($"/api/operational-projects/{projectId}/design-schedule?pageSize=100");
        var body = await ReadJsonAsync(response);
        var task = body.GetProperty("tasks").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == code);
        return new TaskView(
            task.GetProperty("id").GetInt32(),
            task.GetProperty("name").GetString()!,
            task.GetProperty("rowVersion").GetString()!);
    }

    private async Task<HttpResponseMessage> SendJsonWithKeyAsync(
        HttpMethod method,
        string uri,
        object payload,
        string key)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key);
        return await Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> InitializeAsync(int projectId, object payload) =>
        SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{projectId}/design-schedule/initialize",
            payload, $"initialize-{Guid.NewGuid():N}");

    private Task<HttpResponseMessage> CreateTaskAsync(
        Fixture fixture,
        int phaseId,
        TaskPayloadModel payload) => SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phaseId}/tasks",
            payload, $"create-{Guid.NewGuid():N}");

    private async Task AssertMutationStatusesAsync(
        HttpStatusCode expected,
        Fixture fixture,
        PhaseView phase,
        TaskView task)
    {
        using var initialize = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/initialize",
            InitializePayload(), $"authorization-init-{Guid.NewGuid():N}");
        using var updatePhase = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}",
            new
            {
                plannedStart = new DateOnly(2026, 9, 1),
                plannedEnd = new DateOnly(2026, 10, 1),
                status = "NotStarted",
                progressPercent = 0,
                weight = 34,
                rowVersion = phase.RowVersion,
            }, $"authorization-phase-{Guid.NewGuid():N}");
        using var createTask = await SendJsonWithKeyAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/phases/{phase.Id}/tasks",
            TaskPayload("AUTH-002", fixture.MemberId, 0), $"authorization-create-{Guid.NewGuid():N}");
        using var updateTask = await SendJsonWithKeyAsync(HttpMethod.Put,
            $"/api/operational-projects/{fixture.ProjectId}/design-schedule/tasks/{task.Id}",
            TaskPayload("AUTH-001", fixture.MemberId, 0, rowVersion: task.RowVersion),
            $"authorization-update-{Guid.NewGuid():N}");

        initialize.StatusCode.Should().Be(expected);
        updatePhase.StatusCode.Should().Be(expected);
        createTask.StatusCode.Should().Be(expected);
        updateTask.StatusCode.Should().Be(expected);
    }

    private static object InitializePayload() => new
    {
        phases = new[]
        {
            new { code = "Concept", weight = 34 },
            new { code = "BasicDesign", weight = 33 },
            new { code = "ShopDrawing", weight = 33 },
        },
    };

    private static object PhasePayload(
        string? rowVersion,
        int weight,
        string status,
        DateOnly? actualStart = null) => new
        {
            plannedStart = new DateOnly(2026, 9, 1),
            plannedEnd = new DateOnly(2026, 10, 1),
            actualStart,
            status,
            progressPercent = status == "InProgress" ? 10 : 0,
            weight,
            rowVersion,
        };

    private static TaskPayloadModel TaskPayload(
        string code,
        int memberId,
        int progress,
        string status = "NotStarted",
        DateOnly? actualStart = null,
        string? rowVersion = null) => new()
        {
            Code = code,
            Name = "Design package",
            DepartmentCode = "design",
            AssigneeMemberId = memberId,
            PlannedStart = new DateOnly(2026, 9, 1),
            PlannedEnd = new DateOnly(2026, 9, 3),
            ActualStart = actualStart,
            Status = status,
            ProgressPercent = progress,
            Weight = 100,
            RowVersion = rowVersion,
        };

    private sealed record Fixture(int ProjectId, int MemberId);
    private sealed record PhaseView(int Id, string RowVersion);
    private sealed record TaskView(int Id, string Name, string RowVersion);

    private sealed record TaskPayloadModel
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string DepartmentCode { get; init; } = string.Empty;
        public int AssigneeMemberId { get; init; }
        public bool IsMilestone { get; init; }
        public DateOnly PlannedStart { get; init; }
        public DateOnly PlannedEnd { get; init; }
        public DateOnly? ActualStart { get; init; }
        public DateOnly? ActualEnd { get; init; }
        public string Status { get; init; } = string.Empty;
        public int ProgressPercent { get; init; }
        public int Weight { get; init; }
        public List<int>? PredecessorTaskIds { get; init; } = new();
        public string? RowVersion { get; init; }
    }
}