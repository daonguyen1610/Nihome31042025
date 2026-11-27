using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-113 DesignProject overview slice.
/// Uses InMemory EF (no HTTP) — controller / RBAC coverage lives in
/// the integration suite.
/// </summary>
public class DesignProjectServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DesignProjectService _sut;
    private readonly Mock<IProjectDocumentStagingService> _projectDocuments = new();
    private readonly Mock<IProjectAccessService> _projectAccess = new();
    private readonly int _userId;
    private readonly int _customerId;
    private readonly int _contractId;

    public DesignProjectServiceTests()
    {
        _db = DbContextFactory.Create();
        _projectAccess.Setup(service => service.HasAdministrativeBypassAsync(
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _sut = new DesignProjectService(
            _db,
            NullLogger<DesignProjectService>.Instance,
            _projectAccess.Object,
            new LegacyProjectTeamSyncService(_db));

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000020",
            FullName = "PM Tester",
            Email = "pm.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;

        var customer = new Customer
        {
            Name = "Alpha Corp",
            SourceCode = "referral",
            RelationshipStatus = CustomerRelationshipStatus.InProgress,
            Type = CustomerType.Company,
        };
        _db.Customers.Add(customer);
        _db.SaveChanges();
        _customerId = customer.Id;

        var contract = new Contract
        {
            ContractNumber = "HD-2026-9999",
            CustomerId = _customerId,
            Value = 1000000,
            Status = ContractStatus.InProgress,
        };
        _db.Contracts.Add(contract);
        _db.SaveChanges();
        _contractId = contract.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateDesignProjectRequest ValidCreate(
        string? name = null,
        int? customerId = null,
        int? contractId = null,
        DateTime? start = null,
        DateTime? deadline = null) => new()
        {
            Name = name ?? "Nhà máy Alpha - Giai đoạn 1",
            CustomerId = customerId ?? _customerId,
            ContractId = contractId,
            StartDate = start,
            Deadline = deadline,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_AllowsSeparateDesignFlowsOnSameOperationalProject()
    {
        var operational = new OperationalProject
        {
            Code = "OP-NICON-MULTI-DESIGN",
            Name = "NICON - Nhà xưởng và khu văn phòng",
            CustomerId = _customerId,
        };
        _db.OperationalProjects.Add(operational);
        await _db.SaveChangesAsync();
        var factory = ValidCreate(name: "Thiết kế nhà xưởng");
        factory.OperationalProjectId = operational.Id;
        var office = ValidCreate(name: "Thiết kế khu văn phòng");
        office.OperationalProjectId = operational.Id;

        var first = await _sut.CreateAsync(factory, _userId);
        var second = await _sut.CreateAsync(office, _userId);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.ProjectCode, second.ProjectCode);
        Assert.Equal(2, await _db.DesignProjects.CountAsync(project =>
            project.OperationalProjectId == operational.Id));
        Assert.Equal("Thiết kế nhà xưởng", (await _sut.GetAsync(first.Id))!.Name);
        Assert.Equal("Thiết kế khu văn phòng", (await _sut.GetAsync(second.Id))!.Name);
    }

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesCode()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith($"DP-{DateTime.UtcNow.Year}-", resp.ProjectCode);
        Assert.EndsWith("-0001", resp.ProjectCode);
        Assert.Equal("Concept", resp.CurrentStage);
        Assert.Equal("Active", resp.Status);
    }

    [Fact]
    public async Task CreateAsync_LinkedProject_DualWritesDesignManagerAndLeadRoles()
    {
        var lead = new ApplicationUser
        {
            PhoneNumber = "0900000021",
            FullName = "Design Lead Tester",
            Email = "design.lead.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        var operationalProject = new OperationalProject
        {
            Code = "OP-DESIGN-DUAL-WRITE",
            Name = "Design dual-write project",
            CustomerId = _customerId,
        };
        _db.AddRange(lead, operationalProject);
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.OperationalProjectId = operationalProject.Id;
        request.ProjectManagerUserId = _userId;
        request.DesignLeadUserId = lead.Id;

        await _sut.CreateAsync(request, _userId);

        var members = await _db.OperationalProjectMembers.Include(member => member.Roles).ToListAsync();
        Assert.Contains(members, member => member.UserId == _userId && member.Roles.Any(role =>
            role.RoleCode == ProjectTeamRoleCode.ProjectManager &&
            role.Scope == ProjectRoleScope.Module &&
            role.ScopeValue == "Design" &&
            role.EndedAt == null));
        Assert.Contains(members, member => member.UserId == lead.Id && member.Roles.Any(role =>
            role.RoleCode == ProjectTeamRoleCode.DesignLead &&
            role.Scope == ProjectRoleScope.Module &&
            role.ScopeValue == "Design" &&
            role.EndedAt == null));
    }

    [Fact]
    public async Task CreateAsync_MissingName_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(name: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownCustomer_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(customerId: 99999), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownContract_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(contractId: 99999), _userId));
    }

    [Fact]
    public async Task CreateAsync_PurchaseOrderWithSupplier_CannotCarryTheDesign()
    {
        var purchaseOrder = new Contract
        {
            ContractNumber = "PO-RFQ-2026-0007",
            CustomerId = _customerId,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Supply,
            Value = 250_000_000,
            Status = ContractStatus.Signed,
        };
        _db.Contracts.Add(purchaseOrder);
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(ValidCreate(contractId: purchaseOrder.Id), _userId));

        Assert.Contains("PO-RFQ-2026-0007", error.Message);
        Assert.Empty(await _db.DesignProjects.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_InactiveDesignLead_IsRejectedWithoutMembership()
    {
        var inactiveLead = new ApplicationUser
        {
            PhoneNumber = "0900000022",
            FullName = "Inactive Design Lead",
            Email = "inactive.design.lead@example.com",
            Role = UserRole.USER,
            IsActive = false,
            PasswordHash = "x",
        };
        _db.Users.Add(inactiveLead);
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.DesignLeadUserId = inactiveLead.Id;

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(request, _userId));

        Assert.Empty(await _db.OperationalProjectMembers.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_DeadlineBeforeStart_Throws()
    {
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CreateAsync(
                ValidCreate(start: DateTime.UtcNow, deadline: DateTime.UtcNow.AddDays(-1)),
                _userId));
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerYear()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(name: "Villa Bãi Dài"), _userId);
        Assert.EndsWith("-0001", a.ProjectCode);
        Assert.EndsWith("-0002", b.ProjectCode);
    }

    [Fact]
    public async Task CreateAsync_AfterSequenceGap_AllocatesAfterHighestCode()
    {
        var year = DateTime.UtcNow.Year;
        _db.DesignProjects.AddRange(
            new DesignProject
            {
                ProjectCode = $"DP-{year}-0001",
                Name = "Existing first project",
                CustomerId = _customerId,
            },
            new DesignProject
            {
                ProjectCode = $"DP-{year}-0003",
                Name = "Existing third project",
                CustomerId = _customerId,
            });
        await _db.SaveChangesAsync();

        var created = await _sut.CreateAsync(ValidCreate(name: "Project after gap"), _userId);

        Assert.EndsWith("-0004", created.ProjectCode);
    }

    // ---------------- Get / List ----------------

    [Fact]
    public async Task GetAsync_UnknownReturnsNull()
    {
        Assert.Null(await _sut.GetAsync(99999));
    }

    [Fact]
    public async Task GetAsync_HydratesCustomerAndContractNames()
    {
        var created = await _sut.CreateAsync(ValidCreate(contractId: _contractId), _userId);
        var got = await _sut.GetAsync(created.Id);
        Assert.NotNull(got);
        Assert.Equal("Alpha Corp", got!.CustomerName);
        Assert.Equal("HD-2026-9999", got.ContractNumber);
    }

    [Fact]
    public async Task ListAsync_FiltersByStage()
    {
        var a = await _sut.CreateAsync(ValidCreate(name: "Row A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(name: "Row B"), _userId);
        var entity = await _db.DesignProjects.FindAsync(b.Id);
        entity!.CurrentStage = DesignProjectStage.BasicDesign;
        await _db.SaveChangesAsync();

        var basic = await _sut.ListAsync(new DesignProjectListParams { Stage = "BasicDesign" }, _userId);
        Assert.Single(basic.Items);
        Assert.Equal(b.Id, basic.Items[0].Id);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_UnknownReturnsNull()
    {
        var req = new UpdateDesignProjectRequest
        {
            Name = "X",
            CustomerId = _customerId,
        };
        Assert.Null(await _sut.UpdateAsync(99999, req, _userId));
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeStage()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var req = new UpdateDesignProjectRequest
        {
            Name = created.Name,
            CustomerId = _customerId,
            Status = "OnHold",
        };
        var updated = await _sut.UpdateAsync(created.Id, req, _userId);
        Assert.Equal("Concept", updated!.CurrentStage);
    }

    [Fact]
    public async Task UpdateAsync_ProjectChange_IsRejected()
    {
        var oldProject = new OperationalProject { Code = "OP-D-OLD", Name = "Old", CustomerId = _customerId };
        var newProject = new OperationalProject { Code = "OP-D-NEW", Name = "New", CustomerId = _customerId };
        _db.OperationalProjects.AddRange(oldProject, newProject);
        await _db.SaveChangesAsync();
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var design = await _db.DesignProjects.FindAsync(created.Id);
        design!.OperationalProjectId = oldProject.Id;
        var category = new AsBuiltDocumentCategory
        {
            Code = "move",
            Name = "Move",
            NameVi = "Move",
            NameEn = "Move",
            NameZh = "Move",
            NameJa = "Move",
            IsActive = true,
        };
        _db.AsBuiltDocumentCategories.Add(category);
        await _db.SaveChangesAsync();
        _db.AddRange(
            new BasicDesignDoc
            {
                DesignProjectId = created.Id,
                DocumentCode = "BD-MOVE",
                Title = "Basic",
                DisciplineCode = "architecture",
                FilePath = "/files/design/basic.pdf",
                OriginalFileName = "basic.pdf",
            },
            new ShopDrawing
            {
                DesignProjectId = created.Id,
                DrawingCode = "SD-MOVE",
                Title = "Shop",
                ConstructionItem = "Item",
                DisciplineCode = "architecture",
                FilePath = "/files/design/shop.pdf",
                OriginalFileName = "shop.pdf",
            },
            new PermitChecklistItem
            {
                DesignProjectId = created.Id,
                PermitTypeCode = "gpxd",
                SubmittedFilePath = "/files/business-documents/permits/submitted.pdf",
                IssuedFilePath = "/files/business-documents/permits/issued.pdf",
            },
            new AcceptanceRecord
            {
                DesignProjectId = created.Id,
                AcceptanceCode = "AR-MOVE",
                Title = "Acceptance",
                AcceptanceDate = new DateOnly(2026, 8, 31),
                Documents = "[\"/files/business-documents/acceptance/acceptance.pdf\"]",
            },
            new AsBuiltDocument
            {
                DesignProjectId = created.Id,
                CategoryId = category.Id,
                DocumentCode = "AB-MOVE",
                Title = "As built",
                FileUrl = "/files/business-documents/as-built/as-built.pdf",
            },
            new HandoverRecord
            {
                DesignProjectId = created.Id,
                HandoverCode = "HR-MOVE",
                Title = "Handover",
                PlannedHandoverDate = new DateOnly(2026, 9, 1),
                ResponsibleUserId = _userId,
                Documents = "[\"/files/business-documents/handover/handover.pdf\"]",
            });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.UpdateAsync(created.Id, new UpdateDesignProjectRequest
            {
                Name = created.Name,
                CustomerId = _customerId,
                OperationalProjectId = newProject.Id,
            }, _userId));
        _projectDocuments.Verify(staging => staging.StageExistingManagedFilesMoveAsync(
            It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<IReadOnlyCollection<ProjectDocumentMoveDescriptor>>(),
            It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- Close ----------------

    [Fact]
    public async Task CloseAsync_KeepsTheDossierAndRecordsWhoAndWhy()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);

        var closed = await _sut.CloseAsync(created.Id, "  Khách dừng dự án  ", _userId);

        Assert.NotNull(closed);
        Assert.Equal("Cancelled", closed.Status);
        Assert.Equal("Khách dừng dự án", closed.ClosedReason);
        Assert.NotNull(closed.ClosedAt);
        var stored = Assert.Single(_db.DesignProjects.Where(item => item.Id == created.Id));
        Assert.Equal(_userId, stored.ClosedByUserId);
        Assert.Equal(created.CurrentStage, stored.CurrentStage.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CloseAsync_RequiresAReason(string reason)
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CloseAsync(created.Id, reason, _userId));

        Assert.Equal(DesignProjectStatus.Active, _db.DesignProjects.Single(item => item.Id == created.Id).Status);
    }

    [Fact]
    public async Task EnsureForOpportunityAsync_StartsAFreshDossierWhenTheEarlierOneWasClosed()
    {
        var operational = new OperationalProject
        {
            Code = "OP-NICON-REBID",
            Name = "NICON - Chào lại nhà xưởng",
            CustomerId = _customerId,
        };
        _db.OperationalProjects.Add(operational);
        await _db.SaveChangesAsync();
        var request = ValidCreate();
        request.OperationalProjectId = operational.Id;
        var closed = await _sut.CreateAsync(request, _userId);
        await _sut.CloseAsync(closed.Id, "Trượt thầu", _userId);
        var opportunity = new Opportunity
        {
            Name = "Chào lại nhà xưởng",
            CustomerId = request.CustomerId,
            OperationalProjectId = operational.Id,
        };
        _db.Opportunities.Add(opportunity);
        await _db.SaveChangesAsync();

        var result = await _sut.EnsureForOpportunityAsync(opportunity.Id, _userId, "Hồ sơ chào lại", null);

        Assert.True(result.Created);
        Assert.NotEqual(closed.Id, result.DesignProject.Id);
    }

    [Fact]
    public async Task CloseAsync_RefusesADossierThatAlreadyEnded()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.CloseAsync(created.Id, "Trượt thầu", _userId);

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.CloseAsync(created.Id, "Lần hai", _userId));
    }

    [Fact]
    public async Task UpdateAsync_CannotCloseWithoutReason_AndCannotEditAClosedDossier()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var source = ValidCreate();
        var update = new UpdateDesignProjectRequest
        {
            Name = "Đổi tên",
            OperationalProjectId = source.OperationalProjectId,
            CustomerId = source.CustomerId,
            ContractId = source.ContractId,
            Status = "Cancelled",
        };

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.UpdateAsync(created.Id, update, _userId));
        await _sut.CloseAsync(created.Id, "Khách dừng dự án", _userId);
        update.Status = "Active";

        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.UpdateAsync(created.Id, update, _userId));
    }

    [Fact]
    public async Task EnsureForContractAsync_CreatesRowFirstTime()
    {
        var contract = await _db.Contracts.FirstAsync(c => c.Id == _contractId);
        var dp = await _sut.EnsureForContractAsync(contract, _userId);
        Assert.NotNull(dp);
        Assert.Equal(_contractId, dp.ContractId);
        Assert.StartsWith("DP-", dp.ProjectCode);
    }

    [Fact]
    public async Task EnsureForContractAsync_Idempotent()
    {
        var contract = await _db.Contracts.FirstAsync(c => c.Id == _contractId);
        var first = await _sut.EnsureForContractAsync(contract, _userId);
        var second = await _sut.EnsureForContractAsync(contract, _userId);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _db.DesignProjects.CountAsync(dp => dp.ContractId == _contractId));
    }

    [Fact]
    public async Task EnsureForContractAsync_ReusesProjectStartedBeforeContract()
    {
        var operationalProject = new OperationalProject
        {
            Code = "OP-DESIGN-BEFORE-CONTRACT",
            Name = "Design before contract",
            CustomerId = _customerId,
        };
        _db.OperationalProjects.Add(operationalProject);
        await _db.SaveChangesAsync();
        var contract = await _db.Contracts.SingleAsync(item => item.Id == _contractId);
        contract.OperationalProjectId = operationalProject.Id;
        var designProject = new DesignProject
        {
            ProjectCode = "DP-PRECONTRACT",
            Name = "Concept",
            CustomerId = _customerId,
            OperationalProjectId = operationalProject.Id,
            CurrentStage = DesignProjectStage.Concept,
            Status = DesignProjectStatus.Active,
        };
        _db.DesignProjects.Add(designProject);
        await _db.SaveChangesAsync();

        var result = await _sut.EnsureForContractAsync(contract, _userId);

        Assert.Equal(designProject.Id, result.Id);
        Assert.Single(_db.DesignProjects);
        Assert.Null(designProject.ContractId);
    }

    /// <summary>
    /// Minimal permit service stub used by legacy design-project test setup.
    /// </summary>
    private sealed class NoopPermitChecklistService : IPermitChecklistService
    {
        public Task EnsureForProjectAsync(int designProjectId, int? callerUserId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistListResponse> ListAsync(
            NihomeBackend.Models.DTOs.Requests.PermitChecklistListParams parameters,
            CancellationToken ct = default,
            IReadOnlySet<int>? accessibleDesignProjectIds = null)
            => Task.FromResult(new NihomeBackend.Models.DTOs.Responses.PermitChecklistListResponse());

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default)
            => Task.FromResult<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?>(null);

        public Task<IReadOnlyList<NihomeBackend.Models.DTOs.Responses.PermitChecklistHistoryResponse>> HistoryAsync(
            int id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NihomeBackend.Models.DTOs.Responses.PermitChecklistHistoryResponse>>([]);

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse> CreateAsync(
            NihomeBackend.Models.DTOs.Requests.CreatePermitChecklistItemRequest request, int callerUserId, CancellationToken ct = default)
            => Task.FromResult(new NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse());

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?> UpdateAsync(
            int id, NihomeBackend.Models.DTOs.Requests.UpdatePermitChecklistItemRequest request, int callerUserId, CancellationToken ct = default)
            => Task.FromResult<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?>(null);

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?> UploadDocumentAsync(
            int id, PermitDocumentKind kind, Microsoft.AspNetCore.Http.IFormFile? file, int callerUserId, CancellationToken ct = default)
            => Task.FromResult<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?>(null);

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?> DeleteAsync(int id, CancellationToken ct = default)
            => Task.FromResult<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?>(null);
    }
}
