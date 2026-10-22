using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class LegalActivationServiceTests : IDisposable
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly Mock<IContractService> _contracts = new();
    private readonly LegalActivationService _service;
    private readonly RoleGroup _construction;
    private readonly RoleGroup _engineering;
    private readonly ApplicationUser _pm;
    private readonly ApplicationUser _legalLead;
    private readonly ApplicationUser _assignee;
    private readonly OperationalProject _project;
    private readonly Customer _customer;
    private readonly LegalChecklistTemplate _template;

    public LegalActivationServiceTests()
    {
        _construction = AddGroup("CONSTRUCTION");
        _engineering = AddGroup("DESIGN");
        _pm = AddUser("PROJECT_MANAGER", _construction);
        _legalLead = AddUser("LEGAL_OFFICER", _construction);
        _assignee = AddUser("DESIGN_ENGINEER", _engineering);
        _customer = new Customer { Name = "NICON Industrial Client", Type = CustomerType.Company };
        _project = new OperationalProject
        {
            Code = "OP-LEGAL-001",
            Name = "Nhà máy logistics NICON",
            Customer = _customer,
            ProjectManagerUserId = _pm.Id,
        };
        _db.OperationalProjects.Add(_project);
        _db.SaveChanges();

        AddMember(_pm, ProjectTeamRoleCode.ProjectManager);
        AddMember(_legalLead, ProjectTeamRoleCode.LegalOfficer);
        AddMember(_assignee, ProjectTeamRoleCode.Architect);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "Giấy phép xây dựng", IsActive = true },
            new MasterDataOption { Category = "permit_type", Code = "pccc", Name = "Thẩm duyệt PCCC", IsActive = true },
            new MasterDataOption { Category = "permit_type", Code = "retired", Name = "Đã ngừng", IsActive = false });
        _db.SaveChanges();

        _template = new LegalChecklistTemplate
        {
            Code = "NICON-LEGAL-TEST",
            Name = "Factory permit checklist",
            NameVi = "Checklist pháp lý nhà máy",
            NameZh = "工厂许可清单",
            NameJa = "工場許可一覧",
            Scope = "Approved test fixture",
            ScopeVi = "Mẫu kiểm thử",
            ScopeZh = "测试模板",
            ScopeJa = "テスト用",
            Status = LegalChecklistTemplateStatus.Published,
            RowVersion = new byte[8],
            ItemsJson = "[{\"PermitTypeCode\":\"gpxd\",\"IsRequired\":true}]",
        };
        _db.LegalChecklistTemplates.Add(_template);
        _db.SaveChanges();

        _contracts.Setup(service => service.GetAsync(
                It.IsAny<int>(), It.IsAny<int>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, int _, bool _, CancellationToken _) => new ContractResponse
            {
                Id = id,
                ContractNumber = $"HD-{id}",
                Status = ContractStatus.InProgress,
                RowVersion = Convert.ToBase64String(new byte[8]),
            });
        _service = new LegalActivationService(
            _db,
            _contracts.Object,
            new OpportunityClosureInvariantService(_db),
            NullLogger<LegalActivationService>.Instance);
    }

    [Fact]
    public async Task Preview_IsReadOnly_AndReturnsProjectCandidates()
    {
        var contract = AddEligibleContract("HD-PREVIEW");
        var trackedBefore = _db.ChangeTracker.Entries().Count();
        var rowCountBefore = await _db.Contracts.CountAsync();

        var result = await _service.PreviewContractAsync(contract.Id, _pm.Id);

        Assert.NotNull(result);
        Assert.True(result!.Eligible);
        Assert.Equal(_project.Id, result.OperationalProjectId);
        Assert.Contains(result.ProjectMembers, member => member.UserId == _legalLead.Id && member.CanLeadLegal);
        Assert.Contains(result.ProjectMembers, member => member.UserId == _assignee.Id && !member.CanLeadLegal);
        Assert.Equal(rowCountBefore, await _db.Contracts.CountAsync());
        Assert.Equal(trackedBefore, _db.ChangeTracker.Entries().Count());
    }

    [Fact]
    public async Task Activate_CreatesPackageTasksPermitsAndAuditAtomically()
    {
        var contract = AddEligibleContract("HD-ACTIVATE");
        var request = ValidRequest();
        request.Permits.Add(Permit("pccc", _legalLead.Id, _pm.Id, "gpxd"));

        var result = await _service.ActivateContractAsync(contract.Id, request, _pm.Id);

        Assert.NotNull(result);
        Assert.Equal(ContractStatus.InProgress, (await _db.Contracts.FindAsync(contract.Id))!.Status);
        var package = await _db.DepartmentWorkPackages.Include(item => item.Tasks).SingleAsync();
        Assert.Equal(WorkPackagePurpose.LegalPermitting, package.Purpose);
        Assert.Equal(_construction.Id, package.DepartmentRoleGroupId);
        Assert.Equal(2, package.Tasks.Count);
        Assert.Equal(2, await _db.PermitChecklistItems.CountAsync());
        Assert.Equal(1, await _db.DepartmentWorkTaskDependencies.CountAsync());
        Assert.Equal(1, await _db.ContractLegalActivations.CountAsync());
        Assert.Equal(2, await _db.PermitChecklistHistory.CountAsync());
    }

    [Fact]
    public async Task Activate_RetryAndSecondContractReusePackageWithoutDuplicates()
    {
        var first = AddEligibleContract("HD-REUSE-01");
        var firstRequest = ValidRequest();
        var initial = await _service.ActivateContractAsync(first.Id, firstRequest, _pm.Id);
        var retry = await _service.ActivateContractAsync(first.Id, firstRequest, _pm.Id);
        var second = AddEligibleContract("HD-REUSE-02");
        var reuseRequest = ValidRequest();
        reuseRequest.Permits.Clear();

        var reused = await _service.ActivateContractAsync(second.Id, reuseRequest, _pm.Id);

        Assert.NotNull(initial);
        Assert.NotNull(retry);
        Assert.NotNull(reused);
        Assert.Equal(initial!.WorkPackageId, retry!.WorkPackageId);
        Assert.Equal(initial.WorkPackageId, reused!.WorkPackageId);
        Assert.True(retry.ReusedExistingPackage);
        Assert.True(reused.ReusedExistingPackage);
        Assert.Equal(1, await _db.DepartmentWorkPackages.CountAsync());
        Assert.Equal(1, await _db.PermitChecklistItems.CountAsync());
        Assert.Equal(2, await _db.ContractLegalActivations.CountAsync());
    }

    [Fact]
    public async Task SetupProject_LinksLegacyPermitWithoutDuplicatingIt()
    {
        var legacy = new PermitChecklistItem
        {
            OperationalProjectId = _project.Id,
            PermitTypeCode = "gpxd",
            Status = PermitStatus.Preparing,
            CreatedByUserId = _pm.Id,
            UpdatedByUserId = _pm.Id,
        };
        _db.PermitChecklistItems.Add(legacy);
        await _db.SaveChangesAsync();

        var result = await _service.SetupProjectAsync(_project.Id, ProjectSetup(), _pm.Id);

        Assert.NotNull(result);
        Assert.Equal(1, await _db.PermitChecklistItems.CountAsync());
        var linked = await _db.PermitChecklistItems.SingleAsync();
        Assert.NotNull(linked.WorkTaskId);
        Assert.Equal(_assignee.Id, linked.OwnerUserId);
        Assert.Contains(await _db.PermitChecklistHistory.ToListAsync(),
            history => history.Action == "LinkedByLegalSetup");
    }

    [Fact]
    public async Task Activate_InvalidAssignmentLeavesContractAndProjectUnchanged()
    {
        var contract = AddEligibleContract("HD-INVALID");
        var request = ValidRequest();
        request.Permits[0].ApproverUserId = _assignee.Id;

        var error = await Assert.ThrowsAsync<LegalActivationException>(() =>
            _service.ActivateContractAsync(contract.Id, request, _pm.Id));

        Assert.Equal("LEGAL_SELF_APPROVAL", error.Code);
        Assert.Equal(ContractStatus.Signed, (await _db.Contracts.FindAsync(contract.Id))!.Status);
        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
        Assert.Empty(await _db.PermitChecklistItems.ToListAsync());
        Assert.Empty(await _db.ContractLegalActivations.ToListAsync());
    }

    [Fact]
    public async Task Activate_AssignmentOutsideActiveProjectTeamIsRejected()
    {
        var contract = AddEligibleContract("HD-OUTSIDE-TEAM");
        var outsider = AddUser("OUTSIDE", _engineering);
        var request = ValidRequest();
        request.Permits[0].AssigneeUserId = outsider.Id;

        var error = await Assert.ThrowsAsync<LegalActivationException>(() =>
            _service.ActivateContractAsync(contract.Id, request, _pm.Id));

        Assert.Equal("LEGAL_MEMBER_INVALID", error.Code);
        Assert.Equal(ContractStatus.Signed, (await _db.Contracts.FindAsync(contract.Id))!.Status);
        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
        Assert.Empty(await _db.PermitChecklistItems.ToListAsync());
        Assert.Empty(await _db.ContractLegalActivations.ToListAsync());
    }

    [Fact]
    public async Task Activate_CyclicDependenciesAreRejectedBeforeAnyWrite()
    {
        var contract = AddEligibleContract("HD-CYCLE");
        var request = ValidRequest();
        request.Permits[0].PredecessorPermitTypeCodes.Add("pccc");
        request.Permits.Add(Permit("pccc", _legalLead.Id, _pm.Id, "gpxd"));

        var error = await Assert.ThrowsAsync<LegalActivationException>(() =>
            _service.ActivateContractAsync(contract.Id, request, _pm.Id));

        Assert.Equal("LEGAL_PREDECESSOR_CYCLE", error.Code);
        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
    }

    [Theory]
    [InlineData(ContractDirection.Downstream, ContractType.Construction, ContractStatus.Signed)]
    [InlineData(ContractDirection.Upstream, ContractType.Supply, ContractStatus.Signed)]
    [InlineData(ContractDirection.Upstream, ContractType.Design, ContractStatus.Draft)]
    public async Task Activate_IneligibleDirectionTypeOrStatusIsRejected(
        ContractDirection direction, ContractType type, ContractStatus status)
    {
        var contract = AddEligibleContract($"HD-{direction}-{type}-{status}");
        contract.Direction = direction;
        contract.Type = type;
        contract.Status = status;
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<LegalActivationException>(() =>
            _service.ActivateContractAsync(contract.Id, ValidRequest(), _pm.Id));

        Assert.Equal("LEGAL_CONTRACT_NOT_ELIGIBLE", error.Code);
        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ApplyingTemplateToLegacyPackagePreservesScheduleFilesAndCompletedWork()
    {
        var contract = AddEligibleContract("HD-LEGACY-SNAPSHOT");
        await _service.ActivateContractAsync(contract.Id, ValidRequest(), _pm.Id);
        var package = await _db.DepartmentWorkPackages.SingleAsync();
        var task = await _db.DepartmentWorkTasks.SingleAsync();
        var permit = await _db.PermitChecklistItems.SingleAsync();
        package.LegalChecklistTemplateId = null; package.LegalChecklistSnapshotJson = null;
        package.RowVersion = new byte[8]; permit.IsRequired = false;
        task.Status = WorkTaskStatus.Verified;
        permit.Status = PermitStatus.Issued; permit.IssuedAt = DateTime.UtcNow;
        permit.IssuedFilePath = "/files/business-documents/permits/legacy-issued.pdf";
        var start = package.PlannedStart; var end = package.PlannedEnd; var lead = package.LeadUserId;
        await _db.SaveChangesAsync();
        var request = ProjectSetup(); request.WorkPackageRowVersion = Convert.ToBase64String(package.RowVersion);
        request.LegalLeadUserId = _legalLead.Id;
        request.PlannedEnd = request.PlannedEnd.AddMonths(2);
        await _service.SetupProjectAsync(_project.Id, request, _pm.Id);
        Assert.Equal(start, package.PlannedStart); Assert.Equal(end, package.PlannedEnd); Assert.Equal(lead, package.LeadUserId);
        Assert.Equal(WorkTaskStatus.Verified, task.Status);
        Assert.Equal(PermitStatus.Issued, permit.Status);
        Assert.Equal("/files/business-documents/permits/legacy-issued.pdf", permit.IssuedFilePath);
        Assert.True(permit.IsRequired);
        Assert.Single(_db.PermitChecklistItems);
        Assert.Single(_db.DepartmentWorkTasks);
    }

    [Fact]
    public async Task ApplyingTemplateCannotCreateTaskOutsidePreservedLegacyPackageSchedule()
    {
        var contract = AddEligibleContract("HD-LEGACY-BOUNDS");
        await _service.ActivateContractAsync(contract.Id, ValidRequest(), _pm.Id);
        var package = await _db.DepartmentWorkPackages.SingleAsync();
        package.LegalChecklistTemplateId = null; package.LegalChecklistSnapshotJson = null;
        package.RowVersion = new byte[8];
        await _db.SaveChangesAsync();
        var request = ProjectSetup(); request.WorkPackageRowVersion = Convert.ToBase64String(package.RowVersion);
        request.PlannedEnd = request.PlannedEnd.AddMonths(3);
        var added = Permit("pccc", _assignee.Id, _legalLead.Id);
        added.PlannedEnd = request.PlannedEnd;
        request.Permits.Add(added);
        var error = await Assert.ThrowsAsync<LegalActivationException>(() => _service.SetupProjectAsync(_project.Id, request, _pm.Id));
        Assert.Equal("LEGAL_TASK_DATES_INVALID", error.Code);
        Assert.Null(package.LegalChecklistTemplateId);
        Assert.Single(_db.DepartmentWorkTasks);
    }

    [Fact]
    public async Task Activate_MissingRequiredPermitRejectsBeforeCreatingAnyPackage()
    {
        var contract = AddEligibleContract("HD-MISSING-REQUIRED");
        var request = ValidRequest();
        request.Permits = [Permit("pccc", _assignee.Id, _legalLead.Id)];
        var error = await Assert.ThrowsAsync<LegalActivationException>(() => _service.ActivateContractAsync(contract.Id, request, _pm.Id));
        Assert.Equal("LEGAL_REQUIRED_PERMITS_MISSING", error.Code);
        Assert.Empty(_db.DepartmentWorkPackages);
        Assert.Empty(_db.PermitChecklistItems);
        Assert.Equal(ContractStatus.Signed, contract.Status);
    }

    [Fact]
    public async Task AppliedSnapshotSurvivesRetirement_AndBlocksSwitchingTemplates()
    {
        var contract = AddEligibleContract("HD-SNAPSHOT");
        await _service.ActivateContractAsync(contract.Id, ValidRequest(), _pm.Id);
        var package = await _db.DepartmentWorkPackages.SingleAsync();
        var snapshot = package.LegalChecklistSnapshotJson;
        Assert.True((await _db.PermitChecklistItems.SingleAsync()).IsRequired);
        _template.Status = LegalChecklistTemplateStatus.Retired;
        await _db.SaveChangesAsync();
        var preview = await _service.PreviewProjectAsync(_project.Id, _pm.Id);
        Assert.Empty(preview!.Templates);
        Assert.Equal(_template.Id, preview.AppliedTemplate!.Id);
        var request = ProjectSetup(); request.TemplateId = _template.Id + 100;
        var error = await Assert.ThrowsAsync<LegalActivationException>(() => _service.SetupProjectAsync(_project.Id, request, _pm.Id));
        Assert.Equal("LEGAL_TEMPLATE_CHANGE_BLOCKED", error.Code);
        Assert.Equal(snapshot, package.LegalChecklistSnapshotJson);
    }

    private RoleGroup AddGroup(string code)
    {
        var group = new RoleGroup { Code = code, LabelKey = $"group.{code}" };
        _db.RoleGroups.Add(group);
        _db.SaveChanges();
        return group;
    }

    private ApplicationUser AddUser(string roleCode, RoleGroup group)
    {
        var role = new Role { Code = roleCode, Name = roleCode, RoleGroupId = group.Id };
        var user = new ApplicationUser
        {
            FullName = roleCode,
            Email = $"{roleCode.ToLowerInvariant()}@example.com",
            PhoneNumber = $"090{_db.Users.Count() + 1:0000000}",
            PasswordHash = "x",
            IsActive = true,
            RoleEntity = role,
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private void AddMember(ApplicationUser user, ProjectTeamRoleCode role)
    {
        _db.OperationalProjectMembers.Add(new OperationalProjectMember
        {
            OperationalProjectId = _project.Id,
            UserId = user.Id,
            Position = role.ToString(),
            StartedAt = DateTime.UtcNow.AddDays(-10),
            CreatedByUserId = _pm.Id,
            UpdatedByUserId = _pm.Id,
            Roles =
            [
                new OperationalProjectMemberRole
                {
                    RoleCode = role,
                    StartedAt = DateTime.UtcNow.AddDays(-10),
                },
            ],
        });
    }

    private Contract AddEligibleContract(string number)
    {
        var contract = new Contract
        {
            ContractNumber = number,
            CustomerId = _customer.Id,
            OperationalProjectId = _project.Id,
            Direction = ContractDirection.Upstream,
            Type = ContractType.DesignAndBuild,
            Status = ContractStatus.Signed,
            SignedDate = DateTime.UtcNow.AddDays(-2),
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(6),
            OwnerUserId = _pm.Id,
            RowVersion = new byte[8],
        };
        _db.Contracts.Add(contract);
        _db.SaveChanges();
        _db.ContractAttachments.Add(new ContractAttachment
        {
            ContractId = contract.Id,
            Kind = ContractAttachmentKind.SignedScan,
            FilePath = $"/files/contracts/{number}.pdf",
            OriginalFileName = $"{number}.pdf",
            UploadedByUserId = _pm.Id,
        });
        _db.SaveChanges();
        return contract;
    }

    private ActivateContractWithLegalRequest ValidRequest() => new()
    {
        TemplateId = _template.Id,
        TemplateRowVersion = Convert.ToBase64String(_template.RowVersion),
        LegalLeadUserId = _legalLead.Id,
        PlannedStart = DateTime.UtcNow.Date,
        PlannedEnd = DateTime.UtcNow.Date.AddMonths(6),
        RowVersion = Convert.ToBase64String(new byte[8]),
        Permits = [Permit("gpxd", _assignee.Id, _legalLead.Id)],
    };

    private SetupProjectLegalRequest ProjectSetup() => new()
    {
        TemplateId = _template.Id,
        TemplateRowVersion = Convert.ToBase64String(_template.RowVersion),
        LegalLeadUserId = _legalLead.Id,
        PlannedStart = DateTime.UtcNow.Date,
        PlannedEnd = DateTime.UtcNow.Date.AddMonths(6),
        Permits = [Permit("gpxd", _assignee.Id, _legalLead.Id)],
    };

    private LegalPermitSetupItemRequest Permit(
        string code, int assigneeId, int approverId, params string[] predecessors) => new()
        {
            PermitTypeCode = code,
            IssuingAgency = "Cơ quan chuyên ngành",
            AssigneeUserId = assigneeId,
            ApproverUserId = approverId,
            PlannedStart = DateTime.UtcNow.Date,
            PlannedEnd = DateTime.UtcNow.Date.AddMonths(3),
            PredecessorPermitTypeCodes = predecessors.ToList(),
        };
}
