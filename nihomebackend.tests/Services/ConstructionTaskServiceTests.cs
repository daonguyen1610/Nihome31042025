using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-141 Gantt / Construction Task service:
/// CRUD validation, auto task-code allocation, predecessor cycle
/// detection, planned-vs-actual date rules, overdue flag on list, and
/// bulk-delete behaviour when other tasks depend on the row.
/// </summary>
public class ConstructionTaskServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ConstructionTaskService _sut;
    private readonly int _userId;
    private readonly int _projectId;
    private readonly int _otherProjectId;

    public ConstructionTaskServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ConstructionTaskService(_db, NullLogger<ConstructionTaskService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000141",
            FullName = "Gantt Tester",
            Email = "gantt.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        var customer = new Customer { Name = "GanttCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-GANTT-A",
            Name = "Gantt fixture A",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        var other = new DesignProject
        {
            ProjectCode = "DP-2026-GANTT-B",
            Name = "Gantt fixture B",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.AddRange(project, other);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
        _otherProjectId = other.Id;
    }

    private CreateConstructionTaskRequest Req(string? name = null, DateOnly? start = null, DateOnly? end = null, int? projectId = null)
        => new()
        {
            DesignProjectId = projectId ?? _projectId,
            Name = name ?? "Task",
            PlannedStart = start ?? new DateOnly(2026, 6, 1),
            PlannedEnd = end ?? new DateOnly(2026, 6, 10),
        };

    [Fact]
    public async Task CreateAsync_allocates_sequential_task_code_when_blank()
    {
        var t1 = await _sut.CreateAsync(Req("Task A"), _userId);
        var t2 = await _sut.CreateAsync(Req("Task B"), _userId);
        var t3 = await _sut.CreateAsync(Req("Task C"), _userId);

        Assert.Equal("T-001", t1.TaskCode);
        Assert.Equal("T-002", t2.TaskCode);
        Assert.Equal("T-003", t3.TaskCode);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_name()
    {
        var req = Req(name: "   ");
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_end_before_start()
    {
        var req = Req(start: new DateOnly(2026, 6, 10), end: new DateOnly(2026, 6, 5));
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_project()
    {
        var req = Req(projectId: 999_999);
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_taskcode_in_project()
    {
        await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            TaskCode = "CUSTOM-1",
            Name = "One",
            PlannedStart = new DateOnly(2026, 6, 1),
            PlannedEnd = new DateOnly(2026, 6, 2),
        }, _userId);

        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            TaskCode = "CUSTOM-1",
            Name = "Two",
            PlannedStart = new DateOnly(2026, 7, 1),
            PlannedEnd = new DateOnly(2026, 7, 2),
        }, _userId));
    }

    [Fact]
    public async Task CreateAsync_accepts_predecessors_and_persists_edges()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "B",
            PlannedStart = new DateOnly(2026, 6, 11),
            PlannedEnd = new DateOnly(2026, 6, 15),
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);

        var loaded = await _sut.GetAsync(b.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Predecessors);
        Assert.Equal(a.Id, loaded.Predecessors[0].PredecessorTaskId);
        Assert.Equal("T-001", loaded.Predecessors[0].PredecessorTaskCode);
    }

    [Fact]
    public async Task SetPredecessorsAsync_rejects_cross_project_edge()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(Req("B", projectId: _otherProjectId), _userId);
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() =>
            _sut.SetPredecessorsAsync(a.Id, new SetConstructionTaskPredecessorsRequest
            {
                PredecessorTaskIds = new List<int> { b.Id },
            }, _userId));
    }

    [Fact]
    public async Task SetPredecessorsAsync_detects_cycles()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(Req("B"), _userId);
        // A -> B is fine
        await _sut.SetPredecessorsAsync(b.Id, new SetConstructionTaskPredecessorsRequest
        {
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);
        // B -> A would close the loop
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() =>
            _sut.SetPredecessorsAsync(a.Id, new SetConstructionTaskPredecessorsRequest
            {
                PredecessorTaskIds = new List<int> { b.Id },
            }, _userId));
    }

    [Fact]
    public async Task SetPredecessorsAsync_no_op_when_set_is_identical()
    {
        // Re-submitting the same predecessor set must NOT delete + re-add
        // the join rows — otherwise every "Save" click on the detail sheet
        // would churn the audit log even when nothing changed.
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "B",
            PlannedStart = new DateOnly(2026, 6, 11),
            PlannedEnd = new DateOnly(2026, 6, 15),
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);
        var edgeIdBefore = (await _db.ConstructionTaskDependencies
            .FirstAsync(d => d.TaskId == b.Id && d.PredecessorTaskId == a.Id)).Id;

        await _sut.SetPredecessorsAsync(b.Id, new SetConstructionTaskPredecessorsRequest
        {
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);

        // Same primary key => the row was not re-created.
        var edgeIdAfter = (await _db.ConstructionTaskDependencies
            .FirstAsync(d => d.TaskId == b.Id && d.PredecessorTaskId == a.Id)).Id;
        Assert.Equal(edgeIdBefore, edgeIdAfter);
        Assert.Equal(1, await _db.ConstructionTaskDependencies.CountAsync(d => d.TaskId == b.Id));
    }

    [Fact]
    public async Task UpdateAsync_rejects_progress_out_of_range()
    {
        var a = await _sut.CreateAsync(Req(), _userId);
        var body = new UpdateConstructionTaskRequest
        {
            Name = "A",
            PlannedStart = a.PlannedStart,
            PlannedEnd = a.PlannedEnd,
            ProgressPercent = 150,
            Status = "InProgress",
        };
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() =>
            _sut.UpdateAsync(a.Id, body, _userId));
    }

    [Fact]
    public async Task UpdateAsync_auto_completes_when_progress_100_and_actual_end_set()
    {
        var a = await _sut.CreateAsync(Req(), _userId);
        var body = new UpdateConstructionTaskRequest
        {
            Name = "A",
            PlannedStart = a.PlannedStart,
            PlannedEnd = a.PlannedEnd,
            ActualStart = new DateOnly(2026, 6, 1),
            ActualEnd = new DateOnly(2026, 6, 5),
            ProgressPercent = 100,
            Status = "InProgress",
        };
        var updated = await _sut.UpdateAsync(a.Id, body, _userId);
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated!.Status);
    }

    [Fact]
    public async Task UpdateAsync_auto_completes_and_backfills_actual_end_when_missing()
    {
        // 100% progress with no explicit actualEnd => service fills
        // actualEnd = today so the Gantt/status can never disagree.
        var a = await _sut.CreateAsync(Req(), _userId);
        var body = new UpdateConstructionTaskRequest
        {
            Name = "A",
            PlannedStart = a.PlannedStart,
            PlannedEnd = a.PlannedEnd,
            ActualStart = new DateOnly(2026, 6, 1),
            ActualEnd = null,
            ProgressPercent = 100,
            Status = "InProgress",
        };
        var updated = await _sut.UpdateAsync(a.Id, body, _userId);
        Assert.NotNull(updated);
        Assert.Equal("Completed", updated!.Status);
        Assert.NotNull(updated.ActualEnd);
    }

    [Fact]
    public async Task UpdateAsync_auto_starts_when_actual_start_set_on_planned_task()
    {
        var a = await _sut.CreateAsync(Req(), _userId);
        var body = new UpdateConstructionTaskRequest
        {
            Name = "A",
            PlannedStart = a.PlannedStart,
            PlannedEnd = a.PlannedEnd,
            ActualStart = new DateOnly(2026, 6, 1),
            ProgressPercent = 20,
            Status = "Planned",
        };
        var updated = await _sut.UpdateAsync(a.Id, body, _userId);
        Assert.Equal("InProgress", updated!.Status);
    }

    [Fact]
    public async Task ListAsync_flags_overdue_and_reports_status_counts()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Overdue: planned end 10 days ago, still in progress.
        await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "Late",
            PlannedStart = today.AddDays(-20),
            PlannedEnd = today.AddDays(-10),
        }, _userId);
        var late = await _db.ConstructionTasks.OrderByDescending(t => t.Id).FirstAsync();
        late.Status = ConstructionTaskStatus.InProgress;
        late.ProgressPercent = 50;
        await _db.SaveChangesAsync();

        // On-time completed
        await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "Done",
            PlannedStart = today.AddDays(-15),
            PlannedEnd = today.AddDays(-5),
        }, _userId);
        var done = await _db.ConstructionTasks.OrderByDescending(t => t.Id).FirstAsync();
        done.Status = ConstructionTaskStatus.Completed;
        done.ProgressPercent = 100;
        done.ActualStart = today.AddDays(-15);
        done.ActualEnd = today.AddDays(-7);
        await _db.SaveChangesAsync();

        var listAll = await _sut.ListAsync(new ConstructionTaskListParams { DesignProjectId = _projectId });
        Assert.Equal(2, listAll.Total);
        Assert.Equal(1, listAll.OverdueCount);
        Assert.Equal(1, listAll.StatusCounts["InProgress"]);
        Assert.Equal(1, listAll.StatusCounts["Completed"]);

        var overdueOnly = await _sut.ListAsync(new ConstructionTaskListParams
        {
            DesignProjectId = _projectId,
            OverdueOnly = true,
        });
        Assert.Single(overdueOnly.Items);
        Assert.True(overdueOnly.Items[0].IsOverdue);
        Assert.Equal("Late", overdueOnly.Items[0].Name);
    }

    [Fact]
    public async Task ListAsync_search_matches_code_name_wbs()
    {
        await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "Đào móng",
            Wbs = "1.2.3",
            TaskCode = "FN-01",
            PlannedStart = new DateOnly(2026, 6, 1),
            PlannedEnd = new DateOnly(2026, 6, 5),
        }, _userId);
        await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "MEP rough-in",
            PlannedStart = new DateOnly(2026, 6, 6),
            PlannedEnd = new DateOnly(2026, 6, 10),
        }, _userId);

        var byName = await _sut.ListAsync(new ConstructionTaskListParams { DesignProjectId = _projectId, Search = "MEP" });
        Assert.Single(byName.Items);

        var byWbs = await _sut.ListAsync(new ConstructionTaskListParams { DesignProjectId = _projectId, Search = "1.2" });
        Assert.Single(byWbs.Items);

        var byCode = await _sut.ListAsync(new ConstructionTaskListParams { DesignProjectId = _projectId, Search = "FN-" });
        Assert.Single(byCode.Items);
    }

    [Fact]
    public async Task DeleteAsync_blocks_when_task_is_predecessor_of_others()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "B",
            PlannedStart = new DateOnly(2026, 6, 11),
            PlannedEnd = new DateOnly(2026, 6, 15),
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);

        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.DeleteAsync(a.Id));
        Assert.True(await _sut.DeleteAsync(b.Id));
        Assert.True(await _sut.DeleteAsync(a.Id));
    }

    [Fact]
    public async Task BulkDeleteAsync_deletes_whole_dependency_chain_when_all_selected()
    {
        // Chain A -> B -> C plus isolated D and a missing id. The service
        // should collapse the internal edges and drop the three chained
        // rows, while still surfacing the missing-id failure so the caller
        // knows part of their input never existed.
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "B",
            PlannedStart = new DateOnly(2026, 6, 11),
            PlannedEnd = new DateOnly(2026, 6, 15),
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);
        var c = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "C",
            PlannedStart = new DateOnly(2026, 6, 16),
            PlannedEnd = new DateOnly(2026, 6, 20),
            PredecessorTaskIds = new List<int> { b.Id },
        }, _userId);
        var d = await _sut.CreateAsync(Req("D"), _userId);

        var response = await _sut.BulkDeleteAsync(new[] { a.Id, b.Id, c.Id, d.Id, 999 });
        Assert.Equal(5, response.Requested);
        Assert.Equal(4, response.Deleted);
        Assert.Single(response.Failures);
        Assert.Contains(response.Failures, f => f.Id == 999);
        Assert.False(await _db.ConstructionTasks.AnyAsync(t => t.Id == a.Id));
        Assert.False(await _db.ConstructionTaskDependencies.AnyAsync());
    }

    [Fact]
    public async Task BulkDeleteAsync_blocks_when_external_dependent_survives()
    {
        // A -> B. Deleting just A must still fail because B (not in the
        // delete set) still depends on it.
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(new CreateConstructionTaskRequest
        {
            DesignProjectId = _projectId,
            Name = "B",
            PlannedStart = new DateOnly(2026, 6, 11),
            PlannedEnd = new DateOnly(2026, 6, 15),
            PredecessorTaskIds = new List<int> { a.Id },
        }, _userId);

        var response = await _sut.BulkDeleteAsync(new[] { a.Id });
        Assert.Equal(1, response.Requested);
        Assert.Equal(0, response.Deleted);
        Assert.Single(response.Failures);
        Assert.Equal(a.Id, response.Failures[0].Id);
        Assert.True(await _db.ConstructionTasks.AnyAsync(t => t.Id == b.Id));
        Assert.True(await _db.ConstructionTasks.AnyAsync(t => t.Id == a.Id));
    }

    [Fact]
    public async Task BulkDeleteAsync_rejects_empty_input()
    {
        await Assert.ThrowsAsync<ConstructionTaskOperationException>(() => _sut.BulkDeleteAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task UpdateProgressAsync_updates_and_transitions_status()
    {
        var a = await _sut.CreateAsync(Req(), _userId);
        var body = new UpdateConstructionTaskProgressRequest
        {
            ProgressPercent = 100,
            ActualStart = new DateOnly(2026, 6, 1),
            ActualEnd = new DateOnly(2026, 6, 8),
            Status = "InProgress",
        };
        var updated = await _sut.UpdateProgressAsync(a.Id, body, _userId);
        Assert.Equal(100, updated!.ProgressPercent);
        Assert.Equal("Completed", updated.Status);
    }

    public void Dispose() => _db.Dispose();
}
