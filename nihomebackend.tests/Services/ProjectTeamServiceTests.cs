using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectTeamServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IProjectAccessService> _access = new();
    private readonly ProjectTeamService _service;
    private readonly int _projectId;
    private readonly int _otherProjectId;
    private readonly int _callerId;
    private readonly int _managerUserId;
    private readonly int _memberUserId;
    private readonly int _otherUserId;
    private readonly int _inactiveUserId;

    public ProjectTeamServiceTests()
    {
        var caller = AddUser("0901000001", "Caller");
        var manager = AddUser("0901000002", "Manager");
        var member = AddUser("0901000003", "Member");
        var other = AddUser("0901000004", "Other");
        var inactive = AddUser("0901000005", "Inactive", false);
        var customer = new Customer
        {
            Name = "Project team customer",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        _db.Customers.Add(customer);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption
            {
                Category = "design_discipline",
                Code = "architecture",
                Name = "Architecture",
                IsActive = true,
            },
            new MasterDataOption
            {
                Category = "design_discipline",
                Code = "structure",
                Name = "Structure",
                IsActive = true,
            });
        _db.SaveChanges();
        var project = AddProject(customer.Id, "PJ-TEAM-1");
        var otherProject = AddProject(customer.Id, "PJ-TEAM-2");

        _callerId = caller.Id;
        _managerUserId = manager.Id;
        _memberUserId = member.Id;
        _otherUserId = other.Id;
        _inactiveUserId = inactive.Id;
        _projectId = project.Id;
        _otherProjectId = otherProject.Id;
        _access.Setup(value => value.CanManageTeamAsync(_callerId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _access.Setup(value => value.CanViewOperationalProjectAsync(_callerId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _service = new ProjectTeamService(_db, _access.Object);
    }

    [Fact]
    public async Task AddMemberAsync_MultiRoleAndReporting_ReturnsCompleteTeam()
    {
        var manager = await AddMemberAsync(_managerUserId, "Project Manager", Role("ProjectManager"));
        var member = await AddMemberAsync(
            _memberUserId,
            "Lead Architect",
            Role("Architect", "Discipline", "architecture"),
            Role("DesignLead", "Module", "Design"),
            reportsToMemberId: manager.Id);

        var team = await _service.GetAsync(_projectId, _callerId);

        Assert.NotNull(team);
        Assert.True(team.CanManage);
        Assert.Equal(manager.Id, member.ReportsToMemberId);
        Assert.Equal("Manager", member.ReportsToName);
        Assert.Equal(2, member.Roles.Count);
        Assert.Equal(2, team.Members.Count);
        Assert.Contains(team.RoleDefinitions, role => role.Code == "DesignLead" && role.CanManageTeam);
        Assert.Contains("Design", team.ModuleOptions);
        Assert.Contains("architecture", team.DisciplineOptions);
    }

    [Fact]
    public async Task AddMemberAsync_InvalidScopeValue_IsRejected()
    {
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            AddMemberAsync(
                _memberUserId,
                "Architect",
                Role("Architect", "Discipline", "not-a-discipline")));
    }

    [Fact]
    public async Task AddAssignmentAsync_CanonicalizesValuesAndRejectsUnknownCodes()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var canonical = Assignment("DES-CANONICAL", member.Id);
        canonical.Module = "design";
        canonical.Discipline = "Architecture";
        var unknownModule = Assignment("DES-BAD-MODULE", member.Id);
        unknownModule.Module = "Unknown";
        var unknownDiscipline = Assignment("DES-BAD-DISCIPLINE", member.Id);
        unknownDiscipline.Discipline = "Unknown";

        var created = await _service.AddAssignmentAsync(_projectId, canonical, _callerId);

        Assert.Equal("Design", created.Module);
        Assert.Equal("architecture", created.Discipline);
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, unknownModule, _callerId));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, unknownDiscipline, _callerId));
    }

    [Fact]
    public async Task AddAssignmentAsync_ParallelAssignmentsKeepDistinctKpiIdentity()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));

        var first = await _service.AddAssignmentAsync(
            _projectId,
            Assignment("DES-ARCH-01", member.Id, parallelGroup: "DESIGN-SPRINT-1"),
            _callerId);
        var second = await _service.AddAssignmentAsync(
            _projectId,
            Assignment("DES-ARCH-02", member.Id, parallelGroup: "DESIGN-SPRINT-1"),
            _callerId);

        Assert.Equal($"{_projectId}:DES-ARCH-01:{member.Id}", first.KpiIdentity);
        Assert.Equal($"{_projectId}:DES-ARCH-02:{member.Id}", second.KpiIdentity);
        Assert.NotEqual(first.KpiIdentity, second.KpiIdentity);
        Assert.Equal(2, (await _service.GetAsync(_projectId, _callerId))!.Assignments.Count);
    }

    [Fact]
    public async Task UpdateMemberAsync_AppendsImmutableHistorySnapshots()
    {
        var created = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var firstHistory = Assert.Single((await _service.GetHistoryAsync(_projectId, _callerId))!);
        var originalSnapshot = firstHistory.SnapshotJson;
        var request = Member(_memberUserId, "Senior Architect", Role("DesignLead"));
        request.RowVersion = created.RowVersion;

        await _service.UpdateMemberAsync(_projectId, created.Id, request, _callerId);

        var history = (await _service.GetHistoryAsync(_projectId, _callerId))!;
        Assert.Equal(2, history.Count);
        Assert.Contains(history, item => item.Action == "Created" && item.SnapshotJson == originalSnapshot);
        Assert.Contains(history, item => item.Action == "Updated" && item.SnapshotJson.Contains("Senior Architect"));
    }

    [Fact]
    public async Task AddMemberAsync_DuplicateActiveMembership_IsRejected()
    {
        await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            AddMemberAsync(_memberUserId, "Architect again", Role("Architect")));
    }

    [Fact]
    public async Task AddAssignmentAsync_DuplicateWorkAndAssignee_IsRejected()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        await _service.AddAssignmentAsync(_projectId, Assignment("DES-01", member.Id), _callerId);

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, Assignment("DES-01", member.Id), _callerId));
    }

    [Theory]
    [InlineData("Project", "Architecture")]
    [InlineData("Module", null)]
    [InlineData("Discipline", null)]
    [InlineData("Invalid", null)]
    public async Task AddMemberAsync_InvalidRoleScope_IsRejected(string scope, string? scopeValue)
    {
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            AddMemberAsync(_memberUserId, "Architect", Role("Architect", scope, scopeValue)));
    }

    [Fact]
    public async Task AddMemberAsync_InvalidDateOrInactiveUser_IsRejected()
    {
        var invalidDate = Member(_memberUserId, "Architect", Role("Architect"));
        invalidDate.EndedAt = invalidDate.StartedAt.AddDays(-1);

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddMemberAsync(_projectId, invalidDate, _callerId));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            AddMemberAsync(_inactiveUserId, "Inactive", Role("Observer")));
    }

    [Fact]
    public async Task UpdateMemberAsync_SelfManager_IsRejected()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var request = Member(_memberUserId, "Architect", Role("Architect"));
        request.ReportsToMemberId = member.Id;
        request.RowVersion = member.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateMemberAsync(_projectId, member.Id, request, _callerId));
    }

    [Fact]
    public async Task UpdateMemberAsync_CrossProjectManager_IsRejected()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var manager = await AddMemberAsync(
            _managerUserId,
            "Other PM",
            Role("ProjectManager"),
            projectId: _otherProjectId);
        var request = Member(_memberUserId, "Architect", Role("Architect"));
        request.ReportsToMemberId = manager.Id;
        request.RowVersion = member.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateMemberAsync(_projectId, member.Id, request, _callerId));
    }

    [Fact]
    public async Task UpdateMemberAsync_ReportingCycle_IsRejected()
    {
        var first = await AddMemberAsync(_managerUserId, "Manager", Role("ProjectManager"));
        var second = await AddMemberAsync(
            _memberUserId,
            "Architect",
            Role("Architect"),
            reportsToMemberId: first.Id);
        var request = Member(_managerUserId, "Manager", Role("ProjectManager"));
        request.ReportsToMemberId = second.Id;
        request.RowVersion = first.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateMemberAsync(_projectId, first.Id, request, _callerId));
    }

    [Fact]
    public async Task UpdateMemberAsync_ChangingUserIdentity_IsRejected()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var request = Member(_otherUserId, "Architect", Role("Architect"));
        request.RowVersion = member.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateMemberAsync(_projectId, member.Id, request, _callerId));
    }

    [Fact]
    public async Task UpdateMemberAsync_EndingMemberWithActiveDependencies_IsRejected()
    {
        var manager = await AddMemberAsync(_managerUserId, "Manager", Role("ProjectManager"));
        var member = await AddMemberAsync(
            _memberUserId,
            "Architect",
            Role("Architect"),
            reportsToMemberId: manager.Id);
        await _service.AddAssignmentAsync(
            _projectId,
            Assignment("DES-ACTIVE", member.Id, manager.Id),
            _callerId);
        var request = Member(_managerUserId, "Manager", Role("ProjectManager"));
        request.EndedAt = DateTime.UtcNow;
        request.RowVersion = manager.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateMemberAsync(_projectId, manager.Id, request, _callerId));
    }

    [Fact]
    public async Task AddAssignmentAsync_EndedOrCrossProjectMemberAndInvalidDates_AreRejected()
    {
        var ended = await AddMemberAsync(
            _memberUserId,
            "Former Architect",
            Role("Architect"),
            endedAt: DateTime.UtcNow);
        var crossProject = await AddMemberAsync(
            _otherUserId,
            "Other Architect",
            Role("Architect"),
            projectId: _otherProjectId);
        var invalidDates = Assignment("DES-DATE", ended.Id);
        invalidDates.PlannedStart = DateTime.UtcNow;
        invalidDates.PlannedEnd = invalidDates.PlannedStart.Value.AddDays(-1);

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, Assignment("DES-ENDED", ended.Id), _callerId));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, Assignment("DES-CROSS", crossProject.Id), _callerId));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(_projectId, invalidDates, _callerId));
    }

    [Fact]
    public async Task AddAssignmentAsync_AssigneeCannotManageOwnWork()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.AddAssignmentAsync(
                _projectId,
                Assignment("DES-SELF-MANAGED", member.Id, member.Id),
                _callerId));
    }

    [Fact]
    public async Task UpdateAssignmentAsync_TerminalAssignment_IsImmutable()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var request = Assignment("DES-DONE", member.Id);
        request.Status = "Completed";
        var assignment = await _service.AddAssignmentAsync(_projectId, request, _callerId);
        var update = Assignment("DES-DONE", member.Id);
        update.RowVersion = assignment.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateAssignmentAsync(_projectId, assignment.Id, update, _callerId));
    }

    [Fact]
    public async Task UpdateAssignmentAsync_KpiIdentity_IsImmutable()
    {
        var firstMember = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var secondMember = await AddMemberAsync(_otherUserId, "Engineer", Role("StructuralEngineer"));
        var assignment = await _service.AddAssignmentAsync(
            _projectId, Assignment("DES-STABLE", firstMember.Id), _callerId);
        var changedWorkKey = Assignment("DES-CHANGED", firstMember.Id);
        changedWorkKey.RowVersion = assignment.RowVersion;
        var changedAssignee = Assignment("DES-STABLE", secondMember.Id);
        changedAssignee.RowVersion = assignment.RowVersion;

        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateAssignmentAsync(_projectId, assignment.Id, changedWorkKey, _callerId));
        await Assert.ThrowsAsync<ProjectTeamOperationException>(() =>
            _service.UpdateAssignmentAsync(_projectId, assignment.Id, changedAssignee, _callerId));

        var persisted = await _db.OperationalProjectAssignments.FindAsync(assignment.Id);
        Assert.Equal("DES-STABLE", persisted!.WorkKey);
        Assert.Equal(firstMember.Id, persisted.AssigneeMemberId);
    }

    [Fact]
    public async Task UpdateMemberAsync_InvalidRowVersionToken_IsRejected()
    {
        var member = await AddMemberAsync(_memberUserId, "Architect", Role("Architect"));
        var request = Member(_memberUserId, "Architect", Role("Architect"));
        request.RowVersion = "not-base64";

        await Assert.ThrowsAsync<CrmConcurrencyTokenException>(() =>
            _service.UpdateMemberAsync(_projectId, member.Id, request, _callerId));
    }

    private async Task<NihomeBackend.Models.DTOs.Responses.OperationalProjectMemberResponse> AddMemberAsync(
        int userId,
        string position,
        ProjectMemberRoleRequest role,
        ProjectMemberRoleRequest? secondRole = null,
        int? reportsToMemberId = null,
        DateTime? endedAt = null,
        int? projectId = null)
    {
        var roles = secondRole is null ? new List<ProjectMemberRoleRequest> { role } : [role, secondRole];
        var request = Member(userId, position, roles.ToArray());
        request.ReportsToMemberId = reportsToMemberId;
        request.EndedAt = endedAt;
        return await _service.AddMemberAsync(projectId ?? _projectId, request, _callerId);
    }

    private static UpsertOperationalProjectMemberRequest Member(
        int userId,
        string position,
        params ProjectMemberRoleRequest[] roles) => new()
    {
        UserId = userId,
        Position = position,
        StartedAt = DateTime.UtcNow.AddDays(-1),
        Roles = roles.ToList(),
    };

    private static ProjectMemberRoleRequest Role(
        string roleCode,
        string scope = "Project",
        string? scopeValue = null) => new()
    {
        RoleCode = roleCode,
        Scope = scope,
        ScopeValue = scopeValue,
    };

    private static UpsertOperationalProjectAssignmentRequest Assignment(
        string workKey,
        int assigneeMemberId,
        int? managerMemberId = null,
        string? parallelGroup = null) => new()
    {
        WorkKey = workKey,
        Title = $"Assignment {workKey}",
        Module = "Design",
        Discipline = "Architecture",
        ParallelGroup = parallelGroup,
        AssigneeMemberId = assigneeMemberId,
        ManagerMemberId = managerMemberId,
        Status = "Planned",
    };

    private ApplicationUser AddUser(string phone, string name, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = $"{phone}@nihome.test",
            PasswordHash = "test",
            IsActive = isActive,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private OperationalProject AddProject(int customerId, string code)
    {
        var project = new OperationalProject
        {
            Code = code,
            Name = code,
            CustomerId = customerId,
            CreatedByUserId = _callerId == 0 ? null : _callerId,
        };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    public void Dispose() => _db.Dispose();
}
