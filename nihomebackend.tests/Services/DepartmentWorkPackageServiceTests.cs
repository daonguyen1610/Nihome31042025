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
    private readonly RoleGroup _procurementGroup;
    private readonly RoleGroup _crmGroup;
    private readonly RoleGroup _financeGroup;
    private readonly ApplicationUser _pm;
    private readonly ApplicationUser _admin;
    private readonly ApplicationUser _procurementLead;
    private readonly ApplicationUser _warehouseStaff;
    private readonly ApplicationUser _sales;
    private readonly ApplicationUser _accountantLead;
    private readonly ApplicationUser _accountantStaff;
    private readonly OperationalProject _project;
    private readonly Vendor _vendor;

    public DepartmentWorkPackageServiceTests()
    {
        _procurementGroup = AddGroup("PROCUREMENT");
        _crmGroup = AddGroup("CRM");
        _financeGroup = AddGroup("FINANCE");
        var constructionGroup = AddGroup("CONSTRUCTION");
        _pm = AddUser("PM", constructionGroup);
        _admin = AddUser("SUPER_ADMIN", constructionGroup);
        _admin.Role = UserRole.ADMIN;
        _db.SaveChanges();
        _procurementLead = AddUser("PROCUREMENT", _procurementGroup);
        _warehouseStaff = AddUser("WAREHOUSE", _procurementGroup);
        _sales = AddUser("SALE", _crmGroup);
        _accountantLead = AddUser("ACCOUNTANT", _financeGroup);
        _accountantStaff = AddUser("ACCOUNTANT_STAFF", _financeGroup);
        var customer = new Customer { Name = "NICON Industrial Client", Type = CustomerType.Company, SourceCode = "referral" };
        _project = new OperationalProject
        {
            Code = "DB-2026-009",
            Name = "Nhà máy sản xuất linh kiện",
            Customer = customer,
            ProjectManagerUserId = _pm.Id,
        };
        _db.OperationalProjects.Add(_project);
        _vendor = new Vendor { VendorCode = "NCC-MEP-001", CompanyName = "Công ty Thiết bị MEP An Phát", VendorType = VendorType.Supplier, IsActive = true };
        _db.Vendors.Add(_vendor);
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
        Assert.Equal(_procurementGroup.Id, result.DepartmentRoleGroupId);
        Assert.Equal(_procurementLead.Id, result.LeadUserId);
        Assert.Equal("Planned", result.Status);
    }

    [Fact]
    public async Task UnrelatedUser_CannotCreateProjectPackage()
    {
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(ProjectRequest(), _warehouseStaff.Id));

        Assert.Empty(await _db.DepartmentWorkPackages.ToListAsync());
    }

    [Fact]
    public async Task TenderPreparer_CanAssignPlanPackageForTenderProject()
    {
        var tender = new Tender
        {
            Code = "TD-PLAN-OWNER-001",
            Name = "Gói thầu trung tâm logistics NICON",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            PreparerUserId = _sales.Id,
            CreatedByUserId = _sales.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(30),
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var request = ProjectRequest();
        request.Purpose = "TenderPlanning";
        request.SourceTenderId = tender.Id;

        var result = await _service.CreateAsync(request, _sales.Id);

        Assert.Equal(tender.Id, result.SourceTenderId);
        Assert.Equal(_project.Id, result.OperationalProjectId);
    }

    [Fact]
    public async Task ProjectPackage_RejectsLeadWithoutAssignmentPermission()
    {
        var request = ProjectRequest();
        request.LeadUserId = _warehouseStaff.Id;

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
            _service.CreateAsync(request, _procurementLead.Id));

        request.OperationalProjectId = null;
        request.LeadUserId = _warehouseStaff.Id;
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _procurementLead.Id));
    }

    [Fact]
    public async Task InternalPackage_AssignsTasksAcrossParticipatingDepartments()
    {
        var package = await _service.CreateAsync(new CreateDepartmentWorkPackageRequest
        {
            Kind = "Internal",
            Name = "Đào tạo phối hợp liên phòng",
            DepartmentRoleGroupId = _procurementGroup.Id,
            ParticipantDepartmentRoleGroupIds = [_crmGroup.Id],
            LeadUserId = _procurementLead.Id,
        }, _procurementLead.Id);

        var request = TaskRequest(_sales.Id, _procurementLead.Id, "INT-CRM-01");
        request.DepartmentRoleGroupId = _crmGroup.Id;
        request.ApproverUserId = null;
        var task = await _service.AddTaskAsync(package.Id, request, _procurementLead.Id);

        Assert.Equal(_crmGroup.Id, task.DepartmentRoleGroupId);
        Assert.Equal(2, package.ParticipantDepartments.Count);
    }

    [Fact]
    public async Task OnlyLead_AssignsTaskToActiveMemberOfSameDepartment()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var valid = TaskRequest(_warehouseStaff.Id, _procurementLead.Id);

        await Assert.ThrowsAsync<DepartmentWorkPackageScopeException>(() =>
            _service.AddTaskAsync(package.Id, valid, _pm.Id));

        var task = await _service.AddTaskAsync(package.Id, valid, _procurementLead.Id);
        Assert.Equal(_warehouseStaff.Id, task.AssigneeUserId);

        var wrongDepartment = TaskRequest(_sales.Id, _procurementLead.Id, "SUP-02");
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.AddTaskAsync(package.Id, wrongDepartment, _procurementLead.Id));
    }

    [Fact]
    public async Task KpiTask_RequiresMatchingActiveDefinitionAndIndependentVerifier()
    {
        var definition = new KpiDefinition
        {
            Code = "WAREHOUSE_VERIFIED_WORK",
            RoleCode = "WAREHOUSE",
            NameKey = "kpi.definition.WAREHOUSE_VERIFIED_WORK",
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
        var request = TaskRequest(_warehouseStaff.Id, _procurementLead.Id);
        request.KpiDefinitionId = definition.Id;

        var task = await _service.AddTaskAsync(package.Id, request, _procurementLead.Id);

        Assert.Equal(definition.Id, task.KpiDefinitionId);
        Assert.False(task.CanVerify);
    }

    [Fact]
    public async Task ProjectTaskWithoutKpi_RequiresIndependentVerificationBeforePackageClosure()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var task = await _service.AddTaskAsync(package.Id,
            TaskRequest(_warehouseStaff.Id, _procurementLead.Id), _procurementLead.Id);
        var active = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = package.Name,
            Status = "Active",
            PlannedStart = package.PlannedStart,
            PlannedEnd = package.PlannedEnd,
            RowVersion = package.RowVersion,
        }, _pm.Id);

        var completed = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Completed",
                ProgressPercent = 100,
                RowVersion = task.RowVersion,
            }, _warehouseStaff.Id);
        Assert.True((await _service.GetAsync(package.Id, _procurementLead.Id, false))!
            .Tasks.Single().CanVerify);

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
            {
                Name = active!.Name,
                Status = "Completed",
                PlannedStart = active.PlannedStart,
                PlannedEnd = active.PlannedEnd,
                RowVersion = active.RowVersion,
            }, _pm.Id));
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.VerifyTaskAsync(package.Id, task.Id,
                new VerifyDepartmentWorkTaskRequest { Confirm = true, RowVersion = completed!.RowVersion },
                _warehouseStaff.Id));
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.VerifyTaskAsync(package.Id, task.Id,
                new VerifyDepartmentWorkTaskRequest { Confirm = false, RowVersion = completed!.RowVersion },
                _procurementLead.Id));
        Assert.Equal(WorkTaskStatus.Completed,
            (await _db.DepartmentWorkTasks.SingleAsync(item => item.Id == task.Id)).Status);

        var returned = await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest
            {
                Confirm = false,
                Reason = "Bổ sung biên bản xác nhận vật tư",
                RowVersion = completed!.RowVersion,
            }, _procurementLead.Id);
        Assert.Equal("InProgress", returned!.Status);
        var resubmitted = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Completed",
                ProgressPercent = 100,
                RowVersion = returned.RowVersion,
            }, _warehouseStaff.Id);
        var verified = await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest { Confirm = true, RowVersion = resubmitted!.RowVersion },
            _procurementLead.Id);
        var refreshed = await _service.GetAsync(package.Id, _pm.Id, false);
        var closed = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = refreshed!.Name,
            Status = "Completed",
            PlannedStart = refreshed.PlannedStart,
            PlannedEnd = refreshed.PlannedEnd,
            RowVersion = refreshed.RowVersion,
        }, _pm.Id);

        Assert.Equal("Verified", verified!.Status);
        Assert.Equal("Completed", closed!.Status);
        _notifications.Verify(item => item.NotifyFromTemplateAsync(
            _procurementLead.Id, "work-task.project-awaiting-verification",
            It.IsAny<IDictionary<string, string>>(), It.IsAny<string>(),
            It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProjectTask_RejectsSelfApprovalWhenLeadIsAssignee()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var request = TaskRequest(_procurementLead.Id, _procurementLead.Id);
        request.ApproverUserId = null;

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.AddTaskAsync(package.Id, request, _procurementLead.Id));
        Assert.Empty(await _db.DepartmentWorkTasks.ToListAsync());
    }

    [Fact]
    public async Task InternalTaskWithoutKpi_StillClosesWithoutVerification()
    {
        var package = await _service.CreateAsync(new CreateDepartmentWorkPackageRequest
        {
            Kind = "Internal",
            Name = "Đào tạo quy trình quản lý vật tư",
            DepartmentRoleGroupId = _procurementGroup.Id,
            LeadUserId = _procurementLead.Id,
        }, _procurementLead.Id);
        var taskRequest = TaskRequest(_warehouseStaff.Id, _procurementLead.Id);
        taskRequest.ApproverUserId = null;
        taskRequest.DepartmentRoleGroupId = _procurementGroup.Id;
        var task = await _service.AddTaskAsync(package.Id, taskRequest, _procurementLead.Id);
        var active = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = package.Name,
            Status = "Active",
            RowVersion = package.RowVersion,
        }, _procurementLead.Id);
        var completed = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Completed",
                ProgressPercent = 100,
                RowVersion = task.RowVersion,
            }, _warehouseStaff.Id);
        var closed = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = active!.Name,
            Status = "Completed",
            RowVersion = (await _service.GetAsync(package.Id, _procurementLead.Id, false))!.RowVersion,
        }, _procurementLead.Id);

        Assert.False(completed!.CanVerify);
        Assert.Equal("Completed", closed!.Status);
    }

    [Fact]
    public async Task Task_CannotStartUntilPackageAndPredecessorAreReady()
    {
        var package = await _service.CreateAsync(ProjectRequest(), _pm.Id);
        var predecessor = await _service.AddTaskAsync(
            package.Id, TaskRequest(_warehouseStaff.Id, _procurementLead.Id, "SUP-01"), _procurementLead.Id);
        var dependentRequest = TaskRequest(_warehouseStaff.Id, _procurementLead.Id, "SUP-02");
        dependentRequest.PredecessorTaskIds = [predecessor.Id];
        var dependent = await _service.AddTaskAsync(package.Id, dependentRequest, _procurementLead.Id);
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
            }, _warehouseStaff.Id));

        (await _db.DepartmentWorkPackages.FindAsync(package.Id))!.Status = WorkPackageStatus.Active;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateProgressAsync(package.Id, dependent.Id, new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "InProgress",
                ProgressPercent = 10,
                RowVersion = Convert.ToBase64String(dependentVersion),
            }, _warehouseStaff.Id));

        await _service.UpdateProgressAsync(package.Id, predecessor.Id, new UpdateDepartmentWorkTaskProgressRequest
        {
            Status = "Completed",
            ProgressPercent = 100,
            RowVersion = Convert.ToBase64String(predecessorVersion),
        }, _warehouseStaff.Id);
        var started = await _service.UpdateProgressAsync(package.Id, dependent.Id, new UpdateDepartmentWorkTaskProgressRequest
        {
            Status = "InProgress",
            ProgressPercent = 10,
            RowVersion = Convert.ToBase64String(dependentVersion),
        }, _warehouseStaff.Id);

        Assert.Equal("InProgress", started!.Status);
    }

    [Fact]
    public async Task TenderPlan_CannotCloseUntilEveryTaskIsIndependentlyVerified()
    {
        var tender = new Tender
        {
            Code = "TD-PLAN-001",
            Name = "Gói thầu nhà máy NICON",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(30),
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var request = ProjectRequest();
        request.Purpose = "TenderPlanning";
        request.SourceTenderId = tender.Id;
        var package = await _service.CreateAsync(request, _pm.Id);
        var task = await _service.AddTaskAsync(
            package.Id, TaskRequest(_warehouseStaff.Id, _procurementLead.Id), _procurementLead.Id);
        var entity = await _db.DepartmentWorkPackages.Include(item => item.Tasks)
            .SingleAsync(item => item.Id == package.Id);
        entity.Status = WorkPackageStatus.Active;
        entity.Tasks.Single().Status = WorkTaskStatus.Completed;
        entity.Tasks.Single().ProgressPercent = 100;
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
            {
                Name = package.Name,
                Status = "Completed",
                PlannedStart = package.PlannedStart,
                PlannedEnd = package.PlannedEnd,
                RowVersion = package.RowVersion,
            }, _pm.Id));

        Assert.Contains("duyệt độc lập", error.Message);
        Assert.Equal(WorkPackageStatus.Active, entity.Status);
        Assert.Equal(WorkTaskStatus.Completed, entity.Tasks.Single().Status);
    }

    [Fact]
    public async Task TenderPlan_AllowsAnyDepartmentAndVerifiesTaskWithoutKpi()
    {
        var designGroup = AddGroup("DESIGN");
        var designLead = AddUser("DESIGN_LEAD", designGroup, canAssign: true);
        var designer = AddUser("DESIGN", designGroup);
        var tender = new Tender
        {
            Code = "TD-PLAN-DESIGN-001",
            Name = "Gói thầu thiết kế nhà máy NICON",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            PreparerUserId = _sales.Id,
            CreatedByUserId = _sales.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(30),
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var request = ProjectRequest();
        request.Purpose = "TenderPlanning";
        request.SourceTenderId = tender.Id;
        request.DepartmentRoleGroupId = designGroup.Id;
        request.LeadUserId = designLead.Id;

        var package = await _service.CreateAsync(request, _sales.Id);
        var started = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = package.Name,
            Status = "Active",
            PlannedStart = package.PlannedStart,
            PlannedEnd = package.PlannedEnd,
            RowVersion = package.RowVersion,
        }, _sales.Id);
        var taskRequest = TaskRequest(designer.Id, designLead.Id, "DES-01");
        taskRequest.KpiDefinitionId = null;
        var task = await _service.AddTaskAsync(package.Id, taskRequest, designLead.Id);
        var completed = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Completed",
                ProgressPercent = 100,
                RowVersion = task.RowVersion,
            }, designer.Id);
        var verified = await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest { Confirm = true, RowVersion = completed!.RowVersion }, designLead.Id);
        var closed = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = started!.Name,
            Status = "Completed",
            PlannedStart = started.PlannedStart,
            PlannedEnd = started.PlannedEnd,
            RowVersion = (await _service.GetAsync(package.Id, _sales.Id, canViewAll: true))!.RowVersion,
        }, _sales.Id);

        Assert.Equal("TenderPlanning", closed!.Purpose);
        Assert.Equal("Verified", verified!.Status);
        Assert.Equal("Completed", closed.Status);
    }

    [Fact]
    public async Task TenderPlan_RejectsSelfVerificationAndDuplicateDepartment()
    {
        var tender = new Tender
        {
            Code = "TD-PLAN-GUARD-001",
            Name = "Gói thầu kho vận NICON",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            PreparerUserId = _sales.Id,
            CreatedByUserId = _sales.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(30),
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var request = ProjectRequest();
        request.Purpose = "TenderPlanning";
        request.SourceTenderId = tender.Id;
        var validEnd = request.PlannedEnd;
        request.PlannedEnd = tender.SubmissionDeadline.AddDays(1);
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _sales.Id));
        request.PlannedEnd = validEnd;
        var package = await _service.CreateAsync(request, _sales.Id);
        var legacyPackage = await _db.DepartmentWorkPackages.FindAsync(package.Id);
        legacyPackage!.Purpose = WorkPackagePurpose.General;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.CreateAsync(request, _sales.Id));
        var selfApproved = TaskRequest(_procurementLead.Id, _procurementLead.Id);
        selfApproved.ApproverUserId = null;
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.AddTaskAsync(package.Id, selfApproved, _procurementLead.Id));
    }

    [Fact]
    public async Task ProcurementInputOffer_RequiresIndependentApproval_AndCanFeedQuote()
    {
        var package = AddBusinessPackage(WorkPackagePurpose.ProcurementInputPricing, _procurementGroup, _procurementLead);
        var offer = await _service.AddInputPriceOfferAsync(package.Id, new UpsertInputPriceOfferRequest
        {
            VendorId = _vendor.Id,
            SupplierReference = "AP-MEP-2026-042",
            Currency = "VND",
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Lines =
            [
                new() { ItemCode = "MEP-CABLE-01", Description = "Cáp điện chống cháy", Unit = "m", Quantity = 1200, UnitPrice = 85000, VatPercent = 10, SortOrder = 1 },
            ],
        }, _procurementLead.Id);
        offer = (await _service.SubmitInputPriceOfferAsync(package.Id, offer.Id,
            new SubmitBusinessRecordRequest { RowVersion = offer.RowVersion }, _procurementLead.Id))!;

        var selfApproval = await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.DecideInputPriceOfferAsync(package.Id, offer.Id,
                new DecideBusinessRecordRequest { Approved = true, RowVersion = offer.RowVersion }, _procurementLead.Id));
        Assert.Contains("không được tự phê duyệt", selfApproval.Message);

        var approved = await _service.DecideInputPriceOfferAsync(package.Id, offer.Id,
            new DecideBusinessRecordRequest { Approved = true, RowVersion = offer.RowVersion }, _admin.Id);

        Assert.Equal("Approved", approved!.Status);
        Assert.Equal(112_200_000m, approved.GrandTotal);
    }

    [Fact]
    public async Task ProcurementInputOffer_RejectsDuplicateSupplierReference()
    {
        var package = AddBusinessPackage(WorkPackagePurpose.ProcurementInputPricing, _procurementGroup, _procurementLead);
        UpsertInputPriceOfferRequest Request() => new()
        {
            VendorId = _vendor.Id,
            SupplierReference = "AP-MEP-2026-042",
            Currency = "VND",
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Lines =
            [
                new() { ItemCode = "MEP-CABLE-01", Description = "Cáp điện chống cháy", Unit = "m", Quantity = 100, UnitPrice = 85000, VatPercent = 10, SortOrder = 1 },
            ],
        };

        var first = await _service.AddInputPriceOfferAsync(package.Id, Request(), _procurementLead.Id);
        Assert.NotNull(first);

        var duplicate = await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.AddInputPriceOfferAsync(package.Id, Request(), _procurementLead.Id));
        Assert.Contains("AP-MEP-2026-042", duplicate.Message);
    }

    [Fact]
    public async Task FinanceReview_UsesPurposeSpecificChecklist_AndIndependentApproval()
    {
        var package = AddBusinessPackage(WorkPackagePurpose.FinanceCommercialReview, _financeGroup, _accountantLead);
        var review = await _service.SaveFinanceReviewAsync(package.Id, new UpsertFinanceReviewRequest
        {
            ContractConditionsReviewed = true,
            GuaranteesReviewed = true,
            PaymentTermsReviewed = false,
            FinancialDossierReviewed = false,
            Findings = "Điều khoản tạm ứng cần đối chiếu với bảo lãnh.",
        }, _accountantStaff.Id);

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.SubmitFinanceReviewAsync(package.Id,
                new SubmitBusinessRecordRequest { RowVersion = review.RowVersion }, _accountantStaff.Id));

        review = await _service.SaveFinanceReviewAsync(package.Id, new UpsertFinanceReviewRequest
        {
            ContractConditionsReviewed = true,
            GuaranteesReviewed = true,
            PaymentTermsReviewed = true,
            FinancialDossierReviewed = false,
            Findings = "Đủ điều kiện trình duyệt.",
            RowVersion = review.RowVersion,
        }, _accountantStaff.Id);
        review = (await _service.SubmitFinanceReviewAsync(package.Id,
            new SubmitBusinessRecordRequest { RowVersion = review.RowVersion }, _accountantStaff.Id))!;
        var approved = await _service.DecideFinanceReviewAsync(package.Id,
            new DecideBusinessRecordRequest { Approved = true, RowVersion = review.RowVersion }, _accountantLead.Id);

        Assert.Equal("Approved", approved!.Status);
        Assert.Equal(_accountantLead.Id, approved.ReviewedByUserId);
    }

    [Fact]
    public async Task LegalTask_CannotBeSubmittedUntilPermitIsIssuedWithEvidence()
    {
        var package = AddBusinessPackage(WorkPackagePurpose.LegalPermitting, _procurementGroup, _procurementLead);
        var task = new DepartmentWorkTask
        {
            WorkPackageId = package.Id,
            WbsCode = "L-01",
            Title = "Giấy phép xây dựng",
            DepartmentRoleGroupId = _procurementGroup.Id,
            AssigneeUserId = _warehouseStaff.Id,
            ApproverUserId = _procurementLead.Id,
            Status = WorkTaskStatus.InProgress,
            ProgressPercent = 80,
            PlannedStart = DateTime.UtcNow.Date.AddDays(-10),
            PlannedEnd = DateTime.UtcNow.Date.AddDays(10),
            CreatedByUserId = _procurementLead.Id,
            UpdatedByUserId = _warehouseStaff.Id,
        };
        var permit = new PermitChecklistItem
        {
            OperationalProjectId = _project.Id,
            WorkTask = task,
            PermitTypeCode = "gpxd",
            Status = PermitStatus.UnderReview,
            SubmittedAt = DateTime.UtcNow.AddDays(-3),
            SubmittedFilePath = "/files/business-documents/permits/gpxd-submitted.pdf",
            CreatedByUserId = _procurementLead.Id,
            UpdatedByUserId = _warehouseStaff.Id,
        };
        _db.PermitChecklistItems.Add(permit);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateProgressAsync(package.Id, task.Id,
                new UpdateDepartmentWorkTaskProgressRequest { Status = "Completed", ProgressPercent = 100 },
                _warehouseStaff.Id));

        permit.Status = PermitStatus.Issued;
        permit.IssuedAt = DateTime.UtcNow.AddDays(-1);
        permit.IssuedFilePath = "/files/business-documents/permits/gpxd-issued.pdf";
        await _db.SaveChangesAsync();
        var completed = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest { Status = "Completed", ProgressPercent = 100 },
            _warehouseStaff.Id);

        Assert.Equal("Completed", completed!.Status);
        Assert.Equal(100, completed.ProgressPercent);
    }

    [Fact]
    public async Task LegalTask_ReturnRequiresReason_AndApproverCanVerify()
    {
        var package = AddBusinessPackage(WorkPackagePurpose.LegalPermitting, _procurementGroup, _procurementLead);
        var task = new DepartmentWorkTask
        {
            WorkPackageId = package.Id,
            WbsCode = "L-02",
            Title = "Thẩm duyệt PCCC",
            DepartmentRoleGroupId = _procurementGroup.Id,
            AssigneeUserId = _warehouseStaff.Id,
            ApproverUserId = _procurementLead.Id,
            Status = WorkTaskStatus.Completed,
            ProgressPercent = 100,
            PlannedStart = DateTime.UtcNow.Date.AddDays(-10),
            PlannedEnd = DateTime.UtcNow.Date.AddDays(10),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            CreatedByUserId = _procurementLead.Id,
            UpdatedByUserId = _warehouseStaff.Id,
        };
        _db.DepartmentWorkTasks.Add(task);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.VerifyTaskAsync(package.Id, task.Id,
                new VerifyDepartmentWorkTaskRequest { Confirm = false, Reason = "  " }, _procurementLead.Id));
        Assert.Equal(WorkTaskStatus.Completed, (await _db.DepartmentWorkTasks.FindAsync(task.Id))!.Status);

        var returned = await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest { Confirm = false, Reason = "Bổ sung bản đóng dấu." },
            _procurementLead.Id);
        Assert.Equal("InProgress", returned!.Status);
        Assert.Equal(99, returned.ProgressPercent);
        Assert.Null(returned.CompletedAt);

        task.Status = WorkTaskStatus.Completed;
        task.ProgressPercent = 100;
        task.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var verified = await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest { Confirm = true }, _procurementLead.Id);

        Assert.Equal("Verified", verified!.Status);
        Assert.Equal(_procurementLead.Id, verified.VerifiedByUserId);
        var history = await _db.DepartmentWorkPackageHistory
            .Where(item => item.EntityId == task.Id).Select(item => item.Action).ToListAsync();
        Assert.Contains("Returned", history);
        Assert.Contains("Verified", history);
    }

    private DepartmentWorkPackage AddBusinessPackage(WorkPackagePurpose purpose, RoleGroup group, ApplicationUser lead)
    {
        var package = new DepartmentWorkPackage
        {
            Code = $"WP-{purpose}-{Guid.NewGuid():N}"[..40],
            Kind = WorkPackageKind.Project,
            Purpose = purpose,
            Name = purpose.ToString(),
            OperationalProjectId = _project.Id,
            DepartmentRoleGroupId = group.Id,
            LeadUserId = lead.Id,
            Status = WorkPackageStatus.Active,
            CreatedByUserId = _pm.Id,
            UpdatedByUserId = _pm.Id,
        };
        _db.DepartmentWorkPackages.Add(package);
        _db.SaveChanges();
        return package;
    }

    [Theory]
    [InlineData("clear-both-dates")]
    [InlineData("clear-end-date")]
    [InlineData("after-tender-deadline")]
    public async Task TenderPlanningUpdate_RejectsInvalidDatesAndPreservesPlan(string scenario)
    {
        var tender = new Tender
        {
            Code = "TD-AUDIT-DATES-001",
            Name = "Goi thau nha may NICON - kiem tra ke hoach",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            SubmissionDeadline = DateTime.UtcNow.Date.AddDays(30),
            Status = TenderStatus.Preparing,
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var create = ProjectRequest();
        create.Purpose = "TenderPlanning";
        create.SourceTenderId = tender.Id;
        var package = await _service.CreateAsync(create, _pm.Id);
        var originalStart = package.PlannedStart;
        var originalEnd = package.PlannedEnd;
        var originalHistoryCount = await _db.DepartmentWorkPackageHistory.CountAsync();
        var update = new UpdateDepartmentWorkPackageRequest
        {
            Name = package.Name,
            Status = package.Status,
            PlannedStart = scenario == "clear-both-dates" ? null : package.PlannedStart,
            PlannedEnd = scenario == "after-tender-deadline" ? tender.SubmissionDeadline.AddDays(1) : null,
            RowVersion = package.RowVersion,
        };

        // POST requires both dates and an end on/before the tender deadline.
        // PUT must enforce the same business invariant and preserve rejected data.
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateAsync(package.Id, update, _pm.Id));
        var persisted = await _db.DepartmentWorkPackages.AsNoTracking().SingleAsync(item => item.Id == package.Id);
        Assert.Equal(originalStart, persisted.PlannedStart);
        Assert.Equal(originalEnd, persisted.PlannedEnd);
        Assert.Equal(originalHistoryCount, await _db.DepartmentWorkPackageHistory.CountAsync());
    }

    [Theory]
    [InlineData(TenderStatus.Won)]
    [InlineData(TenderStatus.Lost)]
    [InlineData(TenderStatus.Cancelled)]
    public async Task TerminalTender_RejectsReopeningCompletedPlanAndPreservesHistory(TenderStatus terminalStatus)
    {
        var tender = new Tender
        {
            Code = "TD-AUDIT-TERMINAL-001",
            Name = "Goi thau nha may NICON - ket qua cuoi",
            CustomerId = _project.CustomerId,
            OperationalProjectId = _project.Id,
            SubmissionDeadline = DateTime.UtcNow.Date.AddDays(30),
            Status = TenderStatus.Preparing,
        };
        _db.Tenders.Add(tender);
        await _db.SaveChangesAsync();
        var create = ProjectRequest();
        create.Purpose = "TenderPlanning";
        create.SourceTenderId = tender.Id;
        var package = await _service.CreateAsync(create, _pm.Id);
        var started = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = package.Name,
            Status = "Active",
            PlannedStart = package.PlannedStart,
            PlannedEnd = package.PlannedEnd,
            RowVersion = package.RowVersion,
        }, _pm.Id);
        var task = await _service.AddTaskAsync(package.Id,
            TaskRequest(_warehouseStaff.Id, _procurementLead.Id), _procurementLead.Id);
        var completedTask = await _service.UpdateProgressAsync(package.Id, task.Id,
            new UpdateDepartmentWorkTaskProgressRequest
            {
                Status = "Completed",
                ProgressPercent = 100,
                RowVersion = task.RowVersion,
            }, _warehouseStaff.Id);
        await _service.VerifyTaskAsync(package.Id, task.Id,
            new VerifyDepartmentWorkTaskRequest { Confirm = true, RowVersion = completedTask!.RowVersion },
            _procurementLead.Id);
        var closed = await _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
        {
            Name = started!.Name,
            Status = "Completed",
            PlannedStart = started.PlannedStart,
            PlannedEnd = started.PlannedEnd,
            RowVersion = (await _service.GetAsync(package.Id, _pm.Id, canViewAll: true))!.RowVersion,
        }, _pm.Id);
        tender.Status = terminalStatus;
        await _db.SaveChangesAsync();
        var originalHistoryCount = await _db.DepartmentWorkPackageHistory.CountAsync();

        // Nicon-QLVH: a final tender result locks editing. Reopening its plan
        // through the generic work-package endpoint must not bypass that lock.
        await Assert.ThrowsAsync<DepartmentWorkPackageOperationException>(() =>
            _service.UpdateAsync(package.Id, new UpdateDepartmentWorkPackageRequest
            {
                Name = closed!.Name,
                Status = "Active",
                PlannedStart = closed.PlannedStart,
                PlannedEnd = closed.PlannedEnd,
                RowVersion = closed.RowVersion,
            }, _pm.Id));
        Assert.Equal(WorkPackageStatus.Completed,
            (await _db.DepartmentWorkPackages.AsNoTracking().SingleAsync(item => item.Id == package.Id)).Status);
        Assert.Equal(terminalStatus,
            (await _db.Tenders.AsNoTracking().SingleAsync(item => item.Id == tender.Id)).Status);
        Assert.Equal(originalHistoryCount, await _db.DepartmentWorkPackageHistory.CountAsync());
        var readOnly = (await _service.GetAsync(package.Id, _procurementLead.Id, canViewAll: true))!;
        Assert.False(readOnly.CanManagePackage);
        Assert.False(readOnly.CanAssignTasks);
        Assert.All(readOnly.Tasks, item =>
        {
            Assert.False(item.CanUpdateProgress);
            Assert.False(item.CanVerify);
        });
    }

    private CreateDepartmentWorkPackageRequest ProjectRequest() => new()
    {
        Kind = "Project",
        Name = "Chuẩn bị vật tư thi công",
        OperationalProjectId = _project.Id,
        DepartmentRoleGroupId = _procurementGroup.Id,
        LeadUserId = _procurementLead.Id,
        PlannedStart = DateTime.UtcNow.Date,
        PlannedEnd = DateTime.UtcNow.Date.AddDays(14),
    };

    private static UpsertDepartmentWorkTaskRequest TaskRequest(int assigneeId, int approverId, string wbs = "SUP-01") => new()
    {
        WbsCode = wbs,
        Title = "Lập kế hoạch cung ứng vật tư",
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

    private ApplicationUser AddUser(string roleCode, RoleGroup group, bool canAssign = false)
    {
        var role = new Role { Code = roleCode, Name = roleCode, RoleGroupId = group.Id, IsActive = true };
        if (roleCode == "PROCUREMENT" || canAssign)
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
