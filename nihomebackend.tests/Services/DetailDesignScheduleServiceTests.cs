using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DetailDesignScheduleRulesTests
{
    public static IEnumerable<object[]> TransitionCases()
    {
        var allowed = new Dictionary<DesignScheduleStatus, DesignScheduleStatus[]>
        {
            [DesignScheduleStatus.NotStarted] =
                [DesignScheduleStatus.NotStarted, DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.OnHold, DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.InProgress] =
                [DesignScheduleStatus.InProgress, DesignScheduleStatus.Completed,
                    DesignScheduleStatus.OnHold, DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.Completed] = [DesignScheduleStatus.Completed],
            [DesignScheduleStatus.OnHold] =
                [DesignScheduleStatus.OnHold, DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.WaitingForDepartment] =
                [DesignScheduleStatus.WaitingForDepartment, DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.OnHold],
        };
        return from current in Enum.GetValues<DesignScheduleStatus>()
               from next in Enum.GetValues<DesignScheduleStatus>()
               select new object[] { current, next, allowed[current].Contains(next) };
    }

    [Fact]
    public void ValidateDatesAndStatus_EnforcesDateAndStateInvariants()
    {
        var start = new DateOnly(2026, 9, 1);

        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start.AddDays(-1), null, null, DesignScheduleStatus.NotStarted, 0, 10));
        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start.AddDays(1), null, null, DesignScheduleStatus.InProgress, 10, 10));
        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start, start, null, DesignScheduleStatus.Completed, 100, 10));
        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start.AddDays(1), null, null, DesignScheduleStatus.NotStarted, 10, 10));
        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start.AddDays(1), start, start, DesignScheduleStatus.OnHold, 10, 10));
        Assert.Throws<DesignScheduleOperationException>(() => DesignScheduleRules.ValidateDatesAndStatus(
            start, start.AddDays(1), null, null, DesignScheduleStatus.NotStarted, 0, 10, true));

        DesignScheduleRules.ValidateDatesAndStatus(
            start, start, start, start, DesignScheduleStatus.Completed, 100, 100, true);
    }

    [Theory]
    [MemberData(nameof(TransitionCases))]
    public void ValidateTransition_UsesApprovedMatrix(
        DesignScheduleStatus current,
        DesignScheduleStatus next,
        bool allowed)
    {
        var action = () => DesignScheduleRules.ValidateTransition(current, next);
        if (allowed) action();
        else Assert.Throws<DesignScheduleOperationException>(action);
    }

    [Fact]
    public void CalculateRollup_RequiresExactlyOneHundredWeightAndUsesPersistedProgress()
    {
        var ready = DesignScheduleRules.CalculateRollup([(40, 50), (60, 100)]);
        var notReady = DesignScheduleRules.CalculateRollup([(40, 50), (50, 100)]);

        Assert.True(ready.BaselineReady);
        Assert.Equal(80m, ready.Progress);
        Assert.False(notReady.BaselineReady);
        Assert.Null(notReady.Progress);
    }

    [Fact]
    public void HasCycle_DetectsDependencyCycle()
    {
        IReadOnlyDictionary<int, IReadOnlyCollection<int>> acyclic =
            new Dictionary<int, IReadOnlyCollection<int>> { [3] = [2], [2] = [1], [1] = [] };
        IReadOnlyDictionary<int, IReadOnlyCollection<int>> sharedPredecessor =
            new Dictionary<int, IReadOnlyCollection<int>>
            {
                [5] = [3, 4],
                [3] = [2],
                [4] = [2],
                [2] = [1],
                [1] = [],
            };
        IReadOnlyDictionary<int, IReadOnlyCollection<int>> cyclic =
            new Dictionary<int, IReadOnlyCollection<int>> { [3] = [2], [2] = [1], [1] = [3] };

        Assert.False(DesignScheduleRules.HasCycle(3, acyclic));
        Assert.False(DesignScheduleRules.HasCycle(5, sharedPredecessor));
        Assert.True(DesignScheduleRules.HasCycle(3, cyclic));
    }
}

public sealed class DetailDesignScheduleServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly DetailDesignScheduleService service;
    private readonly int callerId;
    private readonly int projectId;
    private readonly int phaseId;
    private readonly int activeMemberId;
    private readonly int otherProjectTaskId;

    public DetailDesignScheduleServiceTests()
    {
        var caller = User("0912000001", true);
        var memberUser = User("0912000002", true);
        var inactiveUser = User("0912000003", false);
        var otherUser = User("0912000004", true);
        var customer = new Customer { Name = "Schedule customer", Type = CustomerType.Company, SourceCode = "referral" };
        db.Add(customer);
        db.SaveChanges();
        var project = Project(customer.Id, "PJ-SCHEDULE-1");
        var otherProject = Project(customer.Id, "PJ-SCHEDULE-2");
        db.AddRange(project, otherProject);
        db.SaveChanges();
        var design = Design(project, customer.Id, "DP-SCHEDULE-1");
        var otherDesign = Design(otherProject, customer.Id, "DP-SCHEDULE-2");
        db.AddRange(design, otherDesign);
        db.SaveChanges();
        var member = Member(project.Id, memberUser.Id);
        var inactiveMember = Member(project.Id, inactiveUser.Id);
        var otherMember = Member(otherProject.Id, otherUser.Id);
        db.AddRange(member, inactiveMember, otherMember);
        db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "project-department",
            Code = "design",
            Name = "Design",
            IsActive = true,
        });
        db.SaveChanges();
        var phase = Phase(project.Id, design.Id);
        var otherPhase = Phase(otherProject.Id, otherDesign.Id);
        db.AddRange(phase, otherPhase);
        db.SaveChanges();
        var otherTask = Task(otherProject.Id, otherDesign.Id, otherPhase.Id, otherMember.Id, "OTHER-1");
        db.Add(otherTask);
        db.SaveChanges();

        callerId = caller.Id;
        projectId = project.Id;
        phaseId = phase.Id;
        activeMemberId = member.Id;
        otherProjectTaskId = otherTask.Id;
        var access = new Mock<IProjectAccessService>();
        access.Setup(item => item.CanManageDesignScheduleAsync(callerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        service = new DetailDesignScheduleService(db, access.Object);
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsInactiveDepartmentAndMember()
    {
        var inactiveDepartment = Request(activeMemberId);
        inactiveDepartment.DepartmentCode = "unknown";
        var inactiveMember = await db.OperationalProjectMembers
            .SingleAsync(item => item.OperationalProjectId == projectId && !item.User.IsActive);

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, inactiveDepartment, callerId, default));
        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, Request(inactiveMember.Id), callerId, default));
        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsCrossProjectPredecessorWithoutChangingState()
    {
        var request = Request(activeMemberId);
        request.PredecessorTaskIds = [otherProjectTaskId];

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    public void Dispose() => db.Dispose();

    private ApplicationUser User(string phone, bool active)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            Email = $"{phone}@example.test",
            FullName = phone,
            PasswordHash = "test",
            IsActive = active,
        };
        db.Add(user);
        db.SaveChanges();
        return user;
    }

    private static OperationalProject Project(int customerId, string code) => new()
    {
        CustomerId = customerId,
        Code = code,
        Name = code,
    };

    private static DesignProject Design(OperationalProject project, int customerId, string code) => new()
    {
        OperationalProjectId = project.Id,
        CustomerId = customerId,
        ProjectCode = code,
        Name = code,
        StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        Deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static OperationalProjectMember Member(int projectId, int userId) => new()
    {
        OperationalProjectId = projectId,
        UserId = userId,
        Position = "Designer",
        StartedAt = DateTime.UtcNow.AddDays(-1),
        CreatedByUserId = userId,
        UpdatedByUserId = userId,
    };

    private static DesignSchedulePhase Phase(int projectId, int designProjectId) => new()
    {
        OperationalProjectId = projectId,
        DesignProjectId = designProjectId,
        Code = DesignSchedulePhaseCode.Concept,
        PlannedStart = new DateOnly(2026, 9, 1),
        PlannedEnd = new DateOnly(2026, 10, 1),
        Status = DesignScheduleStatus.NotStarted,
        Weight = 34,
        CreatedByUserId = 1,
        UpdatedByUserId = 1,
    };

    private static DesignScheduleTask Task(
        int projectId,
        int designProjectId,
        int phaseId,
        int memberId,
        string code) => new()
        {
            OperationalProjectId = projectId,
            DesignProjectId = designProjectId,
            PhaseId = phaseId,
            Code = code,
            Name = code,
            DepartmentCode = "design",
            AssigneeMemberId = memberId,
            PlannedStart = new DateOnly(2026, 9, 1),
            PlannedEnd = new DateOnly(2026, 9, 2),
            Status = DesignScheduleStatus.NotStarted,
            Weight = 100,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };

    private static UpsertDesignScheduleTaskRequest Request(int memberId) => new()
    {
        Code = "DES-001",
        Name = "Design task",
        DepartmentCode = "design",
        AssigneeMemberId = memberId,
        PlannedStart = new DateOnly(2026, 9, 1),
        PlannedEnd = new DateOnly(2026, 9, 2),
        Status = "NotStarted",
        ProgressPercent = 0,
        Weight = 100,
    };
}