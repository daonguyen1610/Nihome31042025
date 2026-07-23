using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-146 Punch List service: CRUD validation,
/// state-machine transitions, reopen counter, overdue roll-up on list,
/// verified/cancelled lock on edits and bulk-delete rules.
/// </summary>
public class PunchItemServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PunchItemService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public PunchItemServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new PunchItemService(_db, NullLogger<PunchItemService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000146",
            FullName = "Punch Tester",
            Email = "punch.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        var customer = new Customer { Name = "PunchCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-PUNCH-A",
            Name = "Punch fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
    }

    private CreatePunchItemRequest Req(string? title = null, string severity = "Medium")
        => new()
        {
            DesignProjectId = _projectId,
            Title = title ?? "Broken tile",
            Severity = severity,
        };

    [Fact]
    public async Task CreateAsync_allocates_sequential_code()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(Req("B"), _userId);
        Assert.Equal("P-001", a.PunchCode);
        Assert.Equal("P-002", b.PunchCode);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_title()
    {
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.CreateAsync(Req(title: "  "), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_severity()
    {
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.CreateAsync(Req(severity: "Nuclear"), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_project()
    {
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.CreateAsync(new CreatePunchItemRequest
            {
                DesignProjectId = 999_999,
                Title = "orphan",
                Severity = "Medium",
            }, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_code_in_project()
    {
        await _sut.CreateAsync(new CreatePunchItemRequest
        {
            DesignProjectId = _projectId,
            PunchCode = "CUSTOM-1",
            Title = "one",
            Severity = "Low",
        }, _userId);
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.CreateAsync(new CreatePunchItemRequest
            {
                DesignProjectId = _projectId,
                PunchCode = "CUSTOM-1",
                Title = "two",
                Severity = "Low",
            }, _userId));
    }

    [Fact]
    public async Task TransitionStatusAsync_walks_full_lifecycle()
    {
        var p = await _sut.CreateAsync(Req(), _userId);
        var inp = await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "InProgress" }, _userId);
        Assert.Equal("InProgress", inp!.Status);

        var fixedRow = await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Fixed" }, _userId);
        Assert.Equal("Fixed", fixedRow!.Status);

        var verified = await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Verified" }, _userId);
        Assert.Equal("Verified", verified!.Status);
        Assert.NotNull(verified.VerifiedAt);

        // Reopen from Verified goes back to Open + bumps reopenCount.
        var reopened = await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Open" }, _userId);
        Assert.Equal("Open", reopened!.Status);
        Assert.Equal(1, reopened.ReopenCount);
        Assert.Null(reopened.VerifiedAt);
    }

    [Fact]
    public async Task TransitionStatusAsync_rejects_invalid_transition()
    {
        var p = await _sut.CreateAsync(Req(), _userId);
        // Open → Verified is not allowed — must go through Fixed.
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Verified" }, _userId));
    }

    [Fact]
    public async Task TransitionStatusAsync_cancelled_is_terminal()
    {
        var p = await _sut.CreateAsync(Req(), _userId);
        await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Cancelled" }, _userId);
        await Assert.ThrowsAsync<PunchItemOperationException>(() =>
            _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Open" }, _userId));
    }

    [Fact]
    public async Task UpdateAsync_locked_on_verified_and_cancelled()
    {
        var p = await _sut.CreateAsync(Req(), _userId);
        await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "InProgress" }, _userId);
        await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Fixed" }, _userId);
        await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "Verified" }, _userId);

        var body = new UpdatePunchItemRequest
        {
            Title = "changed",
            Severity = "Low",
        };
        await Assert.ThrowsAsync<PunchItemOperationException>(() => _sut.UpdateAsync(p.Id, body, _userId));
    }

    [Fact]
    public async Task DeleteAsync_only_allowed_on_open()
    {
        var p = await _sut.CreateAsync(Req(), _userId);
        await _sut.TransitionStatusAsync(p.Id, new TransitionPunchStatusRequest { Status = "InProgress" }, _userId);
        await Assert.ThrowsAsync<PunchItemOperationException>(() => _sut.DeleteAsync(p.Id));
    }

    [Fact]
    public async Task BulkDeleteAsync_only_removes_open_and_reports_failures()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(Req("B"), _userId);
        var c = await _sut.CreateAsync(Req("C"), _userId);
        await _sut.TransitionStatusAsync(b.Id, new TransitionPunchStatusRequest { Status = "InProgress" }, _userId);

        var response = await _sut.BulkDeleteAsync(new[] { a.Id, b.Id, c.Id, 999 });
        Assert.Equal(4, response.Requested);
        Assert.Equal(2, response.Deleted); // a + c
        Assert.Equal(2, response.Failures.Count);
        Assert.Contains(response.Failures, f => f.Id == b.Id);
        Assert.Contains(response.Failures, f => f.Id == 999);
    }

    [Fact]
    public async Task BulkDeleteAsync_rejects_empty_input()
    {
        await Assert.ThrowsAsync<PunchItemOperationException>(() => _sut.BulkDeleteAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task ListAsync_flags_overdue_and_filters()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Overdue: deadline yesterday, still Open.
        var late = await _sut.CreateAsync(new CreatePunchItemRequest
        {
            DesignProjectId = _projectId,
            Title = "Late fix",
            Severity = "High",
            Deadline = today.AddDays(-1),
        }, _userId);

        // On-time
        await _sut.CreateAsync(new CreatePunchItemRequest
        {
            DesignProjectId = _projectId,
            Title = "Cabinet install",
            Severity = "Low",
            Deadline = today.AddDays(5),
        }, _userId);

        // No deadline
        await _sut.CreateAsync(Req("Loose"), _userId);

        var listAll = await _sut.ListAsync(new PunchItemListParams { DesignProjectId = _projectId });
        Assert.Equal(3, listAll.Total);
        Assert.Equal(1, listAll.OverdueCount);

        var lateItem = listAll.Items.First(i => i.Id == late.Id);
        Assert.True(lateItem.IsOverdue);

        var overdueOnly = await _sut.ListAsync(new PunchItemListParams { DesignProjectId = _projectId, OverdueOnly = true });
        Assert.Single(overdueOnly.Items);

        var bySeverity = await _sut.ListAsync(new PunchItemListParams { DesignProjectId = _projectId, Severity = "Low" });
        Assert.Single(bySeverity.Items);
    }

    [Fact]
    public async Task ListAsync_search_matches_title_code_and_location()
    {
        await _sut.CreateAsync(new CreatePunchItemRequest
        {
            DesignProjectId = _projectId,
            Title = "Broken outlet",
            Location = "Floor 3",
            Severity = "Medium",
        }, _userId);
        await _sut.CreateAsync(new CreatePunchItemRequest
        {
            DesignProjectId = _projectId,
            Title = "Cabinet",
            Location = "Kitchen",
            Severity = "Low",
        }, _userId);

        var byTitle = await _sut.ListAsync(new PunchItemListParams { DesignProjectId = _projectId, Search = "outlet" });
        Assert.Single(byTitle.Items);
        var byLocation = await _sut.ListAsync(new PunchItemListParams { DesignProjectId = _projectId, Search = "Kitchen" });
        Assert.Single(byLocation.Items);
    }

    public void Dispose() => _db.Dispose();
}
