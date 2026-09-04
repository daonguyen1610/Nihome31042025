using Microsoft.EntityFrameworkCore;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class BusinessRootHardDeletePlanServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IHardDeleteFileService> files = new();
    private readonly Mock<IGoogleDriveSettingsStore> driveSettings = new();
    private readonly BusinessRootHardDeletePlanService service;

    public BusinessRootHardDeletePlanServiceTests()
    {
        files.Setup(item => item.ValidateManagedPath(It.IsAny<string>()))
            .Returns((string path) => path.StartsWith("/files/", StringComparison.Ordinal)
                ? path
                : throw new HardDeleteFileException("invalid_managed_path", "invalid"));
        driveSettings.Setup(item => item.GetRuntimeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleDriveOptions { InstanceId = "test-instance" });
        service = new BusinessRootHardDeletePlanService(db, driveSettings.Object, files.Object);
    }

    [Fact]
    public async Task ForOpportunityAsync_QuoteBlocksWhileIndependentDependenciesUnlinkWithDetailLinks()
    {
        var customer = new Customer { Name = "Opportunity customer" };
        var opportunity = new Opportunity { Name = "Delete opportunity", Customer = customer };
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync();
        var quote = new Quote { Code = "QT-BLOCK", OpportunityId = opportunity.Id };
        var contract = new Contract
        {
            ContractNumber = "HD-UNLINK",
            CustomerId = customer.Id,
            OpportunityId = opportunity.Id,
        };
        var survey = new Survey
        {
            Code = "SV-UNLINK",
            Location = "Site",
            SurveyDate = DateTime.UtcNow,
            LinkedOpportunityId = opportunity.Id,
        };
        var lead = new Lead
        {
            Name = "Lead to unlink",
            Phone = "0900000000",
            SourceCode = "marketing",
            ConvertedOpportunityId = opportunity.Id,
        };
        var tender = new Tender
        {
            Code = "TD-UNLINK",
            Name = "Tender to unlink",
            CustomerId = customer.Id,
            SubmissionDeadline = DateTime.UtcNow.AddDays(1),
            WonOpportunityId = opportunity.Id,
        };
        db.AddRange(quote, contract, survey, lead, tender);
        await db.SaveChangesAsync();

        var plan = (await service.ForOpportunityAsync(opportunity.Id))!;

        Assert.False(plan.Impact.CanDelete);
        AssertImpact(plan, "opportunity.quotes", DeletionImpactActions.Block,
            quote.Id, $"/admin/quotes/{quote.Id}");
        AssertImpact(plan, "opportunity.contracts", DeletionImpactActions.Unlink,
            contract.Id, $"/admin/contracts/{contract.Id}");
        AssertImpact(plan, "opportunity.surveys", DeletionImpactActions.Unlink,
            survey.Id, $"/admin/surveys/{survey.Id}");
        AssertImpact(plan, "opportunity.convertedLeads", DeletionImpactActions.Unlink,
            lead.Id, $"/admin/leads/{lead.Id}");
        AssertImpact(plan, "opportunity.winningTenders", DeletionImpactActions.Unlink,
            tender.Id, $"/admin/tenders/{tender.Id}");
    }

    [Fact]
    public async Task ForSurveyAsync_MediaBlockersLinkDirectlyToSurveyAndMedia()
    {
        var survey = new Survey
        {
            Code = "SV-MEDIA",
            Location = "Media site",
            SurveyDate = DateTime.UtcNow,
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        var media = new SurveyMedia
        {
            SurveyId = survey.Id,
            OriginalFileName = "site-photo.jpg",
            StoredFileName = "stored.jpg",
            ContentType = "image/jpeg",
            Extension = ".jpg",
            Size = 10,
            RelativePath = $"/files/survey-media/{survey.Id}/stored.jpg",
        };
        db.SurveyMedia.Add(media);
        await db.SaveChangesAsync();

        var plan = (await service.ForSurveyAsync(survey.Id))!;

        Assert.False(plan.Impact.CanDelete);
        var impact = Assert.Single(plan.Impact.Items, item => item.Key == "survey.media");
        Assert.Equal(DeletionImpactActions.Block, impact.Action);
        Assert.Equal($"/admin/surveys/{survey.Id}", impact.ResolutionUrl);
        var link = Assert.Single(impact.ResolutionLinks);
        Assert.Equal(media.OriginalFileName, link.Label);
        Assert.Equal($"/admin/surveys/{survey.Id}?mediaId={media.Id}", link.Url);
    }

    [Fact]
    public async Task ForSurveyAsync_DriveFolderRebindingChangesPlanToken()
    {
        var survey = new Survey
        {
            Code = "SV-FOLDER",
            Location = "Folder site",
            SurveyDate = DateTime.UtcNow,
            DriveFolderId = "folder-a",
        };
        db.Surveys.Add(survey);
        await db.SaveChangesAsync();
        var original = (await service.ForSurveyAsync(survey.Id))!;

        survey.DriveFolderId = "folder-b";
        await db.SaveChangesAsync();
        var rebound = (await service.ForSurveyAsync(survey.Id))!;

        Assert.NotEqual(original.Impact.PlanToken, rebound.Impact.PlanToken);
    }

    [Fact]
    public async Task ForCapabilityAsync_TenderReferenceBlocksAndLinksToTenderDetail()
    {
        var customer = new Customer { Name = "Tender customer" };
        var capability = new CapabilityDocument
        {
            Name = "ISO certificate",
            TagCode = "iso",
            FilePath = "/files/capability/iso.pdf",
            OriginalFileName = "iso.pdf",
            FileSize = 10,
            ContentType = "application/pdf",
        };
        var tender = new Tender
        {
            Code = "TD-CAPABILITY",
            Name = "Capability tender",
            Customer = customer,
            SubmissionDeadline = DateTime.UtcNow.AddDays(1),
        };
        db.AddRange(capability, tender);
        await db.SaveChangesAsync();
        var reference = new TenderChecklistItem
        {
            TenderId = tender.Id,
            Title = "Capability",
            CapabilityDocumentId = capability.Id,
            FilePath = capability.FilePath,
        };
        db.TenderChecklistItems.Add(reference);
        await db.SaveChangesAsync();

        var plan = (await service.ForCapabilityAsync(capability.Id))!;

        Assert.False(plan.Impact.CanDelete);
        var impact = Assert.Single(plan.Impact.Items, item => item.Key == "capability.tenderReferences");
        Assert.Equal(DeletionImpactActions.Block, impact.Action);
        Assert.Contains(reference.Id.ToString(), impact.Examples);
        var link = Assert.Single(impact.ResolutionLinks);
        Assert.Equal($"{tender.Code} · {tender.Name}", link.Label);
        Assert.Equal($"/admin/tenders/{tender.Id}", link.Url);
    }

    [Fact]
    public async Task ForCapabilityAsync_SeedMarkerChangeInvalidatesPlanToken()
    {
        var capability = new CapabilityDocument
        {
            Name = "Seeded capability",
            TagCode = "iso",
            Description = "[SAMPLE_CAP] Seeded capability",
            FilePath = "/files/capability/seeded.pdf",
            OriginalFileName = "seeded.pdf",
            FileSize = 10,
            ContentType = "application/pdf",
        };
        db.CapabilityDocuments.Add(capability);
        await db.SaveChangesAsync();
        var seeded = (await service.ForCapabilityAsync(capability.Id))!;

        capability.Description = "No longer seeded";
        await db.SaveChangesAsync();
        var changed = (await service.ForCapabilityAsync(capability.Id))!;

        Assert.NotEqual(seeded.Impact.PlanToken, changed.Impact.PlanToken);
    }

    [Fact]
    public async Task ForContractAsync_DesignProjectUnlinksWonOpportunityBlocksAndManagedFilesAreSafe()
    {
        var customer = new Customer { Name = "Contract customer" };
        var opportunity = new Opportunity
        {
            Name = "Won opportunity",
            Customer = customer,
            Stage = OpportunityStage.Won,
        };
        db.Opportunities.Add(opportunity);
        await db.SaveChangesAsync();
        var contract = new Contract
        {
            ContractNumber = "HD-LIFECYCLE",
            CustomerId = customer.Id,
            OpportunityId = opportunity.Id,
            Status = ContractStatus.Signed,
            SignedDate = DateTime.UtcNow,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        var managedPath = $"/files/contracts/{contract.Id}/signed.pdf";
        var attachment = new ContractAttachment
        {
            ContractId = contract.Id,
            FilePath = managedPath,
            OriginalFileName = "signed.pdf",
            ContentType = "application/pdf",
        };
        var project = new DesignProject
        {
            ProjectCode = "DP-CONTRACT",
            Name = "Linked design project",
            CustomerId = customer.Id,
            ContractId = contract.Id,
        };
        db.AddRange(attachment, project);
        await db.SaveChangesAsync();

        var plan = (await service.ForContractAsync(contract.Id))!;

        Assert.False(plan.Impact.CanDelete);
        AssertImpact(plan, "contract.designProjects", DeletionImpactActions.Unlink,
            project.Id, $"/admin/design-projects/{project.Id}");
        AssertImpact(plan, "contract.wonOpportunity", DeletionImpactActions.Block,
            opportunity.Id, $"/admin/opportunities/{opportunity.Id}");
        var opportunityLink = Assert.Single(plan.Impact.Items
            .Single(item => item.Key == "contract.wonOpportunity").ResolutionLinks);
        Assert.Equal(opportunity.Name, opportunityLink.Label);
        var definitions = plan.Items.OrderBy(item => item.Sequence).ToList();
        Assert.Equal(HardDeleteItemKind.LocalFile, definitions[0].Kind);
        Assert.Equal(managedPath, definitions[0].ActionIdentifier);
        Assert.Equal(HardDeleteItemKind.DatabaseAggregate, definitions[^1].Kind);
        Assert.Equal("delete-contract-aggregate", definitions[^1].ActionIdentifier);
    }

    [Fact]
    public async Task ForContractAsync_SafeSyncedSidecarAddsDriveDeleteAndUnlinkImpact()
    {
        var (contract, attachment, project) = await CreateContractAttachmentAsync("safe-drive");
        var sidecar = CreateSidecar(project.Id, attachment, "drive-safe");
        db.ProjectDocuments.Add(sidecar);
        await db.SaveChangesAsync();

        var plan = (await service.ForContractAsync(contract.Id))!;

        Assert.True(plan.Impact.CanDelete);
        Assert.Contains(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile &&
            item.ActionIdentifier == "drive-safe" && item.ExpectedParentId == "drive-parent" &&
            item.ExpectedAppProperties!["niconReplicaKey"] == $"project-document:{sidecar.Id}");
        Assert.Equal(DeletionImpactActions.Delete,
            Assert.Single(plan.Impact.Items, item => item.Key == "contract.driveFiles").Action);
        Assert.Equal(DeletionImpactActions.Unlink,
            Assert.Single(plan.Impact.Items, item => item.Key == "contract.projectDocumentSidecars").Action);
    }

    [Fact]
    public async Task ForContractAsync_UnsafeOrDuplicateDriveSidecarsBlockDeletion()
    {
        var (contract, attachment, project) = await CreateContractAttachmentAsync("duplicate-drive");
        var first = CreateSidecar(project.Id, attachment, "duplicate-id");
        var duplicate = CreateSidecar(project.Id, attachment, "duplicate-id");
        duplicate.SourceRecordId = attachment.Id + 1000;
        db.ProjectDocuments.AddRange(first, duplicate);
        await db.SaveChangesAsync();

        var plan = (await service.ForContractAsync(contract.Id))!;

        Assert.False(plan.Impact.CanDelete);
        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "contract.projectDocumentSidecarBlockers" && item.Action == DeletionImpactActions.Block);
        Assert.DoesNotContain(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile);
    }

    private async Task<(Contract Contract, ContractAttachment Attachment, OperationalProject Project)>
        CreateContractAttachmentAsync(string suffix)
    {
        var customer = new Customer { Name = $"Customer {suffix}" };
        var project = new OperationalProject
        {
            Code = $"PJ-{suffix}",
            Name = $"Project {suffix}",
            Customer = customer,
        };
        var contract = new Contract
        {
            ContractNumber = $"HD-{suffix}",
            Customer = customer,
            OperationalProject = project,
        };
        db.AddRange(project, contract);
        await db.SaveChangesAsync();
        var attachment = new ContractAttachment
        {
            ContractId = contract.Id,
            FilePath = $"/files/contracts/{contract.Id}/{suffix}.pdf",
            OriginalFileName = $"{suffix}.pdf",
            ContentType = "application/pdf",
        };
        db.ContractAttachments.Add(attachment);
        await db.SaveChangesAsync();
        return (contract, attachment, project);
    }

    private static ProjectDocument CreateSidecar(
        int projectId, ContractAttachment attachment, string driveFileId) => new()
        {
            OperationalProjectId = projectId,
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
            DriveFileId = driveFileId,
            DriveFolderId = "drive-parent",
            Generation = 2,
        };

    private static void AssertImpact(
        BusinessRootHardDeletePlan plan,
        string key,
        string action,
        int expectedId,
        string expectedUrl)
    {
        var impact = Assert.Single(plan.Impact.Items, item => item.Key == key);
        Assert.Equal(action, impact.Action);
        Assert.Contains(expectedId.ToString(), impact.Examples);
        Assert.Contains(impact.ResolutionLinks, link => link.Url == expectedUrl);
    }

    public void Dispose() => db.Dispose();
}