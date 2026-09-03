using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class LegacyProjectTeamSyncServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly LegacyProjectTeamSyncService _sut;
    private readonly int _projectId;
    private readonly int _actorUserId;

    public LegacyProjectTeamSyncServiceTests()
    {
        _sut = new LegacyProjectTeamSyncService(_db);
        var actor = AddUser("0900200001", "Actor");
        var customer = new Customer
        {
            Name = "Dual-write customer",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        var project = new OperationalProject
        {
            Code = "OP-DUAL-WRITE",
            Name = "Dual-write project",
            CustomerId = customer.Id,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
        };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();
        _actorUserId = actor.Id;
        _projectId = project.Id;
    }

    [Fact]
    public async Task SyncOperationalProjectManagerAsync_ManagerChanges_ReplacesOnlyDerivedAssignment()
    {
        var firstManager = AddUser("0900200002", "First manager");
        var secondManager = AddUser("0900200003", "Second manager");

        await _sut.SyncOperationalProjectManagerAsync(_projectId, firstManager.Id, _actorUserId);
        await _db.SaveChangesAsync();
        await _sut.SyncOperationalProjectManagerAsync(_projectId, secondManager.Id, _actorUserId);
        await _db.SaveChangesAsync();

        var members = await _db.OperationalProjectMembers.Include(member => member.Roles).ToListAsync();
        var oldMember = Assert.Single(members, member => member.UserId == firstManager.Id);
        var newMember = Assert.Single(members, member => member.UserId == secondManager.Id);
        Assert.NotNull(oldMember.EndedAt);
        Assert.All(oldMember.Roles, role => Assert.NotNull(role.EndedAt));
        Assert.Null(newMember.EndedAt);
        var role = Assert.Single(newMember.Roles, item => item.EndedAt == null);
        Assert.Equal(ProjectTeamRoleCode.ProjectManager, role.RoleCode);
        Assert.Equal(ProjectRoleScope.Project, role.Scope);
        Assert.Equal(LegacyProjectTeamSyncService.RuntimeSource, role.Source);
        Assert.Equal(LegacyProjectTeamSyncService.OperationalProjectManagerReference, role.SourceReference);
    }

    [Fact]
    public async Task SyncOperationalProjectManagerAsync_ManualIdenticalRole_IsNeverRevoked()
    {
        var manualManager = AddUser("0900200004", "Manual manager");
        var replacement = AddUser("0900200005", "Legacy replacement");
        var manualMember = AddManualMember(manualManager.Id, ProjectTeamRoleCode.ProjectManager, ProjectRoleScope.Project);
        await _db.SaveChangesAsync();

        await _sut.SyncOperationalProjectManagerAsync(_projectId, manualManager.Id, _actorUserId);
        await _db.SaveChangesAsync();
        await _sut.SyncOperationalProjectManagerAsync(_projectId, replacement.Id, _actorUserId);
        await _db.SaveChangesAsync();

        await _db.Entry(manualMember).Collection(member => member.Roles).LoadAsync();
        Assert.Null(manualMember.EndedAt);
        var manualRole = Assert.Single(manualMember.Roles);
        Assert.Null(manualRole.EndedAt);
        Assert.Equal(LegacyProjectTeamSyncService.ManualSource, manualRole.Source);
        Assert.Contains(await _db.OperationalProjectMembers.Include(member => member.Roles).ToListAsync(),
            member => member.UserId == replacement.Id && member.Roles.Any(role => role.EndedAt == null));
    }

    [Fact]
    public async Task SyncDesignProjectRolesAsync_SameUser_GetsOneMembershipWithBothDesignRoles()
    {
        var designOwner = AddUser("0900200006", "Design owner");

        await _sut.SyncDesignProjectRolesAsync(
            _projectId, designOwner.Id, designOwner.Id, _actorUserId);
        await _db.SaveChangesAsync();

        var member = await _db.OperationalProjectMembers.Include(item => item.Roles).SingleAsync();
        Assert.Equal(designOwner.Id, member.UserId);
        Assert.Collection(
            member.Roles.OrderBy(role => role.RoleCode),
            role =>
            {
                Assert.Equal(ProjectTeamRoleCode.ProjectManager, role.RoleCode);
                Assert.Equal(ProjectRoleScope.Module, role.Scope);
                Assert.Equal("Design", role.ScopeValue);
            },
            role =>
            {
                Assert.Equal(ProjectTeamRoleCode.DesignLead, role.RoleCode);
                Assert.Equal(ProjectRoleScope.Module, role.Scope);
                Assert.Equal("Design", role.ScopeValue);
            });
    }

    [Fact]
    public async Task SyncDesignProjectRolesAsync_RemovingDerivedRole_PreservesOtherSource()
    {
        var user = AddUser("0900200007", "Multi-source member");
        var member = AddManualMember(user.Id, ProjectTeamRoleCode.Observer, ProjectRoleScope.Project);
        await _db.SaveChangesAsync();
        await _sut.SyncDesignProjectRolesAsync(_projectId, user.Id, null, _actorUserId);
        await _db.SaveChangesAsync();

        await _sut.SyncDesignProjectRolesAsync(_projectId, null, null, _actorUserId);
        await _db.SaveChangesAsync();

        await _db.Entry(member).Collection(item => item.Roles).LoadAsync();
        Assert.Null(member.EndedAt);
        Assert.Contains(member.Roles, role =>
            role.RoleCode == ProjectTeamRoleCode.Observer && role.EndedAt == null);
        Assert.Contains(member.Roles, role =>
            role.RoleCode == ProjectTeamRoleCode.ProjectManager && role.EndedAt != null);
    }

    [Fact]
    public async Task SyncOperationalProjectManagerAsync_InactiveOrMissingUser_DoesNotCreateActiveMember()
    {
        var inactiveUser = AddUser("0900200008", "Inactive user", false);

        await _sut.SyncOperationalProjectManagerAsync(_projectId, inactiveUser.Id, _actorUserId);
        await _sut.SyncOperationalProjectManagerAsync(_projectId, int.MaxValue, _actorUserId);
        await _db.SaveChangesAsync();

        Assert.Empty(await _db.OperationalProjectMembers.ToListAsync());
    }

    [Fact]
    public async Task SyncOperationalProjectManagerAsync_UserBecomesInactive_EndsDerivedMembership()
    {
        var manager = AddUser("0900200009", "Manager becoming inactive");
        await _sut.SyncOperationalProjectManagerAsync(_projectId, manager.Id, _actorUserId);
        await _db.SaveChangesAsync();
        manager.IsActive = false;
        await _db.SaveChangesAsync();

        await _sut.SyncOperationalProjectManagerAsync(_projectId, manager.Id, _actorUserId);
        await _db.SaveChangesAsync();

        var member = await _db.OperationalProjectMembers.Include(item => item.Roles).SingleAsync();
        Assert.NotNull(member.EndedAt);
        Assert.All(member.Roles, role => Assert.NotNull(role.EndedAt));
    }

    [Fact]
    public async Task SyncOperationalProjectManagerAsync_ChangedThenRetried_WritesOneHistoryEntry()
    {
        var manager = AddUser("0900200010", "Audited manager");

        await _sut.SyncOperationalProjectManagerAsync(_projectId, manager.Id, _actorUserId);
        await _db.SaveChangesAsync();
        await _sut.SyncOperationalProjectManagerAsync(_projectId, manager.Id, _actorUserId);
        await _db.SaveChangesAsync();

        var history = Assert.Single(await _db.OperationalProjectTeamHistory.ToListAsync());
        Assert.Equal("LegacyTeamSync", history.EntityType);
        Assert.Equal("Synchronized", history.Action);
        Assert.Equal(_projectId, history.EntityId);
        Assert.Contains(LegacyProjectTeamSyncService.OperationalProjectManagerReference, history.SnapshotJson);
    }

    public void Dispose() => _db.Dispose();

    private ApplicationUser AddUser(string phone, string name, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = name,
            Email = $"{phone}@example.com",
            PasswordHash = "x",
            Role = UserRole.USER,
            IsActive = isActive,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private OperationalProjectMember AddManualMember(
        int userId,
        ProjectTeamRoleCode roleCode,
        ProjectRoleScope scope)
    {
        var member = new OperationalProjectMember
        {
            OperationalProjectId = _projectId,
            UserId = userId,
            Position = "Manual member",
            StartedAt = DateTime.UtcNow,
            Source = LegacyProjectTeamSyncService.ManualSource,
            CreatedByUserId = _actorUserId,
            UpdatedByUserId = _actorUserId,
        };
        member.Roles.Add(new OperationalProjectMemberRole
        {
            RoleCode = roleCode,
            Scope = scope,
            Source = LegacyProjectTeamSyncService.ManualSource,
            StartedAt = DateTime.UtcNow,
        });
        _db.OperationalProjectMembers.Add(member);
        return member;
    }
}