using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class BusinessRootHardDeleteHandlerTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();

    [Fact]
    public async Task CanAccessSurveyAsync_UsesNormalManagementScope()
    {
        var customer = new Customer { Name = "Survey scope customer" };
        var project = new OperationalProject
        {
            Code = "PJ-SURVEY-SCOPE",
            Name = "Survey scope project",
            Customer = customer,
            ProjectManagerUserId = 31,
            CreatedByUserId = 32,
        };
        var survey = new Survey
        {
            Code = "SV-SCOPE",
            Location = "Scope site",
            SurveyDate = DateTime.UtcNow,
            OperationalProject = project,
            SurveyorUserId = 11,
            CreatedByUserId = 12,
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        var settings = new Mock<IGoogleDriveSettingsStore>();
        settings.Setup(item => item.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions { InstanceId = "scope-instance" });
        var files = new Mock<IHardDeleteFileService>();
        var service = new BusinessRootHardDeletePlanService(db, settings.Object, files.Object);

        Assert.True(await service.CanAccessSurveyAsync(survey.Id, 11, false));
        Assert.True(await service.CanAccessSurveyAsync(survey.Id, 12, false));
        Assert.True(await service.CanAccessSurveyAsync(survey.Id, 31, false));
        Assert.True(await service.CanAccessSurveyAsync(survey.Id, 32, false));
        Assert.False(await service.CanAccessSurveyAsync(survey.Id, 99, false));
        Assert.True(await service.CanAccessSurveyAsync(survey.Id, 99, true));
    }

    [Fact]
    public async Task AuthorizeAsync_RevalidatesPlanBeforeExternalEffects()
    {
        var opportunity = new Opportunity { Name = "Plan changed", OwnerUserId = 7 };
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync();
        var plans = new Mock<IBusinessRootHardDeletePlanService>();
        plans.Setup(item => item.ForOpportunityAsync(opportunity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Plan(EntityTypes.Opportunity, opportunity.Id, "current-plan"));
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(7, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new OpportunityHardDeleteHandler(db, plans.Object, permissions.Object);
        var context = Context(EntityTypes.Opportunity, opportunity.Id, 7, "preview-plan");

        await Assert.ThrowsAsync<DeletionPlanChangedException>(() => handler.AuthorizeAsync(context));
        plans.Verify(item => item.ForOpportunityAsync(opportunity.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_MissingRootBeforeDatabaseFinalization_IsRejectedDuringForwardRecovery()
    {
        var plans = new Mock<IBusinessRootHardDeletePlanService>();
        plans.Setup(item => item.ForOpportunityAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BusinessRootHardDeletePlan?)null);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(7, "crm.opportunities.manage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new OpportunityHardDeleteHandler(db, plans.Object, permissions.Object);
        var context = Context(EntityTypes.Opportunity, 42, 7, "preview-plan");

        var exception = await Assert.ThrowsAsync<HardDeleteAuthorizationException>(
            () => handler.AuthorizeAsync(context));

        Assert.Equal("hard_delete_authorization_changed", exception.Code);
    }

    [Fact]
    public async Task ContractFinalize_TerminalizesSidecarAndCreatesSeedTombstone()
    {
        var customer = new Customer { Name = "Contract finalizer customer" };
        var project = new OperationalProject
        {
            Code = "PJ-CONTRACT-FINALIZER",
            Name = "Contract finalizer project",
            Customer = customer,
        };
        var contract = new Contract
        {
            ContractNumber = "HD-SAMPLE-999",
            Customer = customer,
            OperationalProject = project,
        };
        db.AddRange(project, contract);
        await db.SaveChangesAsync();
        var attachment = new ContractAttachment
        {
            ContractId = contract.Id,
            FilePath = $"/files/contracts/{contract.Id}/finalize.pdf",
            OriginalFileName = "finalize.pdf",
            ContentType = "application/pdf",
        };
        db.ContractAttachments.Add(attachment);
        await db.SaveChangesAsync();
        var sidecar = new ProjectDocument
        {
            OperationalProjectId = project.Id,
            SourceModule = ProjectDocumentSourceModule.Crm,
            SourceType = ProjectDocumentSourceType.ExistingManagedFile,
            SourceEntityType = nameof(ContractAttachment),
            SourceSlot = "file",
            SourceRecordId = attachment.Id,
            LocalPath = attachment.FilePath,
            OriginalFileName = attachment.OriginalFileName,
            Origin = ProjectDocumentOrigin.Nicon,
            DesiredOperation = ProjectDocumentDesiredOperation.None,
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            DriveFileId = "deleted-drive-file",
            DriveFolderId = "drive-parent",
            DriveVersion = "v1",
            DriveModifiedAt = DateTime.UtcNow,
            Generation = 3,
        };
        db.ProjectDocuments.Add(sidecar);
        await db.SaveChangesAsync();
        var plans = new Mock<IBusinessRootHardDeletePlanService>();
        plans.SetupSequence(item => item.ForContractAsync(contract.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Plan(EntityTypes.Contract, contract.Id, "contract-plan"))
            .ReturnsAsync((BusinessRootHardDeletePlan?)null);
        var handler = new ContractHardDeleteHandler(db, plans.Object, Mock.Of<IPermissionService>());
        var context = Context(EntityTypes.Contract, contract.Id, 42, "contract-plan");

        await handler.FinalizeAsync(context);
        await handler.FinalizeAsync(context);

        Assert.False(await db.Contracts.AnyAsync(item => item.Id == contract.Id));
        var retainedSidecar = await db.ProjectDocuments.SingleAsync(item => item.Id == sidecar.Id);
        Assert.Equal(ProjectDocumentSyncStatus.Deleted, retainedSidecar.SyncStatus);
        Assert.Equal(ProjectDocumentDesiredOperation.None, retainedSidecar.DesiredOperation);
        Assert.Null(retainedSidecar.DriveFileId);
        Assert.Null(retainedSidecar.DriveVersion);
        Assert.Null(retainedSidecar.DriveModifiedAt);
        Assert.Equal(42, retainedSidecar.DeletedByUserId);
        Assert.Single(db.SeededRootDeletions, item =>
            item.ResourceType == EntityTypes.Contract && item.ResourceKey == contract.ContractNumber);
        Assert.DoesNotContain(db.AuditLogs, item => item.AuditId == context.OperationId.ToString("N"));
    }

    [Fact]
    public async Task Finalizers_CreateTombstonesForExactOpportunitySurveyAndCapabilitySampleIdentities()
    {
        var customer = new Customer { Name = "Tombstone customer" };
        var project = new OperationalProject
        {
            Code = "PJ-TOMBSTONES",
            Name = "Tombstone project",
            Customer = customer,
        };
        var opportunity = new Opportunity { Name = "[SAMPLE] Tombstone opportunity", Customer = customer };
        var survey = new Survey
        {
            Code = "SV-SAMPLE-999",
            Location = "Tombstone site",
            SurveyDate = DateTime.UtcNow,
            OperationalProject = project,
        };
        var capability = new CapabilityDocument
        {
            Name = "Tombstone capability",
            TagCode = "iso",
            Description = "[SAMPLE_CAP] Tombstone capability",
            FilePath = "/files/capability/tombstone.pdf",
            OriginalFileName = "tombstone.pdf",
            ContentType = "application/pdf",
        };
        db.AddRange(project, opportunity, survey, capability);
        await db.SaveChangesAsync();
        var plans = new Mock<IBusinessRootHardDeletePlanService>();
        plans.Setup(item => item.ForOpportunityAsync(opportunity.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Plan(EntityTypes.Opportunity, opportunity.Id, "opportunity-plan"));
        plans.Setup(item => item.ForSurveyAsync(survey.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Plan(EntityTypes.Survey, survey.Id, "survey-plan"));
        plans.Setup(item => item.ForCapabilityAsync(capability.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Plan(EntityTypes.CapabilityDocument, capability.Id, "capability-plan"));
        var permissions = Mock.Of<IPermissionService>();

        await new OpportunityHardDeleteHandler(db, plans.Object, permissions)
            .FinalizeAsync(Context(EntityTypes.Opportunity, opportunity.Id, 42, "opportunity-plan"));
        await new SurveyHardDeleteHandler(db, plans.Object, permissions)
            .FinalizeAsync(Context(EntityTypes.Survey, survey.Id, 42, "survey-plan"));
        await new CapabilityDocumentHardDeleteHandler(db, plans.Object, permissions)
            .FinalizeAsync(Context(EntityTypes.CapabilityDocument, capability.Id, 42, "capability-plan"));

        Assert.Contains(db.SeededRootDeletions, item =>
            item.ResourceType == EntityTypes.Opportunity && item.ResourceKey == opportunity.Name);
        Assert.Contains(db.SeededRootDeletions, item =>
            item.ResourceType == EntityTypes.Survey && item.ResourceKey == survey.Code);
        Assert.Contains(db.SeededRootDeletions, item =>
            item.ResourceType == EntityTypes.CapabilityDocument && item.ResourceKey == capability.FilePath);
    }

    private static BusinessRootHardDeletePlan Plan(string resourceType, int id, string token) =>
        new(new DeletionImpactResponse
        {
            ResourceType = resourceType,
            ResourceId = id,
            ResourceLabel = resourceType,
            RequiredConfirmation = resourceType,
            PlanToken = token,
            CanDelete = true,
        }, []);

    private static HardDeleteResourceContext Context(
        string resourceType, int id, int requestedBy, string planToken) =>
        new(Guid.NewGuid(), resourceType, id.ToString(), planToken, requestedBy.ToString(),
            $"delete-{resourceType.ToLowerInvariant()}-aggregate", true);

    public void Dispose() => db.Dispose();
}