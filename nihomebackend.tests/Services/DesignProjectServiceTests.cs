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
        var hardDelete = HardDeleteTestServices.Create(_db, _projectDocuments.Object);
        _sut = new DesignProjectService(
            _db,
            new NoopPermitChecklistService(),
            NullLogger<DesignProjectService>.Instance,
            _projectAccess.Object,
            new LegacyProjectTeamSyncService(_db),
            hardDelete.Plans,
            hardDelete.Operations);

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

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_ConceptStage_Deletes()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var impact = await _sut.GetDeletionImpactAsync(created.Id);
        var removed = await _sut.DeleteAsync(created.Id, Confirm(impact!), _userId);
        Assert.True(removed!.IsComplete);
        Assert.Null(await _sut.GetAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_BeyondConcept_DeletesAggregateAndStagesManagedFiles()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var operationalProject = new OperationalProject
        {
            Code = "OP-AGGREGATE",
            Name = "Aggregate delete project",
            CustomerId = _customerId,
        };
        _db.OperationalProjects.Add(operationalProject);
        await _db.SaveChangesAsync();
        var createdEntity = await _db.DesignProjects.FindAsync(created.Id);
        createdEntity!.OperationalProjectId = operationalProject.Id;
        await _db.SaveChangesAsync();
        createdEntity.CurrentStage = DesignProjectStage.BasicDesign;
        await _db.SaveChangesAsync();
        var basicDoc = new BasicDesignDoc
        {
            DesignProjectId = created.Id,
            DisciplineCode = "architecture",
            DocumentCode = "BD-DELETE-001",
            Title = "Basic cleanup",
            FilePath = "/files/design/basic/aggregate-basic.pdf",
        };
        var shopDrawing = new ShopDrawing
        {
            DesignProjectId = created.Id,
            DisciplineCode = "architecture",
            ConstructionItem = "Cleanup",
            DrawingCode = "SD-DELETE-001",
            Title = "Shop cleanup",
            FilePath = "/files/design/shop/aggregate-shop.pdf",
        };
        var permit = new PermitChecklistItem
        {
            DesignProjectId = created.Id,
            PermitTypeCode = "gpxd",
            SubmittedFilePath = "/files/business-documents/permits/aggregate-submitted.pdf",
            IssuedFilePath = "/files/business-documents/permits/aggregate-issued.pdf",
        };
        var predecessor = new ConstructionTask
        {
            DesignProjectId = created.Id,
            TaskCode = "T-DELETE-001",
            Name = "Predecessor",
            PlannedStart = new DateOnly(2026, 8, 1),
            PlannedEnd = new DateOnly(2026, 8, 2),
        };
        var successor = new ConstructionTask
        {
            DesignProjectId = created.Id,
            TaskCode = "T-DELETE-002",
            Name = "Successor",
            PlannedStart = new DateOnly(2026, 8, 3),
            PlannedEnd = new DateOnly(2026, 8, 4),
        };
        _db.AddRange(basicDoc, shopDrawing, permit, predecessor, successor);
        await _db.SaveChangesAsync();

        var release = new IfcRelease
        {
            DesignProjectId = created.Id,
            ReleaseNumber = "IFC-DELETE-001",
            Title = "Release cleanup",
        };
        var asBuiltCategory = new AsBuiltDocumentCategory
        {
            Code = "aggregate-delete",
            Name = "Aggregate delete",
            NameVi = "Aggregate delete",
            NameEn = "Aggregate delete",
            NameZh = "Aggregate delete",
            NameJa = "Aggregate delete",
            IsActive = true,
        };
        _db.AsBuiltDocumentCategories.Add(asBuiltCategory);
        await _db.SaveChangesAsync();
        var acceptance = new AcceptanceRecord
        {
            DesignProjectId = created.Id,
            ConstructionTaskId = successor.Id,
            AcceptanceCode = "A-DELETE-001",
            Title = "Acceptance blocker",
            AcceptanceDate = new DateOnly(2026, 8, 5),
            Documents = "[\"/files/business-documents/acceptance/aggregate.pdf\"]",
        };
        var asBuilt = new AsBuiltDocument
        {
            DesignProjectId = created.Id,
            CategoryId = asBuiltCategory.Id,
            DocumentCode = "AB-DELETE-001",
            Title = "As-built blocker",
            FileUrl = "/files/business-documents/as-built/aggregate.pdf",
        };
        var handover = new HandoverRecord
        {
            DesignProjectId = created.Id,
            HandoverCode = "H-DELETE-001",
            Title = "Handover blocker",
            PlannedHandoverDate = new DateOnly(2026, 8, 6),
            ResponsibleUserId = _userId,
            Documents = "[\"/files/business-documents/handover/aggregate.pdf\"]",
            CreatedByUserId = _userId,
            UpdatedByUserId = _userId,
        };
        _db.AddRange(
            new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.BasicDesignDoc,
                TargetId = basicDoc.Id,
                RevisionNumber = 1,
                ReasonCode = "client-change",
                Note = "Basic revision",
                IsCurrent = true,
                CreatedByUserId = _userId,
            },
            new DrawingRevision
            {
                TargetType = DrawingRevisionTargetType.ShopDrawing,
                TargetId = shopDrawing.Id,
                RevisionNumber = 1,
                ReasonCode = "client-change",
                Note = "Shop revision",
                IsCurrent = true,
                CreatedByUserId = _userId,
            },
            new ConstructionTaskDependency
            {
                TaskId = successor.Id,
                PredecessorTaskId = predecessor.Id,
            },
            acceptance,
            asBuilt,
            handover,
            release);
        await _db.SaveChangesAsync();
        _db.IfcReleaseItems.Add(new IfcReleaseItem
        {
            IfcReleaseId = release.Id,
            ShopDrawingId = shopDrawing.Id,
        });
        _db.ProjectDocuments.AddRange(
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Design,
                nameof(BasicDesignDoc), "file", basicDoc.Id, basicDoc.FilePath!),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Design,
                nameof(ShopDrawing), "file", shopDrawing.Id, shopDrawing.FilePath!),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Design,
                nameof(PermitChecklistItem), "submittedPackage", permit.Id, permit.SubmittedFilePath!),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Design,
                nameof(PermitChecklistItem), "issuedPermit", permit.Id, permit.IssuedFilePath!),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Acceptance,
                nameof(AcceptanceRecord), "documents", acceptance.Id,
                "/files/business-documents/acceptance/aggregate.pdf"),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Acceptance,
                nameof(AsBuiltDocument), "file", asBuilt.Id, asBuilt.FileUrl!),
            Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Handover,
                nameof(HandoverRecord), "documents", handover.Id,
                "/files/business-documents/handover/aggregate.pdf"));
        await _db.SaveChangesAsync();
        _projectDocuments.Setup(staging => staging.StageExistingManagedFileDeleteAsync(
                It.IsAny<int>(), It.IsAny<ProjectDocumentSourceModule>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var impact = await _sut.GetDeletionImpactAsync(created.Id);
        var removed = await _sut.DeleteAsync(created.Id, Confirm(impact!), _userId);

        Assert.True(removed!.IsComplete);
        Assert.Null(await _db.DesignProjects.FindAsync(created.Id));
        Assert.Empty(await _db.DrawingRevisions.ToListAsync());
        Assert.Empty(await _db.AcceptanceRecords.ToListAsync());
        Assert.Empty(await _db.HandoverRecords.ToListAsync());
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            It.IsAny<int>(), It.IsAny<ProjectDocumentSourceModule>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ProjectDocument Sidecar(
        int operationalProjectId,
        ProjectDocumentSourceModule sourceModule,
        string sourceEntityType,
        string sourceSlot,
        long sourceRecordId,
        string localPath) => new()
        {
            OperationalProjectId = operationalProjectId,
            Category = ProjectDocumentCategory.Unclassified,
            SourceModule = sourceModule,
            SourceType = ProjectDocumentSourceType.ExistingManagedFile,
            SourceEntityType = sourceEntityType,
            SourceSlot = sourceSlot,
            SourceRecordId = sourceRecordId,
            LocalPath = localPath,
            OriginalFileName = Path.GetFileName(localPath),
            Sha256 = new string('a', 64),
            SyncStatus = ProjectDocumentSyncStatus.Deleted,
        };

    [Fact]
    public async Task DeleteAsync_WhenFileStagingFails_PreservesAggregateAndTranslations()
    {
        var created = await _sut.CreateAsync(ValidCreate("Failed staging"), _userId);
        var operationalProject = new OperationalProject
        {
            Code = "OP-STAGING-FAILURE",
            Name = "Staging failure project",
            CustomerId = _customerId,
        };
        _db.OperationalProjects.Add(operationalProject);
        await _db.SaveChangesAsync();
        var project = await _db.DesignProjects.FindAsync(created.Id);
        project!.OperationalProjectId = operationalProject.Id;
        var document = new BasicDesignDoc
        {
            DesignProjectId = created.Id,
            DisciplineCode = "architecture",
            DocumentCode = "BD-STAGING-FAILURE",
            Title = "Failed staging document",
            FilePath = "/images/unmanaged/staging-failure.pdf",
        };
        _db.BasicDesignDocs.Add(document);
        await _db.SaveChangesAsync();
        _db.ProjectDocuments.Add(Sidecar(operationalProject.Id, ProjectDocumentSourceModule.Design,
            nameof(BasicDesignDoc), "file", document.Id, document.FilePath));
        _db.EntityTranslations.Add(new EntityTranslation
        {
            EntityType = NihomeBackend.Constants.EntityTypes.BasicDesignDoc,
            EntityId = document.Id,
            FieldName = "Title",
            LanguageCode = "en",
            Value = "Failed staging document",
        });
        await _db.SaveChangesAsync();
        _projectDocuments.Setup(staging => staging.StageExistingManagedFileDeleteAsync(
                operationalProject.Id, ProjectDocumentSourceModule.Design, nameof(BasicDesignDoc),
                "file", document.Id, document.FilePath, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var impact = await _sut.GetDeletionImpactAsync(created.Id);

        Assert.False(impact!.CanDelete);
        await Assert.ThrowsAsync<DesignProjectOperationException>(() =>
            _sut.DeleteAsync(created.Id, Confirm(impact!), _userId));

        Assert.NotNull(await _db.DesignProjects.FindAsync(created.Id));
        Assert.NotNull(await _db.BasicDesignDocs.FindAsync(document.Id));
        Assert.NotNull(await _db.EntityTranslations.SingleOrDefaultAsync(item =>
            item.EntityType == NihomeBackend.Constants.EntityTypes.BasicDesignDoc && item.EntityId == document.Id));
    }

    private static ConfirmDeletionRequest Confirm(
        NihomeBackend.Models.DTOs.Responses.DeletionImpactResponse impact) => new()
        {
            PlanToken = impact.PlanToken,
            Confirmation = impact.RequiredConfirmation,
        };

    // ---------------- Auto-create hook ----------------

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

    /// <summary>
    /// Stub for the NIH-137 permit checklist hook. Real coverage lives in
    /// <c>PermitChecklistServiceTests</c>; here we only need the create /
    /// auto-create paths to not blow up.
    /// </summary>
    private sealed class NoopPermitChecklistService : IPermitChecklistService
    {
        public Task EnsureForProjectAsync(int designProjectId, int? callerUserId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistListResponse> ListAsync(
            NihomeBackend.Models.DTOs.Requests.PermitChecklistListParams parameters, CancellationToken ct = default)
            => Task.FromResult(new NihomeBackend.Models.DTOs.Responses.PermitChecklistListResponse());

        public Task<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?> GetAsync(int id, CancellationToken ct = default)
            => Task.FromResult<NihomeBackend.Models.DTOs.Responses.PermitChecklistItemResponse?>(null);

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
