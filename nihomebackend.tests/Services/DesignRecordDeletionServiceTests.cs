using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class DesignRecordDeletionServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly Mock<IConceptOptionService> concepts = new();
    private readonly DesignRecordDeletionService service;
    private readonly int projectId;
    private readonly int operationalProjectId;

    public DesignRecordDeletionServiceTests()
    {
        var customer = new Customer { Name = "NICON design deletion customer", Type = CustomerType.Company };
        db.Add(customer);
        db.SaveChanges();
        var operationalProject = new OperationalProject
        {
            Code = "NICON-DEL-01",
            Name = "NICON design deletion project",
            CustomerId = customer.Id,
        };
        db.Add(operationalProject);
        db.SaveChanges();
        operationalProjectId = operationalProject.Id;
        var project = new DesignProject
        {
            OperationalProjectId = operationalProject.Id,
            ProjectCode = "DP-NICON-DEL-01",
            Name = "Design hard-delete test",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.Concept,
        };
        db.Add(project);
        db.SaveChanges();
        projectId = project.Id;

        service = new DesignRecordDeletionService(
            db,
            concepts.Object,
            Mock.Of<IBasicDesignDocService>(),
            Mock.Of<IShopDrawingService>(),
            Mock.Of<IIfcReleaseService>());
    }

    [Fact]
    public async Task Impact_BlocksConceptThatHasNotBeenDiscarded()
    {
        var concept = AddConcept("Phương án đang làm", ConceptOptionStatus.Drafting);

        var impact = await service.GetImpactAsync(
            DesignProjectResourceType.ConceptOption, concept.Id);

        Assert.NotNull(impact);
        Assert.False(impact.CanDelete);
        Assert.Contains(impact.Items, item =>
            item.Key == "design.recordState" && item.Action == "Block");
    }

    [Fact]
    public async Task Delete_RejectsStalePlanWithoutCallingDomainDelete()
    {
        var concept = AddConcept("PA-KT-02", ConceptOptionStatus.Discarded);
        var original = await service.GetImpactAsync(
            DesignProjectResourceType.ConceptOption, concept.Id);
        Assert.NotNull(original);

        db.ProjectDocuments.Add(new ProjectDocument
        {
            OperationalProjectId = operationalProjectId,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = concept.Id,
            LocalPath = "/files/project-documents/pa-kt-02.pdf",
            OriginalFileName = "PA-KT-02.pdf",
            Sha256 = new string('a', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DeletionPlanChangedException>(() => service.DeleteAsync(
            DesignProjectResourceType.ConceptOption,
            concept.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = original.PlanToken,
                Confirmation = original.RequiredConfirmation,
            }));
        concepts.Verify(item => item.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Impact_BlocksReviewSharedWithAnotherRecord()
    {
        var target = AddConcept("PA-KT-03", ConceptOptionStatus.Discarded);
        var sibling = AddConcept("PA-KT-04", ConceptOptionStatus.Discarded);
        var manager = AddUser();
        var targetDocument = AddDeletedDocument(target.Id, "PA-KT-03.pdf");
        var siblingDocument = AddDeletedDocument(sibling.Id, "PA-KT-04.pdf");
        var review = new DesignReview
        {
            DesignProjectId = projectId,
            Stage = DesignReviewStage.Concept,
            RevisionNumber = 1,
            Title = "Trình chung hai phương án",
            DesignManagerUserId = manager.Id,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        review.Documents.Add(ReviewDocument(review, targetDocument, target.Id));
        review.Documents.Add(ReviewDocument(review, siblingDocument, sibling.Id));
        db.Add(review);
        await db.SaveChangesAsync();

        var impact = await service.GetImpactAsync(
            DesignProjectResourceType.ConceptOption, target.Id);

        Assert.NotNull(impact);
        Assert.False(impact.CanDelete);
        Assert.Contains(impact.Items, item =>
            item.Key == "design.sharedReviewEvidence" && item.Action == "Block");
    }

    [Fact]
    public async Task Delete_RemovesExclusiveReviewAndCallsDomainDelete()
    {
        var concept = AddConcept("PA-KT-05", ConceptOptionStatus.Discarded);
        var manager = AddUser();
        var document = AddDeletedDocument(concept.Id, "PA-KT-05.pdf");
        var review = new DesignReview
        {
            DesignProjectId = projectId,
            Stage = DesignReviewStage.Concept,
            RevisionNumber = 1,
            Title = "Trình riêng phương án PA-KT-05",
            DesignManagerUserId = manager.Id,
            CreatedByUserId = manager.Id,
            UpdatedByUserId = manager.Id,
        };
        review.Documents.Add(ReviewDocument(review, document, concept.Id));
        db.Add(review);
        await db.SaveChangesAsync();
        concepts.Setup(item => item.DeleteAsync(concept.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var impact = await service.GetImpactAsync(
            DesignProjectResourceType.ConceptOption, concept.Id);
        Assert.NotNull(impact);
        Assert.True(impact.CanDelete);

        var deleted = await service.DeleteAsync(
            DesignProjectResourceType.ConceptOption,
            concept.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = impact.PlanToken,
                Confirmation = impact.RequiredConfirmation,
            });

        Assert.True(deleted);
        Assert.Empty(db.DesignReviews.Where(item => item.Id == review.Id));
        concepts.Verify(item => item.DeleteAsync(concept.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => db.Dispose();

    private ConceptOption AddConcept(string name, ConceptOptionStatus status)
    {
        var concept = new ConceptOption { DesignProjectId = projectId, Name = name, Status = status };
        db.Add(concept);
        db.SaveChanges();
        return concept;
    }

    private ApplicationUser AddUser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new ApplicationUser
        {
            PhoneNumber = $"0900{suffix[..6]}",
            FullName = "Chủ nhiệm thiết kế NICON",
            Email = $"design.manager.{suffix}@example.test",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        db.Add(user);
        db.SaveChanges();
        return user;
    }

    private ProjectDocument AddDeletedDocument(int sourceId, string name)
    {
        var document = new ProjectDocument
        {
            OperationalProjectId = operationalProjectId,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = sourceId,
            LocalPath = $"/files/project-documents/{name}",
            OriginalFileName = name,
            Sha256 = new string('b', 64),
            SyncStatus = ProjectDocumentSyncStatus.Deleted,
            DesiredOperation = ProjectDocumentDesiredOperation.Delete,
            DeletedAt = DateTime.UtcNow,
        };
        db.Add(document);
        db.SaveChanges();
        return document;
    }

    private static DesignReviewDocument ReviewDocument(
        DesignReview review, ProjectDocument document, int sourceId) => new()
        {
            DesignReview = review,
            ProjectDocument = document,
            SourceEntityTypeSnapshot = nameof(ConceptOption),
            SourceRecordIdSnapshot = sourceId,
            Sha256Snapshot = document.Sha256,
        };
}
