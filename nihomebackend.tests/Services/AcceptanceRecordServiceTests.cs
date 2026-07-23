using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-143 partial acceptance service — state
/// machine, code allocation, filter roll-ups, update lock on
/// Approved/Cancelled and bulk-delete rules.
/// </summary>
public class AcceptanceRecordServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AcceptanceRecordService _sut;
    private readonly int _userId;
    private readonly int _projectId;
    private readonly int _taskId;

    public AcceptanceRecordServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new AcceptanceRecordService(_db, NullLogger<AcceptanceRecordService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000143",
            FullName = "Acceptance Tester",
            Email = "acc.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        var customer = new Customer { Name = "AccCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-ACC-A",
            Name = "Acceptance fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        var task = new ConstructionTask
        {
            DesignProjectId = project.Id,
            TaskCode = "T-001",
            Name = "Foundation",
            PlannedStart = new DateOnly(2026, 6, 1),
            PlannedEnd = new DateOnly(2026, 6, 20),
            Status = ConstructionTaskStatus.InProgress,
        };
        _db.ConstructionTasks.Add(task);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
        _taskId = task.Id;
    }

    private CreateAcceptanceRecordRequest Req(string? title = null, DateOnly? date = null, int? taskId = null)
        => new()
        {
            DesignProjectId = _projectId,
            Title = title ?? "Nghiệm thu",
            AcceptanceDate = date ?? new DateOnly(2026, 6, 15),
            ConstructionTaskId = taskId,
        };

    [Fact]
    public async Task CreateAsync_allocates_sequential_code()
    {
        var a = await _sut.CreateAsync(Req("A1"), _userId);
        var b = await _sut.CreateAsync(Req("A2"), _userId);
        var c = await _sut.CreateAsync(Req("A3"), _userId);
        Assert.Equal("A-001", a.AcceptanceCode);
        Assert.Equal("A-002", b.AcceptanceCode);
        Assert.Equal("A-003", c.AcceptanceCode);
        Assert.Equal("Draft", a.Status);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_title()
    {
        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.CreateAsync(Req(title: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_project()
    {
        var req = Req("A");
        req.DesignProjectId = 9999;
        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_task_from_other_project()
    {
        var other = new DesignProject
        {
            ProjectCode = "DP-2026-ACC-B",
            Name = "Other",
            CustomerId = _db.Customers.First().Id,
        };
        _db.DesignProjects.Add(other);
        _db.SaveChanges();
        var otherTask = new ConstructionTask
        {
            DesignProjectId = other.Id,
            TaskCode = "T-X",
            Name = "X",
            PlannedStart = new DateOnly(2026, 1, 1),
            PlannedEnd = new DateOnly(2026, 1, 2),
        };
        _db.ConstructionTasks.Add(otherTask);
        _db.SaveChanges();

        var ex = await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.CreateAsync(Req(taskId: otherTask.Id), _userId));
        Assert.Contains("không thuộc", ex.Message);
    }

    [Fact]
    public async Task Transition_walks_draft_submitted_approved()
    {
        var a = await _sut.CreateAsync(Req("Full flow"), _userId);
        var submitted = await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        Assert.Equal("Submitted", submitted!.Status);
        Assert.NotNull(submitted.SubmittedAt);

        var approved = await _sut.ApproveAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Approved", ResolutionNote = "OK" }, _userId);
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("OK", approved.ResolutionNote);
    }

    [Fact]
    public async Task Transition_status_endpoint_refuses_Approved()
    {
        var a = await _sut.CreateAsync(Req("X"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        var ex = await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Approved" }, _userId));
        Assert.Contains("/approve", ex.Message);
    }

    [Fact]
    public async Task Reject_then_revise_increments_revision_counter()
    {
        var a = await _sut.CreateAsync(Req("Rev"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        var rejected = await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Rejected", ResolutionNote = "Fix rỗ" }, _userId);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.NotNull(rejected.RejectedAt);
        Assert.Equal(0, rejected.RevisionCount);

        var revised = await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Draft" }, _userId);
        Assert.Equal("Draft", revised!.Status);
        Assert.Equal(1, revised.RevisionCount);
    }

    [Fact]
    public async Task Transition_rejects_invalid_move()
    {
        var a = await _sut.CreateAsync(Req("Bad"), _userId);
        // Draft -> Rejected is not allowed.
        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Rejected" }, _userId));
    }

    [Fact]
    public async Task Cancelled_is_terminal()
    {
        var a = await _sut.CreateAsync(Req("Term"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Cancelled" }, _userId);
        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Draft" }, _userId));
    }

    [Fact]
    public async Task UpdateAsync_locked_after_approved()
    {
        var a = await _sut.CreateAsync(Req("Lock"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Approved" }, _userId);

        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(
            () => _sut.UpdateAsync(a.Id, new UpdateAcceptanceRecordRequest
            {
                Title = "New",
                AcceptanceDate = new DateOnly(2026, 6, 15),
            }, _userId));
    }

    [Fact]
    public async Task DeleteAsync_blocks_approved()
    {
        var a = await _sut.CreateAsync(Req("NoDel"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a.Id, new TransitionAcceptanceStatusRequest { Status = "Approved" }, _userId);
        await Assert.ThrowsAsync<AcceptanceRecordOperationException>(() => _sut.DeleteAsync(a.Id));
    }

    [Fact]
    public async Task BulkDelete_skips_approved()
    {
        var a1 = await _sut.CreateAsync(Req("Bulk1"), _userId);
        var a2 = await _sut.CreateAsync(Req("Bulk2"), _userId);
        await _sut.TransitionAsync(a2.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a2.Id, new TransitionAcceptanceStatusRequest { Status = "Approved" }, _userId);

        var res = await _sut.BulkDeleteAsync(new BulkDeleteAcceptanceRecordsRequest { Ids = new List<int> { a1.Id, a2.Id } });
        Assert.Contains(a1.Id, res.DeletedIds);
        Assert.Contains(a2.Id, res.SkippedIds);
    }

    [Fact]
    public async Task ListAsync_flags_overdue_and_returns_status_counts()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await _sut.CreateAsync(Req("Overdue", yesterday), _userId);
        var open = await _sut.CreateAsync(Req("Open", tomorrow), _userId);
        await _sut.TransitionAsync(open.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);

        var list = await _sut.ListAsync(new AcceptanceRecordListParams { DesignProjectId = _projectId });
        Assert.Equal(2, list.Total);
        Assert.Equal(1, list.OverdueCount);
        Assert.Equal(1, list.StatusCounts["Draft"]);
        Assert.Equal(1, list.StatusCounts["Submitted"]);
        Assert.Contains(list.Items, i => i.Title == "Overdue" && i.IsOverdue);
    }

    [Fact]
    public async Task ListAsync_openOnly_filters_out_approved_and_cancelled()
    {
        var a1 = await _sut.CreateAsync(Req("Draft"), _userId);
        var a2 = await _sut.CreateAsync(Req("Done"), _userId);
        await _sut.TransitionAsync(a2.Id, new TransitionAcceptanceStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a2.Id, new TransitionAcceptanceStatusRequest { Status = "Approved" }, _userId);

        var list = await _sut.ListAsync(new AcceptanceRecordListParams { DesignProjectId = _projectId, OpenOnly = true });
        Assert.Single(list.Items);
        Assert.Equal(a1.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_search_matches_title_code_location()
    {
        await _sut.CreateAsync(new CreateAcceptanceRecordRequest
        {
            DesignProjectId = _projectId,
            Title = "Cột trục A",
            Location = "Tầng hầm",
            AcceptanceDate = new DateOnly(2026, 6, 15),
        }, _userId);
        await _sut.CreateAsync(Req("Different"), _userId);

        var byLoc = await _sut.ListAsync(new AcceptanceRecordListParams { DesignProjectId = _projectId, Search = "hầm" });
        Assert.Single(byLoc.Items);

        var byCode = await _sut.ListAsync(new AcceptanceRecordListParams { DesignProjectId = _projectId, Search = "A-002" });
        Assert.Single(byCode.Items);
    }

    public void Dispose() => _db.Dispose();
}
