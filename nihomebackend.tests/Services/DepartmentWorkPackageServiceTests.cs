using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DepartmentWorkPackageServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IProjectAccessService> _access = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly DepartmentWorkPackageService _service;
    private readonly RoleGroup _designGroup;
    private readonly RoleGroup _crmGroup;
    private readonly ApplicationUser _pm;
    private readonly ApplicationUser _designLead;
    private readonly ApplicationUser _architect;
    private readonly ApplicationUser _sales;
    private readonly OperationalProject _project;

    public DepartmentWorkPackageServiceTests()
    {
        _designGroup = AddGroup("DESIGN");
        _crmGroup = AddGroup("CRM");
        var constructionGroup = AddGroup("CONSTRUCTION");
        _pm = AddUser("PM", constructionGroup);
        _designLead = AddUser("DESIGN_LEAD", _designGroup);
        _architect = AddUser("ARCHITECT", _designGroup);
        _sales = AddUser("SALE", _crmGroup);
        var customer = new Customer { Name = "NICON Industrial Client", Type = CustomerType.Company, SourceCode = "referral" };
        _project = new OperationalProject
        {
            Code = "DB-2026-009",
            Name = "Nhà máy sản xuất linh kiện",
            Customer = customer,
            ProjectManagerUserId = _pm.Id,
        };
        _db.OperationalProjects.Add(_project);
        _db.SaveChanges();

        _access.Setup(item => item.GetAccessibleOperationalProjectIdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { _project.Id });
        _access.Setup(item => item.CanViewOperationalProjectAsync(It.IsAny<int>(), _project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _notifications.Setup(item => item.NotifyFromTemplateAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((NihomeBackend.Models.DTOs.Responses.NotificationResponse?)null);
        _service = new DepartmentWorkPackageService(
            _db, _access.Object, _notifications.Object, NullLogger<DepartmentWorkPackageService>.Instance);
    }

    [Fact]
    public async Task ProjectManager_CreatesPackageForReceivingDepartment()
    {
        var result = await _service.CreateAsync(ProjectRequest(), _pm.Id);

        Assert.Equal("Project", result.Kind);
        Assert.Equal(_project.Id, result.OperationalProjectId);
        Assert.Equal(_designGroup.Id, result.DepartmentRoleGroupId);
        Assert.Equal(_designLead.Id, result.LeadUserId);
        Assert.Equal("Planned", result.Status);
    }

    [Fact]
    public async Task UnrelatedUser_CannotCreateProjectPackage()
    {
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(ProjectRequest(), _architect.Id));

        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
    }

    [Fact]
    public async Task ProjectPackage_RejectsLeadWithoutAssignmentPermission()
    {
        var request = ProjectRequest();
        request.LeadUserId = _architect.Id;

        var error = await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _pm.Id));

        Assert.Contains("quyền phân công", error.Message);
        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
    }

    [Fact]
    public async Task InternalPackage_CannotReferenceProjectOrUseAnotherLead()
    {
        var request = ProjectRequest();
        request.Kind = "Internal";

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _designLead.Id));

        request.OperationalProjectId = null;
        request.LeadUserId = _architect.Id;
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _designLead.Id));
    }

    [Fact]
    public async Task OnlyLead_AssignsTaskToActiveMemberOfSameDepartment()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var valid = TaskRequest(_architect.Id, _designLead.Id);

        await Assert.ThrowsAsync<DepartmentWorkPackageScopeException>(() =>
            _service.AddTaskAsync(package.Id, valid, _pm.Id));

        var task = await _service.AddTaskAsync(package.Id, valid, _designLead.Id);
        Assert.Equal(_architect.Id, task.AssigneeUserId);

        var wrongDepartment = TaskRequest(_sales.Id, _designLead.Id, "DES-02");
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.AddTaskAsync(package.Id, wrongDepartment, _designLead.Id));
    }

    [Fact]
    public async Task KpiTask_RequiresMatchingActiveDefinitionAndIndependentVerifier()
    {
        var definition = new KpiDefinition
        {
            Code = "DESIGN_VERIFIED_WORK",
            RoleCode = "DESIGN",
            NameKey = "kpi.definition.DESIGN_VERIFIED_WORK",
            SourceModule = "WORK",
            MetricCode = "VerifiedWorkTaskCount",
            Weight = .1m,
            TargetValue = 1m,
            IsActive = true,
            CreatedByUserId = _pm.Id,
            UpdatedByUserId = _pm.Id,
        };
        _db.KpiDefinitions.Add(definition);
        await _db.SaveChangesAsync();
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var request = TaskRequest(_architect.Id, _designLead.Id);
        request.KpiDefinitionId = definition.Id;

        var task = await _service.AddTaskAsync(package.Id, request, _designLead.Id);

        Assert.Equal(definition.Id, task.KpiDefinitionId);
        Assert.False(task.CanVerify);
    }

    [Fact]
    public async Task Task_CannotStartUntilPackageAndPredecessorAreReady()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var predecessor = await _service.AddTaskAsync(
            package.Id, TaskRequest(_architect.Id, _designLead.Id, "DES-01"), _designLead.Id);
        var dependentRequest = TaskRequest(_architect.Id, _designLead.Id, "DES-02");
        dependentRequest.PredecessorTaskIds = [predecessor.Id];
        var dependent = await _service.AddTaskAsync(package.Id, dependentRequest, _designLead.Id);
        var predecessorEntity = await _db.DepartmentWorkTasks.FindAsync(predecessor.Id);
        var dependentEntity = await _db.DepartmentWorkTasks.FindAsync(dependent.Id);
        var predecessorVersion = BitConverter.GetBytes(1L);
        var dependentVersion = BitConverter.GetBytes(2L);
        predecessorEntity!.RowVersion = predecessorVersion;
        dependentEntity!.RowVersion = dependentVersion;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateProgressAsync(package.Id, predecessor.Id, new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "InProgress",
                ProgressPercent = 10,
                RowVersion = Convert.ToBase64String(predecessorVersion),
            }, _architect.Id));

        (await _db.DepartmentWorkPackages.FindAsync(package.Id))!.Status = WorkPackageStatus.Active;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateProgressAsync(package.Id, dependent.Id, new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "InProgress",
                ProgressPercent = 10,
                RowVersion = Convert.ToBase64String(dependentVersion),
            }, _architect.Id));

        await _service.UpdateProgressAsync(package.Id, predecessor.Id, new UpdateDepartmentWorkTaskProgressRequest
        {
            Status = "Completed",
            ProgressPercent = 100,
            RowVersion = Convert.ToBase64String(predecessorVersion),
        }, _architect.Id);
        var started = await _service.UpdateProgressAsync(package.Id, dependent.Id, new UpdateDepartmentWorkTaskProgressRequest
        {
            Status = "InProgress",
            ProgressPercent = 10,
            RowVersion = Convert.ToBase64String(dependentVersion),
        }, _architect.Id);

        Assert.Equal("InProgress", started!.Status);
    }

    private CreateDepartmentWorkPackageRequest ProjectRequest() => new()
    {
        Kind = "Project",
        Name = "Triển khai hồ sơ thiết kế kỹ thuật",
        OperationalProjectId = _project.Id,
        DepartmentRoleGroupId = _designGroup.Id,
        LeadUserId = _designLead.Id,
        PlannedStart = DateTime.UtcNow.Date,
        PlannedEnd = DateTime.UtcNow.Date.AddDays(14),
    };

    private static UpsertDepartmentWorkTaskRequest TaskRequest(int assigneeId, int approverId, string wbs = "DES-01") => new()
    {
        WbsCode = wbs,
        Title = "Hoàn thiện mặt bằng kiến trúc",
        AssigneeUserId = assigneeId,
        ApproverUserId = approverId,
        PlannedStart = DateTime.UtcNow.Date,
        PlannedEnd = DateTime.UtcNow.Date.AddDays(5),
    };

    private RoleGroup AddGroup(string code)
    {
        var group = new RoleGroup { Code = code, LabelKey = $"rbac.group.{code.ToLowerInvariant()}" };
        _db.RoleGroups.Add(group);
        _db.SaveChanges();
        return group;
    }

    private ApplicationUser AddUser(string roleCode, RoleGroup group)
    {
        var role = new Role { Code = roleCode, Name = roleCode, RoleGroupId = group.Id, IsActive = true };
        if (roleCode == "DESIGN_LEAD")
        {
            var permission = _db.Permissions.SingleOrDefault(item =>
                item.Module == "work.packages" && item.Action == "assign") ??
                new Permission { Module = "work.packages", Action = "assign", IsActive = true };
            role.RolePermissions.Add(new RolePermission { Permission = permission });
        }
        var user = new ApplicationUser
        {
            PhoneNumber = $"09{_db.Users.Count() + 1:00000000}",
            Email = $"{roleCode.ToLowerInvariant()}@nihome.test",
            FullName = roleCode,
            Role = UserRole.USER,
            RoleEntity = role,
            IsActive = true,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    public void Dispose() => _db.Dispose();
}
