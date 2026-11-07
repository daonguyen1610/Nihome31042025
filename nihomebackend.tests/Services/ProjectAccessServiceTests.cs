using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class ProjectAccessServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IPermissionService> _permissions = new();

    public ProjectAccessServiceTests()
    {
        _permissions
            .Setup(service => service.HasAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task CanManageTeamAsync_ProjectManagerWithoutGlobalManage_IsDenied()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.ProjectManager,
            ProjectRoleScope.Project);
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageTeamAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageTeamAsync_DisciplineScopedDesignLeadWithGlobalManage_IsDenied()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Discipline,
            "architecture");
        Grant(userId, "operations.projects.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageTeamAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_ProjectScopedDesignLeadWithPermission_IsAllowed()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Project);
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanManageDesignScheduleAsync_DirectDesignLeadership_IsAllowed(bool isProjectManager)
    {
        var (userId, projectId, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        var designProject = _db.DesignProjects.Single(item => item.Id == designProjectId);
        if (isProjectManager) designProject.ProjectManagerUserId = userId;
        else designProject.DesignLeadUserId = userId;
        _db.SaveChanges();
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.True(allowed);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_DesignModuleLeadership_IsAllowed()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Module,
            "Design");
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.True(allowed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CanManageDesignScheduleAsync_ExpiredLeadership_IsDenied(bool expireMember)
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Project);
        var member = _db.OperationalProjectMembers.Single(item =>
            item.OperationalProjectId == projectId && item.UserId == userId);
        if (expireMember) member.EndedAt = DateTime.UtcNow.AddMinutes(-1);
        else member.Roles.Single().EndedAt = DateTime.UtcNow.AddMinutes(-1);
        _db.SaveChanges();
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_DeniesMissingPermissionAndDisciplineScope()
    {
        var (withoutPermissionUserId, withoutPermissionProjectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Project);
        var (disciplineUserId, disciplineProjectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Discipline,
            "architecture");
        Grant(disciplineUserId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var withoutPermission = await service.CanManageDesignScheduleAsync(
            withoutPermissionUserId, withoutPermissionProjectId);
        var disciplineScoped = await service.CanManageDesignScheduleAsync(
            disciplineUserId, disciplineProjectId);

        Assert.False(withoutPermission);
        Assert.False(disciplineScoped);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_PortfolioViewDoesNotReplaceManagePermission()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Project);
        Grant(userId, "operations.projects.view.all");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_CustomRoleCannotUseAdministrativeBypass()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        var customRole = new Role
        {
            Code = "SCHEDULE_AUDITOR",
            Name = "Schedule auditor",
            IsActive = true,
        };
        _db.Roles.Add(customRole);
        _db.SaveChanges();
        var user = _db.Users.Single(item => item.Id == userId);
        user.RoleEntityId = customRole.Id;
        _db.SaveChanges();
        Grant(userId, "operations.projects.view.all");
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_LegacyAdminBypassRequiresExistingProject()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        _db.Users.Single(item => item.Id == userId).Role = UserRole.ADMIN;
        _db.SaveChanges();
        Grant(userId, "operations.projects.view.all");
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var existing = await service.CanManageDesignScheduleAsync(userId, projectId);
        var missing = await service.CanManageDesignScheduleAsync(userId, int.MaxValue);

        Assert.True(existing);
        Assert.False(missing);
    }

    [Fact]
    public async Task CanManageDesignScheduleAsync_InactiveAdminCannotBypassAssignment()
    {
        var (userId, projectId, _) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        var user = _db.Users.Single(item => item.Id == userId);
        user.Role = UserRole.ADMIN;
        user.IsActive = false;
        _db.SaveChanges();
        Grant(userId, "operations.projects.view.all");
        Grant(userId, "design.schedule.manage");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var allowed = await service.CanManageDesignScheduleAsync(userId, projectId);

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanManageDesignProjectAsync_DisciplineScope_RequiresMatchingResourceDiscipline()
    {
        var (userId, _, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        var service = new ProjectAccessService(_db, _permissions.Object);

        var matching = await service.CanManageDesignProjectAsync(
            userId, designProjectId, disciplineCode: "architecture");
        var mismatching = await service.CanManageDesignProjectAsync(
            userId, designProjectId, disciplineCode: "structure");
        var projectWide = await service.CanManageDesignProjectAsync(userId, designProjectId);

        Assert.True(matching);
        Assert.False(mismatching);
        Assert.False(projectWide);
    }

    [Fact]
    public async Task GetDesignProjectPermissionsAsync_ExplicitMatrixOverridesRoleDefaults()
    {
        var (userId, _, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Module,
            "Design");
        var member = _db.OperationalProjectMembers.Single(item =>
            item.UserId == userId && item.DesignProjectId == designProjectId);
        member.DesignPermission = new DesignProjectMemberPermission
        {
            CanView = true,
            CanCreate = false,
            CanUpdate = true,
            CanDelete = true,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
        };
        _db.SaveChanges();
        var service = new ProjectAccessService(_db, _permissions.Object);

        var permissions = await service.GetDesignProjectPermissionsAsync(userId, designProjectId);

        Assert.True(permissions.CanView);
        Assert.False(permissions.CanCreate);
        Assert.True(permissions.CanUpdate);
        Assert.True(permissions.CanDelete);
        Assert.True(await service.HasDesignProjectPermissionAsync(
            userId, designProjectId, DesignProjectAction.Update));
        Assert.False(await service.HasDesignProjectPermissionAsync(
            userId, designProjectId, DesignProjectAction.Create));
    }

    [Fact]
    public async Task CanManageDesignProjectTeamAsync_DesignLeadMember_IsAllowed()
    {
        var (userId, _, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.DesignLead,
            ProjectRoleScope.Module,
            "Design");
        var service = new ProjectAccessService(_db, _permissions.Object);

        Assert.True(await service.CanManageDesignProjectTeamAsync(userId, designProjectId));
    }

    [Theory]
    [InlineData("member")]
    [InlineData("manager")]
    [InlineData("creator")]
    public async Task RevokedDossierView_OverridesRootProjectAccess(string rootAccess)
    {
        var (userId, projectId, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect, ProjectRoleScope.Module, "Design");
        var member = _db.OperationalProjectMembers.Single(item => item.UserId == userId);
        member.DesignPermission = new DesignProjectMemberPermission
        {
            CanView = false,
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
        };
        var root = _db.OperationalProjects.Single(item => item.Id == projectId);
        if (rootAccess == "manager") root.ProjectManagerUserId = userId;
        else if (rootAccess == "creator") root.CreatedByUserId = userId;
        else _db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = projectId,
            UserId = userId,
            Position = "Project observer",
            CreatedByUserId = userId,
            UpdatedByUserId = userId,
            Roles = [new OperationalProjectMemberRole
            {
                RoleCode = ProjectTeamRoleCode.Observer,
                Scope = ProjectRoleScope.Project,
            }],
        });
        await _db.SaveChangesAsync();
        var service = new ProjectAccessService(_db, _permissions.Object);

        Assert.False((await service.GetDesignProjectPermissionsAsync(userId, designProjectId)).CanView);
        Assert.False(await service.CanViewDesignProjectAsync(userId, designProjectId));
        Assert.DoesNotContain(designProjectId, await service.GetAccessibleDesignProjectIdsAsync(userId));
        Assert.True(await service.CanViewOperationalProjectAsync(userId, projectId));
    }

    [Fact]
    public async Task CanManageDesignProjectTeamAsync_ContributorWithUpdate_IsDenied()
    {
        var (userId, _, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Module,
            "Design");
        var service = new ProjectAccessService(_db, _permissions.Object);

        Assert.True(await service.HasDesignProjectPermissionAsync(
            userId, designProjectId, DesignProjectAction.Update));
        Assert.False(await service.CanManageDesignProjectTeamAsync(userId, designProjectId));
    }

    [Fact]
    public async Task CanViewDesignResourceAsync_DisciplineScope_HidesOtherDisciplines()
    {
        var (userId, _, designProjectId) = AddProjectMember(
            ProjectTeamRoleCode.Architect,
            ProjectRoleScope.Discipline,
            "architecture");
        var architecture = new BasicDesignDoc
        {
            DesignProjectId = designProjectId,
            DisciplineCode = "architecture",
            DocumentCode = "KT-BD-001",
            Title = "Architecture",
        };
        var structure = new BasicDesignDoc
        {
            DesignProjectId = designProjectId,
            DisciplineCode = "structure",
            DocumentCode = "KC-BD-001",
            Title = "Structure",
        };
        _db.AddRange(architecture, structure);
        await _db.SaveChangesAsync();
        var service = new ProjectAccessService(_db, _permissions.Object);

        var matching = await service.CanViewDesignResourceAsync(
            userId, DesignProjectResourceType.BasicDesignDoc, architecture.Id);
        var mismatching = await service.CanViewDesignResourceAsync(
            userId, DesignProjectResourceType.BasicDesignDoc, structure.Id);

        Assert.True(matching);
        Assert.False(mismatching);
    }

    public void Dispose() => _db.Dispose();

    private (int UserId, int ProjectId, int DesignProjectId) AddProjectMember(
        ProjectTeamRoleCode roleCode,
        ProjectRoleScope scope,
        string? scopeValue = null)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = $"09{Guid.NewGuid():N}"[..10],
            FullName = "Scoped member",
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "x",
            Role = UserRole.USER,
            IsActive = true,
        };
        var customer = new Customer
        {
            Name = "Access customer",
            Type = CustomerType.Individual,
            SourceCode = "referral",
        };
        _db.AddRange(user, customer);
        _db.SaveChanges();

        var project = new OperationalProject
        {
            Code = $"OP-{Guid.NewGuid():N}",
            Name = "Access project",
            CustomerId = customer.Id,
            UpdatedByUserId = user.Id,
        };
        _db.OperationalProjects.Add(project);
        _db.SaveChanges();

        var member = new OperationalProjectMember
        {
            OperationalProjectId = project.Id,
            UserId = user.Id,
            Position = "Project member",
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id,
            Roles =
            [
                new OperationalProjectMemberRole
                {
                    RoleCode = roleCode,
                    Scope = scope,
                    ScopeValue = scopeValue,
                    StartedAt = DateTime.UtcNow,
                },
            ],
        };
        var designProject = new DesignProject
        {
            OperationalProjectId = project.Id,
            ProjectCode = $"DP-{Guid.NewGuid():N}",
            Name = "Scoped design",
            CustomerId = customer.Id,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id,
        };
        _db.AddRange(member, designProject);
        _db.SaveChanges();
        if (scope != ProjectRoleScope.Project)
        {
            member.DesignProjectId = designProject.Id;
            _db.SaveChanges();
        }
        return (user.Id, project.Id, designProject.Id);
    }

    private void Grant(int userId, string permissionCode)
    {
        _permissions
            .Setup(service => service.HasAsync(userId, permissionCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }
}
