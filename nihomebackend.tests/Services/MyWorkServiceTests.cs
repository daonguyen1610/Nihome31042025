using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class MyWorkServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IPermissionService> _permissions = new();
    private readonly MyWorkService _myWork;
    private readonly DailyWorkReportService _reports;
    private readonly RoleGroup _design;
    private readonly RoleGroup _finance;
    private readonly ApplicationUser _designHead;
    private readonly ApplicationUser _architect;
    private readonly ApplicationUser _engineer;
    private readonly ApplicationUser _financeHead;
    private readonly DepartmentWorkPackage _package;
    private readonly DateOnly _today;

    public MyWorkServiceTests()
    {
        _design = AddGroup("DESIGN");
        _finance = AddGroup("FINANCE");
        _designHead = AddUser("Trưởng phòng Thiết kế", _design);
        _architect = AddUser("KTS Nguyễn Minh", _design);
        _engineer = AddUser("KS Trần Hà", _design);
        _financeHead = AddUser("Trưởng phòng Tài chính", _finance);
        // Staff roles such as PROCUREMENT hold work.packages.assign too; only the review permission makes a head.
        _permissions.Setup(item => item.HasAsync(It.IsAny<int>(), "work.packages.assign", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _permissions.Setup(item => item.HasAsync(It.IsAny<int>(), DailyWorkReportService.ReviewPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int userId, string _, CancellationToken _) => userId == _designHead.Id || userId == _financeHead.Id);
        _today = DisplaySettings.TodayAsync(_db, CancellationToken.None).GetAwaiter().GetResult();
        _package = new DepartmentWorkPackage
        {
            Code = "WP-INT-TRAINING",
            Kind = WorkPackageKind.Internal,
            Name = "Đào tạo nội bộ quy trình BIM",
            DepartmentRoleGroupId = _design.Id,
            LeadUserId = _designHead.Id,
            CreatedByUserId = _designHead.Id,
            UpdatedByUserId = _designHead.Id,
        };
        _db.DepartmentWorkPackages.Add(_package);
        _db.SaveChanges();
        _myWork = new MyWorkService(_db);
        _reports = new DailyWorkReportService(_db, _myWork, _permissions.Object);
    }

    [Fact]
    public async Task MyWork_GroupsOnlyTheCallersOpenTasks()
    {
        var overdue = AddTask("01", _architect, -5, -2);
        var today = AddTask("02", _architect, -1, 1, priority: true);
        var soon = AddTask("03", _architect, 2, 3);
        var later = AddTask("04", _architect, 10, 20);
        AddTask("05", _architect, -1, 1, WorkTaskStatus.Completed);
        AddTask("06", _architect, -1, 1, WorkTaskStatus.Cancelled);
        AddTask("07", _engineer, -1, 1);

        var result = await _myWork.GetAsync(_architect.Id, null, null);

        Assert.Equal(new[] { overdue.Id, today.Id, soon.Id, later.Id }, result.Items.Select(item => item.Id));
        var byId = result.Items.ToDictionary(item => item.Id);
        Assert.True(byId[overdue.Id].IsOverdue);
        Assert.Equal(2, byId[overdue.Id].OverdueDays);
        Assert.True(byId[today.Id].IsToday);
        Assert.True(byId[today.Id].IsPriority);
        Assert.True(byId[soon.Id].IsDueSoon);
        Assert.False(byId[later.Id].IsDueSoon || byId[later.Id].IsToday || byId[later.Id].IsOverdue);
        Assert.All(result.Items, item => Assert.Equal("Assigned", item.Relation));
        Assert.All(result.Items, item => Assert.True(item.IsInternal));
        Assert.Equal($"/admin/work-packages/{_package.Id}?taskId={today.Id}", byId[today.Id].LinkUrl);

        // The head who created the tasks sees them as work they gave to others.
        var head = await _myWork.GetAsync(_designHead.Id, null, null);
        Assert.Equal(5, head.Items.Count);
        Assert.All(head.Items, item => Assert.Equal("Created", item.Relation));
    }

    [Fact]
    public async Task MyWorkPlan_ListsFinishedWorkInTheRangeAndRejectsAHalfRange()
    {
        var done = AddTask("01", _architect, -3, -2, WorkTaskStatus.Completed);
        AddTask("02", _architect, 30, 40);

        var week = await _myWork.GetAsync(_architect.Id, _today.AddDays(-7), _today);

        var item = Assert.Single(week.Items);
        Assert.Equal(done.Id, item.Id);
        Assert.False(item.IsOpen);
        Assert.False(item.IsOverdue);
        await Assert.ThrowsAsync<ArgumentException>(() => _myWork.GetAsync(_architect.Id, _today, null));
        await Assert.ThrowsAsync<ArgumentException>(() => _myWork.GetAsync(_architect.Id, _today, _today.AddDays(-1)));
    }

    [Fact]
    public async Task MyWork_IncludesDesignScheduleTasksAssignedToMe()
    {
        var project = new DesignProject { ProjectCode = "TK-2026-014", Name = "Văn phòng NICON Bình Dương" };
        _db.DesignProjects.Add(project);
        var member = new OperationalProjectMember { UserId = _architect.Id, User = _architect, Position = "KTS chủ trì" };
        _db.Add(member);
        _db.DesignScheduleTasks.Add(new DesignScheduleTask
        {
            DesignProject = project,
            Code = "KT-01",
            Name = "Mặt bằng kiến trúc tầng 1",
            AssigneeMember = member,
            ApproverMemberId = member.Id,
            Status = DesignScheduleStatus.InProgress,
            IsCritical = true,
            PlannedStart = _today.AddDays(-2),
            PlannedEnd = _today,
            CreatedByUserId = _designHead.Id,
        });
        _db.SaveChanges();

        var item = Assert.Single((await _myWork.GetAsync(_architect.Id, null, null)).Items);

        Assert.Equal(MyWorkSources.DesignTask, item.Source);
        Assert.Equal("TK-2026-014", item.ContainerCode);
        Assert.True(item.IsToday);
        Assert.True(item.IsPriority);
        Assert.Equal($"/admin/design-projects/{project.Id}?tab=schedule", item.LinkUrl);
    }

    [Fact]
    public async Task DailyReport_IsPrefilledSubmittedAndReviewedByTheDepartmentHeadOnly()
    {
        var inProgress = AddTask("01", _architect, -3, 3, WorkTaskStatus.InProgress, progress: 40);
        AddTask("02", _architect, 5, 9);

        var draft = await _reports.GetTodayAsync(_architect.Id);
        Assert.Equal(0, draft.Id);
        var line = Assert.Single(draft.Items);
        Assert.Equal(inProgress.Id, line.SourceId);
        Assert.Equal(40, line.ProgressPercent);

        var submitted = await _reports.SubmitTodayAsync(_architect.Id, new SubmitDailyWorkReportRequest
        {
            Items = [new() { SourceType = line.SourceType, SourceId = line.SourceId, Title = line.Title, ProgressPercent = 60 }],
            Proposals = "Đề xuất bổ sung license Revit",
            TomorrowPlan = "Hoàn thiện mặt cắt tầng 2",
        });
        Assert.NotEqual(0, submitted.Id);
        Assert.True(submitted.CanEdit);
        // Sending again replaces today's report instead of adding a second one.
        await _reports.SubmitTodayAsync(_architect.Id, new SubmitDailyWorkReportRequest
        {
            Items = [new() { Title = "Họp phối hợp MEP", ProgressPercent = 100 }],
            TomorrowPlan = "Hoàn thiện mặt cắt tầng 2",
        });
        Assert.Equal(1, _db.DailyWorkReports.Count());

        var department = await _reports.DepartmentAsync(_designHead.Id, null, null, canViewAll: false);
        var report = Assert.Single(department.Reports);
        Assert.Equal("Họp phối hợp MEP", Assert.Single(report.Items).Title);
        Assert.True(report.CanReview);
        Assert.Contains(department.MissingMembers, member => member.UserId == _engineer.Id);
        Assert.DoesNotContain(department.MissingMembers, member => member.UserId == _architect.Id);

        await Assert.ThrowsAsync<DailyWorkReportScopeException>(() =>
            _reports.DepartmentAsync(_engineer.Id, null, null, canViewAll: false));
        await Assert.ThrowsAsync<DailyWorkReportScopeException>(() =>
            _reports.DepartmentAsync(_financeHead.Id, null, _design.Id, canViewAll: false));
        await Assert.ThrowsAsync<DailyWorkReportScopeException>(() =>
            _reports.ReviewAsync(_financeHead.Id, report.Id, new ReviewDailyWorkReportRequest { Comment = "OK" }, false));
        await Assert.ThrowsAsync<DailyWorkReportScopeException>(() =>
            _reports.ReviewAsync(_engineer.Id, report.Id, new ReviewDailyWorkReportRequest { Comment = "OK" }, false));

        var reviewed = await _reports.ReviewAsync(_designHead.Id, report.Id,
            new ReviewDailyWorkReportRequest { Comment = "Ưu tiên xong mặt cắt trước thứ Sáu" }, false);
        Assert.Equal("Ưu tiên xong mặt cắt trước thứ Sáu", reviewed.ReviewComment);

        var mine = await _reports.GetTodayAsync(_architect.Id);
        Assert.False(mine.CanEdit);
        var locked = await Assert.ThrowsAsync<DailyWorkReportException>(() =>
            _reports.SubmitTodayAsync(_architect.Id, new SubmitDailyWorkReportRequest { TomorrowPlan = "Sửa lại" }));
        Assert.Equal(DailyWorkReportService.Reviewed, locked.Code);
    }

    [Fact]
    public async Task DailyReport_HeadCannotReviewTheirOwnReport()
    {
        var own = await _reports.SubmitTodayAsync(_designHead.Id, new SubmitDailyWorkReportRequest { TomorrowPlan = "Duyệt hồ sơ" });

        var error = await Assert.ThrowsAsync<DailyWorkReportException>(() =>
            _reports.ReviewAsync(_designHead.Id, own.Id, new ReviewDailyWorkReportRequest { Comment = "Tốt" }, false));

        Assert.Equal(DailyWorkReportService.OwnReport, error.Code);
    }

    private DepartmentWorkTask AddTask(
        string wbs, ApplicationUser assignee, int startOffset, int endOffset,
        WorkTaskStatus status = WorkTaskStatus.Planned, bool priority = false, int progress = 0)
    {
        var task = new DepartmentWorkTask
        {
            WorkPackageId = _package.Id,
            WbsCode = $"INT-{wbs}",
            Title = $"Việc nội bộ {wbs}",
            DepartmentRoleGroupId = _design.Id,
            AssigneeUserId = assignee.Id,
            Status = status,
            IsPriority = priority,
            ProgressPercent = progress,
            PlannedStart = _today.AddDays(startOffset).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            PlannedEnd = _today.AddDays(endOffset).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            CreatedByUserId = _designHead.Id,
            UpdatedByUserId = _designHead.Id,
        };
        _db.DepartmentWorkTasks.Add(task);
        _db.SaveChanges();
        return task;
    }

    private RoleGroup AddGroup(string code)
    {
        var group = new RoleGroup { Code = code, LabelKey = $"rbac.group.{code.ToLowerInvariant()}" };
        _db.RoleGroups.Add(group);
        _db.SaveChanges();
        return group;
    }

    private ApplicationUser AddUser(string name, RoleGroup group)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = $"09{_db.Users.Count() + 1:00000000}",
            Email = $"user{_db.Users.Count() + 1}@nicon.test",
            FullName = name,
            Role = UserRole.USER,
            RoleEntity = new Role { Code = $"{group.Code}_{_db.Users.Count()}", Name = name, RoleGroupId = group.Id, IsActive = true },
            IsActive = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    public void Dispose() => _db.Dispose();
}
