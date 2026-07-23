using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-142 Site Diary service: CRUD validation,
/// weather-code lookup, one-per-day guard, Draft → Submitted → Confirmed
/// lifecycle, edit/delete gates and bulk-delete rules.
/// </summary>
public class SiteDiaryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SiteDiaryService _sut;
    private readonly int _userId;
    private readonly int _projectId;
    private readonly int _otherProjectId;

    public SiteDiaryServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new SiteDiaryService(_db, NullLogger<SiteDiaryService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000142",
            FullName = "Diary Tester",
            Email = "diary.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "diary_weather", Code = "sunny", Name = "Nắng", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "diary_weather", Code = "rain", Name = "Mưa", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "diary_weather", Code = "storm", Name = "Giông", IsActive = false, SortOrder = 3 });

        var customer = new Customer { Name = "DiaryCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-DIARY-A",
            Name = "Diary fixture A",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        var other = new DesignProject
        {
            ProjectCode = "DP-2026-DIARY-B",
            Name = "Diary fixture B",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.AddRange(project, other);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
        _otherProjectId = other.Id;
    }

    private CreateSiteDiaryRequest Req(DateOnly? date = null, int? projectId = null, string weather = "sunny", string work = "Đổ bê tông móng trục A")
        => new()
        {
            DesignProjectId = projectId ?? _projectId,
            DiaryDate = date ?? new DateOnly(2026, 7, 1),
            WeatherCode = weather,
            WorkPerformed = work,
            HeadcountLabor = 10,
            HeadcountEngineers = 1,
            HeadcountSupervisors = 1,
            HeadcountSubcontractors = 0,
        };

    [Fact]
    public async Task CreateAsync_happy_path_returns_draft_row()
    {
        var r = await _sut.CreateAsync(Req(), _userId);
        Assert.Equal("Draft", r.Status);
        Assert.Equal("sunny", r.WeatherCode);
        Assert.Equal("Nắng", r.WeatherLabel);
        Assert.Equal(12, r.HeadcountTotal);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_work()
    {
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(work: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_missing_weather()
    {
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(weather: ""), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_weather_code()
    {
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(weather: "meteor-shower"), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_inactive_weather_code()
    {
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(weather: "storm"), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_negative_headcount()
    {
        var req = Req();
        req.HeadcountLabor = -1;
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_project()
    {
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(projectId: 999_999), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_date_per_project()
    {
        var date = new DateOnly(2026, 7, 1);
        await _sut.CreateAsync(Req(date: date), _userId);
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.CreateAsync(Req(date: date), _userId));

        // Same date on a DIFFERENT project must succeed — the guard is
        // scoped per project.
        var other = await _sut.CreateAsync(Req(date: date, projectId: _otherProjectId), _userId);
        Assert.Equal("Draft", other.Status);
    }

    [Fact]
    public async Task UpdateAsync_only_allowed_on_draft()
    {
        var d = await _sut.CreateAsync(Req(), _userId);
        var submitted = await _sut.SubmitAsync(d.Id, _userId);
        Assert.Equal("Submitted", submitted.Status);

        var body = new UpdateSiteDiaryRequest
        {
            DiaryDate = d.DiaryDate,
            WeatherCode = "sunny",
            WorkPerformed = "changed",
            HeadcountLabor = 5,
            HeadcountEngineers = 1,
            HeadcountSupervisors = 0,
            HeadcountSubcontractors = 0,
        };
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.UpdateAsync(d.Id, body, _userId));
    }

    [Fact]
    public async Task UpdateAsync_can_move_date_when_no_conflict()
    {
        var d = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 1)), _userId);
        var body = new UpdateSiteDiaryRequest
        {
            DiaryDate = new DateOnly(2026, 7, 3),
            WeatherCode = "sunny",
            WorkPerformed = "moved",
            HeadcountLabor = 10,
            HeadcountEngineers = 1,
            HeadcountSupervisors = 1,
            HeadcountSubcontractors = 0,
        };
        var updated = await _sut.UpdateAsync(d.Id, body, _userId);
        Assert.Equal(new DateOnly(2026, 7, 3), updated!.DiaryDate);
    }

    [Fact]
    public async Task UpdateAsync_rejects_date_that_collides_with_another_diary()
    {
        var a = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 1)), _userId);
        var b = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 2)), _userId);
        var body = new UpdateSiteDiaryRequest
        {
            DiaryDate = a.DiaryDate,
            WeatherCode = "sunny",
            WorkPerformed = "would collide",
            HeadcountLabor = 1,
            HeadcountEngineers = 0,
            HeadcountSupervisors = 0,
            HeadcountSubcontractors = 0,
        };
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.UpdateAsync(b.Id, body, _userId));
    }

    [Fact]
    public async Task SubmitConfirmReopen_state_machine()
    {
        var d = await _sut.CreateAsync(Req(), _userId);
        var s = await _sut.SubmitAsync(d.Id, _userId);
        Assert.Equal("Submitted", s.Status);
        Assert.NotNull(s.SubmittedAt);

        var c = await _sut.ConfirmAsync(d.Id, _userId);
        Assert.Equal("Confirmed", c.Status);
        Assert.NotNull(c.ConfirmedAt);

        // Confirm again should fail (already Confirmed).
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.ConfirmAsync(d.Id, _userId));

        // Reopen clears the confirm/submit stamps so a resubmit recaptures
        // the fresh timeline.
        var r = await _sut.ReopenAsync(d.Id, _userId);
        Assert.Equal("Draft", r.Status);
        Assert.Null(r.SubmittedAt);
        Assert.Null(r.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmAsync_rejects_when_not_submitted()
    {
        var d = await _sut.CreateAsync(Req(), _userId);
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() =>
            _sut.ConfirmAsync(d.Id, _userId));
    }

    [Fact]
    public async Task DeleteAsync_only_allowed_on_draft()
    {
        var d = await _sut.CreateAsync(Req(), _userId);
        await _sut.SubmitAsync(d.Id, _userId);
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() => _sut.DeleteAsync(d.Id));

        await _sut.ReopenAsync(d.Id, _userId);
        Assert.True(await _sut.DeleteAsync(d.Id));
    }

    [Fact]
    public async Task BulkDeleteAsync_deletes_only_draft_and_reports_failures()
    {
        var a = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 1)), _userId);
        var b = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 2)), _userId);
        var c = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 3)), _userId);
        await _sut.SubmitAsync(b.Id, _userId); // b becomes Submitted → cannot delete

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
        await Assert.ThrowsAsync<SiteDiaryOperationException>(() => _sut.BulkDeleteAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task ListAsync_filters_by_project_status_date_and_search()
    {
        await _sut.CreateAsync(Req(date: new DateOnly(2026, 6, 1), work: "Lắp cốt thép"), _userId);
        await _sut.CreateAsync(Req(date: new DateOnly(2026, 6, 15), work: "Đổ bê tông trục A"), _userId);
        var late = await _sut.CreateAsync(Req(date: new DateOnly(2026, 7, 1), work: "Hoàn thiện"), _userId);
        await _sut.SubmitAsync(late.Id, _userId);

        var byProject = await _sut.ListAsync(new SiteDiaryListParams { DesignProjectId = _projectId });
        Assert.Equal(3, byProject.Total);
        Assert.Equal(2, byProject.StatusCounts["Draft"]);
        Assert.Equal(1, byProject.StatusCounts["Submitted"]);

        var byStatus = await _sut.ListAsync(new SiteDiaryListParams { DesignProjectId = _projectId, Status = "Submitted" });
        Assert.Single(byStatus.Items);

        var byDate = await _sut.ListAsync(new SiteDiaryListParams
        {
            DesignProjectId = _projectId,
            DateFrom = new DateOnly(2026, 6, 10),
            DateTo = new DateOnly(2026, 6, 30),
        });
        Assert.Single(byDate.Items);

        var bySearch = await _sut.ListAsync(new SiteDiaryListParams { DesignProjectId = _projectId, Search = "cốt thép" });
        Assert.Single(bySearch.Items);
    }

    public void Dispose() => _db.Dispose();
}
