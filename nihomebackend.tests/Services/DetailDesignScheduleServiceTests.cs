using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
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
                [DesignScheduleStatus.NotStarted,
                    DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.OnHold,
                    DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.InProgress] =
                [DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.Completed,
                    DesignScheduleStatus.OnHold,
                    DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.Completed] = [DesignScheduleStatus.Completed],
            [DesignScheduleStatus.OnHold] =
                [DesignScheduleStatus.OnHold,
                    DesignScheduleStatus.InProgress,
                    DesignScheduleStatus.WaitingForDepartment],
            [DesignScheduleStatus.WaitingForDepartment] =
                [DesignScheduleStatus.WaitingForDepartment,
                    DesignScheduleStatus.InProgress,
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
    private readonly int designProjectId;
    private readonly int phaseId;
    private readonly int activeMemberId;
    private readonly int approverMemberId;
    private readonly int otherProjectTaskId;
    private readonly int assigneeUserId;
    private readonly Mock<INotificationService> notifications = new();

    public DetailDesignScheduleServiceTests()
    {
        var caller = User("0912000001", true);
        var memberUser = User("0912000002", true);
        var approverUser = User("0912000005", true);
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
        design.DesignLeadUserId = approverUser.Id;
        var otherDesign = Design(otherProject, customer.Id, "DP-SCHEDULE-2");
        db.AddRange(design, otherDesign);
        db.SaveChanges();
        var member = Member(project.Id, memberUser.Id);
        var approver = Member(project.Id, approverUser.Id);
        var inactiveMember = Member(project.Id, inactiveUser.Id);
        var otherMember = Member(otherProject.Id, otherUser.Id);
        member.DesignProjectId = design.Id;
        approver.DesignProjectId = design.Id;
        inactiveMember.DesignProjectId = design.Id;
        otherMember.DesignProjectId = otherDesign.Id;
        db.AddRange(member, approver, inactiveMember, otherMember);
        db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "design_discipline",
            Code = "architecture",
            Name = "Architecture",
            IsActive = true,
        });
        db.RoleGroups.Add(new RoleGroup
        {
            Code = "DESIGN",
            LabelKey = "rbac.group.design",
            IsActive = true,
        });
        db.SaveChanges();
        var phase = Phase(project.Id, design.Id);
        var otherPhase = Phase(otherProject.Id, otherDesign.Id);
        db.AddRange(phase, otherPhase);
        db.SaveChanges();
        var otherTask = Task(otherProject.Id, otherDesign.Id, otherPhase.Id, otherMember.Id, otherMember.Id, "OTHER-1");
        db.Add(otherTask);
        db.SaveChanges();

        callerId = caller.Id;
        projectId = project.Id;
        designProjectId = design.Id;
        phaseId = phase.Id;
        activeMemberId = member.Id;
        approverMemberId = approver.Id;
        otherProjectTaskId = otherTask.Id;
        assigneeUserId = memberUser.Id;
        var access = new Mock<IProjectAccessService>();
        access.Setup(item => item.CanViewOperationalProjectAsync(callerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        access.Setup(item => item.CanViewDesignProjectAsync(callerId, designProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        access.Setup(item => item.CanManageDesignScheduleAsync(callerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        access.Setup(item => item.CanManageDesignProjectAsync(callerId, designProjectId, It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(true);
        service = new DetailDesignScheduleService(
            db, access.Object, notifications.Object,
            NullLogger<DetailDesignScheduleService>.Instance);
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsInactiveDepartmentAndMember()
    {
        var inactiveDepartment = Request(activeMemberId, approverMemberId);
        inactiveDepartment.DepartmentCode = "unknown";
        var inactiveMember = await db.OperationalProjectMembers
            .SingleAsync(item => item.OperationalProjectId == projectId && !item.User.IsActive);

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, inactiveDepartment, callerId, default));
        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, Request(inactiveMember.Id, approverMemberId), callerId, default));
        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task CreateTaskAsync_AcceptsActiveRoleGroupAndNormalizesDiscipline()
    {
        var request = Request(activeMemberId, approverMemberId);
        request.DepartmentCode = "design";
        request.DisciplineCode = " Architecture ";

        var result = await service.CreateTaskAsync(projectId, phaseId, request, callerId, default);

        Assert.NotNull(result);
        Assert.Equal("DESIGN", result.DepartmentCode);
        Assert.Equal("architecture", result.DisciplineCode);
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsInactiveRoleGroupDepartment()
    {
        db.RoleGroups.Add(new RoleGroup
        {
            Code = "OLD_UNIT",
            LabelKey = "rbac.group.old-unit",
            IsActive = false,
        });
        await db.SaveChangesAsync();
        var request = Request(activeMemberId, approverMemberId);
        request.DepartmentCode = "old_unit";

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsUnknownDiscipline()
    {
        var request = Request(activeMemberId, approverMemberId);
        request.DisciplineCode = "unknown";

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task GetAsync_FiltersTasksByDisciplineCode()
    {
        var architectureTask = Task(projectId, designProjectId, phaseId,
            activeMemberId, approverMemberId, "DIS-ARCH");
        architectureTask.DisciplineCode = "architecture";
        var structureTask = Task(projectId, designProjectId, phaseId,
            activeMemberId, approverMemberId, "DIS-STRUCT");
        structureTask.DisciplineCode = "structure";
        db.DesignScheduleTasks.AddRange(architectureTask, structureTask);
        await db.SaveChangesAsync();

        var response = await service.GetAsync(projectId,
            new DesignScheduleQuery { DisciplineCode = "architecture" }, callerId, default);

        Assert.NotNull(response);
        var item = Assert.Single(response.Tasks.Items);
        Assert.Equal("DIS-ARCH", item.Code);
        Assert.Equal("architecture", item.DisciplineCode);
    }

    [Fact]
    public async Task GetAsync_FiltersTasksWaitingForApproval()
    {
        var pending = Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, "WAIT-001");
        pending.ApprovalStatus = DesignTaskApprovalStatus.Pending;
        var approved = Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, "WAIT-002");
        approved.ApprovalStatus = DesignTaskApprovalStatus.Approved;
        db.DesignScheduleTasks.AddRange(pending, approved);
        await db.SaveChangesAsync();

        var response = await service.GetAsync(projectId,
            new DesignScheduleQuery { PendingApprovalOnly = true }, callerId, default);

        Assert.NotNull(response);
        Assert.Equal(1, response.Tasks.TotalCount);
        Assert.Equal("WAIT-001", Assert.Single(response.Tasks.Items).Code);
    }

    [Fact]
    public async Task ApprovalInbox_ListsOnlyWorkWaitingOnTheCaller()
    {
        var approverUserId = SeedInbox();
        var inbox = new DesignApprovalInboxService(db);

        var summary = await inbox.GetSummaryAsync(approverUserId, default);
        var tasks = await inbox.ListTasksAsync(approverUserId, new DesignApprovalInboxQuery(), default);
        var reviews = await inbox.ListReviewsAsync(approverUserId, new DesignApprovalInboxQuery(), default);
        var someoneElse = await inbox.ListTasksAsync(assigneeUserId, new DesignApprovalInboxQuery(), default);

        Assert.Equal(3, summary.TaskCount);
        Assert.Equal(2, summary.ReviewCount);
        Assert.Equal(designProjectId, Assert.Single(summary.Dossiers).Id);
        Assert.Equal(["INBOX-001", "INBOX-002", "INBOX-003"], tasks.Items.Select(item => item.Code));
        Assert.Equal(3, tasks.Total);
        Assert.Equal(2, reviews.Total);
        Assert.Equal("INBOX-901", Assert.Single(someoneElse.Items).Code);
        Assert.Equal(0, (await inbox.GetSummaryAsync(assigneeUserId, default)).ReviewCount);
    }

    [Fact]
    public async Task ApprovalInbox_HidesTasksOnceTheApproverHasLeftTheTeam()
    {
        var approverUserId = SeedInbox();
        var approver = db.OperationalProjectMembers.Single(item => item.Id == approverMemberId);
        approver.EndedAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        var inbox = new DesignApprovalInboxService(db);

        var summary = await inbox.GetSummaryAsync(approverUserId, default);
        var tasks = await inbox.ListTasksAsync(approverUserId, new DesignApprovalInboxQuery(), default);

        // They can no longer approve these tasks, so they are not asked to.
        Assert.Equal(0, summary.TaskCount);
        Assert.Equal(0, tasks.Total);
    }

    [Fact]
    public async Task ApprovalInbox_SearchesSortsAndPagesTasks()
    {
        var approverUserId = SeedInbox();
        var inbox = new DesignApprovalInboxService(db);

        var search = await inbox.ListTasksAsync(approverUserId,
            new DesignApprovalInboxQuery { Search = " Mặt đứng " }, default);
        var newestSecondPage = await inbox.ListTasksAsync(approverUserId,
            new DesignApprovalInboxQuery { NewestFirst = true, Page = 2, PageSize = 2 }, default);
        var otherDossier = await inbox.ListTasksAsync(approverUserId,
            new DesignApprovalInboxQuery { DesignProjectId = designProjectId + 1000 }, default);

        Assert.Equal("INBOX-002", Assert.Single(search.Items).Code);
        Assert.Equal(3, newestSecondPage.Total);
        Assert.Equal("INBOX-001", Assert.Single(newestSecondPage.Items).Code);
        Assert.Equal(0, otherDossier.Total);
    }

    [Fact]
    public async Task ApprovalInbox_FiltersReviewsByStageAndOwnComment()
    {
        var approverUserId = SeedInbox();
        var inbox = new DesignApprovalInboxService(db);

        var basic = await inbox.ListReviewsAsync(approverUserId,
            new DesignApprovalInboxQuery { Stage = "basicdesign" }, default);
        var commented = await inbox.ListReviewsAsync(approverUserId,
            new DesignApprovalInboxQuery { Commented = true }, default);
        var notCommented = await inbox.ListReviewsAsync(approverUserId,
            new DesignApprovalInboxQuery { Commented = false }, default);

        Assert.Equal("Hồ sơ cơ sở kết cấu", Assert.Single(basic.Items).Title);
        var mine = Assert.Single(commented.Items);
        Assert.Equal("Phương án mặt tiền", mine.Title);
        Assert.True(mine.HasMyComment);
        Assert.Equal("Hồ sơ cơ sở kết cấu", Assert.Single(notCommented.Items).Title);
        var invalid = await Assert.ThrowsAsync<DesignApprovalInboxOperationException>(() =>
            inbox.ListReviewsAsync(approverUserId, new DesignApprovalInboxQuery { Stage = "Permit" }, default));
        Assert.Contains("Giai đoạn không hợp lệ", invalid.Message);
    }

    /// <summary>Three pending tasks and two internal reviews for the approver, plus work that is not theirs.</summary>
    private int SeedInbox()
    {
        var approverUserId = db.OperationalProjectMembers.Single(item => item.Id == approverMemberId).UserId;
        var submitted = new DateTime(2026, 11, 10, 2, 0, 0, DateTimeKind.Utc);
        DesignScheduleTask PendingTask(string code, string name, int day)
        {
            var task = Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, code);
            task.Name = name;
            task.ApprovalStatus = DesignTaskApprovalStatus.Pending;
            task.SubmittedAt = submitted.AddDays(day);
            return task;
        }
        var approved = Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, "INBOX-900");
        approved.ApprovalStatus = DesignTaskApprovalStatus.Approved;
        var notMine = Task(projectId, designProjectId, phaseId, approverMemberId, activeMemberId, "INBOX-901");
        notMine.ApprovalStatus = DesignTaskApprovalStatus.Pending;
        db.DesignScheduleTasks.AddRange(
            PendingTask("INBOX-001", "Mặt bằng tầng điển hình", 0),
            PendingTask("INBOX-002", "Mặt đứng chính", 1),
            PendingTask("INBOX-003", "Mặt cắt dọc", 2),
            approved, notMine);
        DesignReview Review(string title, DesignReviewStage stage, DesignReviewStatus status, int day) => new()
        {
            DesignProjectId = designProjectId,
            Stage = stage,
            RevisionNumber = 1,
            Title = title,
            Status = status,
            DesignManagerUserId = callerId,
            InternalSubmittedAt = submitted.AddDays(day),
            Recipients = [new DesignReviewRecipient { UserId = approverUserId }],
        };
        var commented = Review("Phương án mặt tiền", DesignReviewStage.Concept, DesignReviewStatus.InternalReview, 0);
        commented.Comments = [new DesignReviewComment { AuthorUserId = approverUserId, Body = "Đề nghị tăng lam che nắng." }];
        db.DesignReviews.AddRange(
            commented,
            Review("Hồ sơ cơ sở kết cấu", DesignReviewStage.BasicDesign, DesignReviewStatus.InternalReview, 1),
            Review("Phương án đã gửi khách", DesignReviewStage.Concept, DesignReviewStatus.SubmittedToClient, 2));
        db.SaveChanges();
        return approverUserId;
    }

    [Fact]
    public async Task TaskAssignment_NotifiesEachNewAssigneeOnce()
    {
        var created = await service.CreateTaskAsync(projectId, phaseId,
            Request(activeMemberId, approverMemberId), callerId, default);
        var sameAssignee = Request(activeMemberId, approverMemberId);
        sameAssignee.Name = "Mặt bằng tầng điển hình – chỉnh sửa";
        sameAssignee.RowVersion = created!.RowVersion;
        var renamed = await service.UpdateTaskAsync(projectId, created.Id, sameAssignee, callerId, default);
        var newcomerUser = User("0912000006", true);
        var newcomer = Member(projectId, newcomerUser.Id);
        newcomer.DesignProjectId = designProjectId;
        db.Add(newcomer);
        db.SaveChanges();
        var reassigned = Request(newcomer.Id, approverMemberId);
        reassigned.RowVersion = renamed!.RowVersion;

        await service.UpdateTaskAsync(projectId, created.Id, reassigned, callerId, default);

        void Assigned(int userId) => notifications.Verify(item => item.NotifyManyFromTemplateAsync(
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { userId })), "design-task.assigned",
            It.Is<IDictionary<string, string>>(data => data["taskCode"] == "DES-001" && data["plannedEnd"] == "02/09/2026"),
            "DesignScheduleTask", created.Id, $"/admin/design-projects/{designProjectId}?tab=schedule", "vi"), Times.Once);
        Assigned(assigneeUserId);
        Assigned(newcomerUser.Id);
    }

    [Fact]
    public async Task CreateTaskAsync_WithoutCode_NumbersTasksWithinThePhase()
    {
        // NICON design feedback: a designer types the task name, not an internal code.
        db.DesignScheduleTasks.Add(Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, "YT-02"));
        await db.SaveChangesAsync();
        var first = Request(activeMemberId, approverMemberId);
        first.Code = "  ";
        var second = Request(activeMemberId, approverMemberId);
        second.Code = "";

        var a = await service.CreateTaskAsync(projectId, phaseId, first, callerId, default);
        var b = await service.CreateTaskAsync(projectId, phaseId, second, callerId, default);

        // Concept phase codes start with YT; an existing YT-02 is skipped, never duplicated.
        Assert.Equal("YT-03", a!.Code);
        Assert.Equal("YT-04", b!.Code);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithoutCode_KeepsTheTaskCode()
    {
        var created = await service.CreateTaskAsync(projectId, phaseId,
            Request(activeMemberId, approverMemberId), callerId, default);
        var update = Request(activeMemberId, approverMemberId);
        update.Code = "";
        update.Name = "Mặt bằng tầng 1 – chỉnh sửa";
        update.RowVersion = created!.RowVersion;

        var updated = await service.UpdateTaskAsync(projectId, created.Id, update, callerId, default);

        Assert.Equal("DES-001", updated!.Code);
        Assert.Equal("Mặt bằng tầng 1 – chỉnh sửa", updated.Name);
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsCrossProjectPredecessorWithoutChangingState()
    {
        var request = Request(activeMemberId, approverMemberId);
        request.PredecessorTaskIds = [otherProjectTaskId];

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task CreateTaskAsync_RequiresMatchingDownstreamDesignContract()
    {
        var vendor = new Vendor
        {
            VendorCode = "NCC-TK-001",
            CompanyName = "Công ty Tư vấn Thiết kế NICON",
            VendorType = VendorType.SubContractor,
            CreatedByUserId = callerId,
        };
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        var project = await db.OperationalProjects.SingleAsync(item => item.Id == projectId);
        var contract = new Contract
        {
            ContractNumber = "HD-TK-DAU-VAO-001",
            CustomerId = project.CustomerId,
            OperationalProjectId = projectId,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Design,
            VendorId = vendor.Id,
            Status = ContractStatus.Signed,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        var request = Request(activeMemberId, approverMemberId);
        request.VendorId = vendor.Id;
        request.DownstreamContractId = contract.Id;

        var result = await service.CreateTaskAsync(projectId, phaseId, request, callerId, default);

        Assert.NotNull(result);
        Assert.Equal(vendor.Id, result.VendorId);
        Assert.Equal(vendor.CompanyName, result.VendorName);
        Assert.Equal(contract.Id, result.DownstreamContractId);
        Assert.Equal(contract.ContractNumber, result.DownstreamContractNumber);
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsVendorWithoutContractAndLeavesStateUnchanged()
    {
        var request = Request(activeMemberId, approverMemberId);
        request.VendorId = 999;

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task CreateTaskAsync_RejectsContractForAnotherVendorAndLeavesStateUnchanged()
    {
        var selectedVendor = new Vendor
        {
            VendorCode = "NCC-TK-SELECTED",
            CompanyName = "Nhà thầu thiết kế được chọn",
            VendorType = VendorType.SubContractor,
            CreatedByUserId = callerId,
        };
        var contractVendor = new Vendor
        {
            VendorCode = "NCC-TK-CONTRACT",
            CompanyName = "Nhà thầu đứng tên hợp đồng",
            VendorType = VendorType.SubContractor,
            CreatedByUserId = callerId,
        };
        db.Vendors.AddRange(selectedVendor, contractVendor);
        await db.SaveChangesAsync();
        var project = await db.OperationalProjects.SingleAsync(item => item.Id == projectId);
        var contract = new Contract
        {
            ContractNumber = "HD-TK-SAI-NCC-001",
            CustomerId = project.CustomerId,
            OperationalProjectId = projectId,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Design,
            VendorId = contractVendor.Id,
            Status = ContractStatus.Signed,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        var request = Request(activeMemberId, approverMemberId);
        request.VendorId = selectedVendor.Id;
        request.DownstreamContractId = contract.Id;

        await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.CreateTaskAsync(projectId, phaseId, request, callerId, default));

        Assert.Empty(db.DesignScheduleTasks.Where(item => item.OperationalProjectId == projectId));
    }

    [Fact]
    public async Task UpdateTaskProgressAsync_AssigneeStartsThenReportsProgress()
    {
        var task = AddOwnTask("PROGRESS-001");

        var started = await service.UpdateTaskProgressAsync(projectId, task.Id,
            new UpdateDesignScheduleTaskProgressRequest { ProgressPercent = 0, RowVersion = Version(task) },
            assigneeUserId, CancellationToken.None);
        var updated = await service.UpdateTaskProgressAsync(projectId, task.Id,
            new UpdateDesignScheduleTaskProgressRequest { ProgressPercent = 40, RowVersion = started!.RowVersion },
            assigneeUserId, CancellationToken.None);

        Assert.Equal("InProgress", updated!.Status);
        Assert.Equal(40, updated.ProgressPercent);
        Assert.NotNull(updated.ActualStart);
        var actions = await db.DesignScheduleHistory
            .Where(item => item.EntityType == "Task" && item.EntityId == task.Id)
            .Select(item => item.Action).ToListAsync();
        Assert.Contains("Started", actions);
        Assert.Contains("ProgressUpdated", actions);
    }

    [Fact]
    public async Task UpdateTaskProgressAsync_RejectsSomeoneOtherThanAssigneeWithoutChangingTask()
    {
        var task = AddOwnTask("PROGRESS-002");

        var error = await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.UpdateTaskProgressAsync(projectId, task.Id,
                new UpdateDesignScheduleTaskProgressRequest { ProgressPercent = 30, RowVersion = Version(task) },
                callerId, CancellationToken.None));

        Assert.Contains("người phụ trách", error.Message);
        var stored = await db.DesignScheduleTasks.AsNoTracking().SingleAsync(item => item.Id == task.Id);
        Assert.Equal(DesignScheduleStatus.NotStarted, stored.Status);
        Assert.Equal(0, stored.ProgressPercent);
    }

    [Theory]
    [InlineData(DesignScheduleStatus.InProgress, DesignTaskApprovalStatus.Pending, 50, "chờ duyệt")]
    [InlineData(DesignScheduleStatus.Completed, DesignTaskApprovalStatus.Approved, 50, "đã được duyệt")]
    [InlineData(DesignScheduleStatus.OnHold, DesignTaskApprovalStatus.NotSubmitted, 50, "tạm dừng")]
    [InlineData(DesignScheduleStatus.InProgress, DesignTaskApprovalStatus.NotSubmitted, 100, "Hoàn thành")]
    public async Task UpdateTaskProgressAsync_RejectsLockedTasksAndFullProgress(
        DesignScheduleStatus status,
        DesignTaskApprovalStatus approval,
        int progress,
        string expectedMessage)
    {
        var task = AddOwnTask($"PROGRESS-{status}-{approval}-{progress}", status, approval);

        var error = await Assert.ThrowsAsync<DesignScheduleOperationException>(() =>
            service.UpdateTaskProgressAsync(projectId, task.Id,
                new UpdateDesignScheduleTaskProgressRequest { ProgressPercent = progress, RowVersion = Version(task) },
                assigneeUserId, CancellationToken.None));

        Assert.Contains(expectedMessage, error.Message);
        var stored = await db.DesignScheduleTasks.AsNoTracking().SingleAsync(item => item.Id == task.Id);
        Assert.Equal(status, stored.Status);
    }

    [Fact]
    public async Task ReviewTaskAsync_ApprovalOpensTheNextStepAndCompletesThePhase()
    {
        var approverUserId = db.OperationalProjectMembers.Single(item => item.Id == approverMemberId).UserId;
        var nextPhase = Phase(projectId, designProjectId);
        nextPhase.Code = DesignSchedulePhaseCode.BasicDesign;
        db.Add(nextPhase);
        db.SaveChanges();
        var first = AddOwnTask("STEP-001", DesignScheduleStatus.Completed, DesignTaskApprovalStatus.Pending);
        var second = AddOwnTask("STEP-002");
        var basic = Task(projectId, designProjectId, nextPhase.Id, activeMemberId, approverMemberId, "STEP-101");
        db.AddRange(basic, new DesignScheduleTaskDependency
        {
            OperationalProjectId = projectId, TaskId = second.Id, PredecessorTaskId = first.Id,
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        await service.ReviewTaskAsync(projectId, first.Id,
            new ReviewDesignScheduleTaskRequest { Decision = "Approve", RowVersion = Version(first) },
            approverUserId, CancellationToken.None);

        Assert.Equal(DesignScheduleStatus.InProgress, db.DesignScheduleTasks.AsNoTracking().Single(item => item.Id == second.Id).Status);
        Assert.Equal(DesignScheduleStatus.NotStarted, db.DesignScheduleTasks.AsNoTracking().Single(item => item.Id == basic.Id).Status);
        notifications.Verify(item => item.NotifyManyFromTemplateAsync(
            It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { assigneeUserId })), "design-task.opened",
            It.IsAny<IDictionary<string, string>>(), "DesignScheduleTask", second.Id,
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        var started = await service.UpdateTaskProgressAsync(projectId, second.Id,
            new UpdateDesignScheduleTaskProgressRequest { ProgressPercent = 50, RowVersion = Version(second) },
            assigneeUserId, CancellationToken.None);
        await service.CompleteTaskAsync(projectId, second.Id, started!.RowVersion, assigneeUserId, CancellationToken.None);
        await service.ReviewTaskAsync(projectId, second.Id,
            new ReviewDesignScheduleTaskRequest { Decision = "Approve", RowVersion = Version(second) },
            approverUserId, CancellationToken.None);

        var phases = db.DesignSchedulePhases.AsNoTracking().Where(item => item.DesignProjectId == designProjectId)
            .ToDictionary(item => item.Code);
        Assert.Equal(DesignScheduleStatus.Completed, phases[DesignSchedulePhaseCode.Concept].Status);
        Assert.Equal(100, phases[DesignSchedulePhaseCode.Concept].ProgressPercent);
        Assert.Equal(DesignScheduleStatus.InProgress, phases[DesignSchedulePhaseCode.BasicDesign].Status);
        Assert.Equal(DesignScheduleStatus.InProgress, db.DesignScheduleTasks.AsNoTracking().Single(item => item.Id == basic.Id).Status);
    }

    [Fact]
    public async Task ReviewTaskAsync_RejectionDoesNotOpenTheNextStep()
    {
        var approverUserId = db.OperationalProjectMembers.Single(item => item.Id == approverMemberId).UserId;
        var first = AddOwnTask("STEP-201", DesignScheduleStatus.Completed, DesignTaskApprovalStatus.Pending);
        var second = AddOwnTask("STEP-202");
        db.Add(new DesignScheduleTaskDependency { OperationalProjectId = projectId, TaskId = second.Id, PredecessorTaskId = first.Id });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        await service.ReviewTaskAsync(projectId, first.Id,
            new ReviewDesignScheduleTaskRequest { Decision = "Reject", Comment = "Sửa trục A-3", RowVersion = Version(first) },
            approverUserId, CancellationToken.None);

        Assert.Equal(DesignScheduleStatus.NotStarted, db.DesignScheduleTasks.AsNoTracking().Single(item => item.Id == second.Id).Status);
        Assert.Equal(DesignScheduleStatus.NotStarted, db.DesignSchedulePhases.AsNoTracking().Single(item => item.Id == phaseId).Status);
    }

    public void Dispose() => db.Dispose();

    private DesignScheduleTask AddOwnTask(
        string code,
        DesignScheduleStatus status = DesignScheduleStatus.NotStarted,
        DesignTaskApprovalStatus approval = DesignTaskApprovalStatus.NotSubmitted)
    {
        var task = Task(projectId, designProjectId, phaseId, activeMemberId, approverMemberId, code);
        task.Status = status;
        task.ApprovalStatus = approval;
        if (status != DesignScheduleStatus.NotStarted)
        {
            task.ActualStart = new DateOnly(2026, 9, 1);
            task.ProgressPercent = status == DesignScheduleStatus.Completed ? 100 : 20;
        }
        if (status == DesignScheduleStatus.Completed) task.ActualEnd = new DateOnly(2026, 9, 2);
        db.Add(task);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return task;
    }

    private string Version(DesignScheduleTask task) => Convert.ToBase64String(
        db.DesignScheduleTasks.AsNoTracking().Single(item => item.Id == task.Id).RowVersion);

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
        int approverId,
        string code) => new()
        {
            OperationalProjectId = projectId,
            DesignProjectId = designProjectId,
            PhaseId = phaseId,
            Code = code,
            Name = code,
            DepartmentCode = "design",
            AssigneeMemberId = memberId,
            ApproverMemberId = approverId,
            PlannedStart = new DateOnly(2026, 9, 1),
            PlannedEnd = new DateOnly(2026, 9, 2),
            Status = DesignScheduleStatus.NotStarted,
            Weight = 100,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };

    private static UpsertDesignScheduleTaskRequest Request(int memberId, int approverId) => new()
    {
        Code = "DES-001",
        Name = "Design task",
        DepartmentCode = "design",
        AssigneeMemberId = memberId,
        ApproverMemberId = approverId,
        PlannedStart = new DateOnly(2026, 9, 1),
        PlannedEnd = new DateOnly(2026, 9, 2),
        Status = "NotStarted",
        ProgressPercent = 0,
        Weight = 100,
    };
}
