using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-114 Concept option workflow: create /
/// update guards, state-machine enforcement, and the finalize +
/// project-unlock invariants.
/// </summary>
public class ConceptOptionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ConceptOptionService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public ConceptOptionServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ConceptOptionService(_db, NullLogger<ConceptOptionService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000040",
            FullName = "Concept Tester",
            Email = "concept.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        var customer = new Customer { Name = "ConceptCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var operationalProject = new OperationalProject
        {
            Code = "OP-CONCEPT",
            Name = "Concept operational project",
            CustomerId = customer.Id,
        };
        _db.OperationalProjects.Add(operationalProject);
        _db.SaveChanges();

        var project = new DesignProject
        {
            OperationalProjectId = operationalProject.Id,
            ProjectCode = "DP-2026-CONCEPT",
            Name = "Concept test",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.Concept,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateConceptOptionRequest ValidCreate(string? name = null, int? projectId = null) => new()
    {
        DesignProjectId = projectId ?? _projectId,
        Name = name ?? "Option A",
        Description = "Modern minimal.",
    };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_StartsInDrafting()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.Equal("Drafting", resp.Status);
        Assert.Equal(_projectId, resp.DesignProjectId);
    }

    [Fact]
    public async Task CreateAsync_MissingName_Throws()
    {
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(name: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownProject_Throws()
    {
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(projectId: 999999), _userId));
    }

    [Fact]
    public async Task CreateAsync_ProjectPastConcept_Throws()
    {
        var dp = await _db.DesignProjects.FirstAsync(x => x.Id == _projectId);
        dp.CurrentStage = DesignProjectStage.BasicDesign;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(), _userId));
    }

    // ---------------- Transition ----------------

    [Fact]
    public async Task TransitionStatus_InvalidTarget_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionConceptOptionStatusRequest { Status = "Bogus" }, _userId));
    }

    [Fact]
    public async Task TransitionStatus_DraftingToPresentedDirectly_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        // Drafting → PresentedToClient is not allowed; must go via PendingInternalReview.
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionConceptOptionStatusRequest { Status = "PresentedToClient" }, _userId));
    }

    [Fact]
    public async Task TransitionStatus_Finalize_UnlocksProjectAndDiscardsSiblings()
    {
        var a = await _sut.CreateAsync(ValidCreate("A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate("B"), _userId);
        var c = await _sut.CreateAsync(ValidCreate("C"), _userId);
        await MoveToPresentedAsync(a.Id);
        await MoveToPresentedAsync(b.Id);
        // Leave c in Drafting.

        var finalResp = await _sut.TransitionStatusAsync(a.Id,
            new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId);

        Assert.NotNull(finalResp);
        Assert.Equal("Finalized", finalResp!.Status);

        var bAfter = await _sut.GetAsync(b.Id);
        var cAfter = await _sut.GetAsync(c.Id);
        Assert.Equal("Discarded", bAfter!.Status);
        Assert.Equal("Discarded", cAfter!.Status);

        var project = await _db.DesignProjects.FindAsync(_projectId);
        Assert.Equal(DesignProjectStage.BasicDesign, project!.CurrentStage);
    }

    [Fact]
    public async Task TransitionStatus_FinalizeSecond_Throws()
    {
        var a = await _sut.CreateAsync(ValidCreate("A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate("B"), _userId);
        await MoveToPresentedAsync(a.Id);
        await MoveToPresentedAsync(b.Id);
        await _sut.TransitionStatusAsync(a.Id,
            new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId);

        // b was auto-discarded so the invariant is preserved. Push a
        // fresh row + try to finalize a second one — should be blocked
        // by the "already finalized" guard.
        // First bring the project back to Concept so we can create.
        var project = await _db.DesignProjects.FindAsync(_projectId);
        project!.CurrentStage = DesignProjectStage.Concept;
        await _db.SaveChangesAsync();
        var c = await _sut.CreateAsync(ValidCreate("C"), _userId);
        await MoveToPresentedAsync(c.Id);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(c.Id,
                new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId));
    }

    [Fact]
    public async Task Finalize_NewerUnsubmittedDraft_DoesNotHideApprovedRevision()
    {
        var option = await _sut.CreateAsync(ValidCreate(), _userId);
        await MoveToPresentedAsync(option.Id);
        var revisionId = await _db.ConceptRevisions.Where(item => item.ConceptOptionId == option.Id)
            .Select(item => item.Id).SingleAsync();
        _db.DesignReviews.Add(new DesignReview
        {
            DesignProjectId = _projectId,
            Stage = DesignReviewStage.Concept,
            ConceptRevisionId = revisionId,
            RevisionNumber = 2,
            Status = DesignReviewStatus.Draft,
            Title = "Nháp chưa trình",
            DesignManagerUserId = _userId,
        });
        await _db.SaveChangesAsync();
        var finalized = await _sut.TransitionStatusAsync(option.Id,
            new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId);
        Assert.Equal("Finalized", finalized!.Status);
        Assert.Equal(DesignProjectStage.BasicDesign, (await _db.DesignProjects.FindAsync(_projectId))!.CurrentStage);
    }

    [Fact]
    public async Task Finalize_ApprovalForAnotherOption_DoesNotUnlockProject()
    {
        var selected = await _sut.CreateAsync(ValidCreate("Selected"), _userId);
        var other = await _sut.CreateAsync(ValidCreate("Other"), _userId);
        await MoveToPresentedAsync(other.Id);
        var option = await _db.ConceptOptions.FindAsync(selected.Id);
        option!.Status = ConceptOptionStatus.PresentedToClient;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(selected.Id,
                new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId));
        Assert.Equal(DesignProjectStage.Concept, (await _db.DesignProjects.FindAsync(_projectId))!.CurrentStage);
        Assert.Equal(ConceptOptionStatus.PresentedToClient, option.Status);
    }

    [Fact]
    public async Task Finalize_ApprovalForPreviousRevision_DoesNotUnlockProject()
    {
        var selected = await _sut.CreateAsync(ValidCreate(), _userId);
        await MoveToPresentedAsync(selected.Id);
        _db.ConceptRevisions.Add(new ConceptRevision { ConceptOptionId = selected.Id, Number = 1 });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(selected.Id,
                new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId));
        Assert.Equal(DesignProjectStage.Concept, (await _db.DesignProjects.FindAsync(_projectId))!.CurrentStage);
    }

    [Theory]
    [InlineData(DesignReviewStatus.ClientChangesRequested)]
    [InlineData(DesignReviewStatus.ClientApproved)]
    public async Task Finalize_NewerReviewWithoutAcceptance_DoesNotReuseOldApproval(DesignReviewStatus status)
    {
        var selected = await _sut.CreateAsync(ValidCreate(), _userId);
        await MoveToPresentedAsync(selected.Id);
        var revisionId = await _db.ConceptRevisions.Where(item => item.ConceptOptionId == selected.Id)
            .Select(item => item.Id).SingleAsync();
        _db.DesignReviews.Add(new DesignReview
        {
            DesignProjectId = _projectId,
            Stage = DesignReviewStage.Concept,
            ConceptRevisionId = revisionId,
            RevisionNumber = 2,
            Status = status,
            Title = "Latest review has no signed acceptance",
            DesignManagerUserId = _userId,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.TransitionStatusAsync(selected.Id,
                new TransitionConceptOptionStatusRequest { Status = "Finalized" }, _userId));
        Assert.Equal(DesignProjectStage.Concept, (await _db.DesignProjects.FindAsync(_projectId))!.CurrentStage);
    }

    // ---------------- Update / Delete ----------------

    [Fact]
    public async Task UpdateAsync_DiscardedRow_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.TransitionStatusAsync(created.Id,
            new TransitionConceptOptionStatusRequest { Status = "Discarded" }, _userId);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.UpdateAsync(created.Id, new UpdateConceptOptionRequest { Name = "renamed" }, _userId));
    }

    [Fact]
    public async Task DeleteAsync_DraftingRow_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() => _sut.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_PresentedRow_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await MoveToPresentedAsync(created.Id);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() => _sut.DeleteAsync(created.Id));
    }

    // ---------------- helpers ----------------

    private async Task MoveToPresentedAsync(int id)
    {
        // Simulate the DesignReviewService handoff, then exercise the option
        // finalization service against the persisted approval evidence.
        var option = await _db.ConceptOptions.SingleAsync(item => item.Id == id);
        option.Status = ConceptOptionStatus.PresentedToClient;
        var revision = await _db.ConceptRevisions.SingleAsync(item => item.ConceptOptionId == id);
        revision.FrozenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var operationalProjectId = await _db.DesignProjects.Where(project => project.Id == _projectId)
            .Select(project => project.OperationalProjectId!.Value).SingleAsync();
        var document = new ProjectDocument
        {
            OperationalProjectId = operationalProjectId,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = id,
            LocalPath = $"/files/project-documents/concept-{id}.pdf",
            OriginalFileName = $"concept-{id}.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('c', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        var acceptance = new ProjectDocument
        {
            OperationalProjectId = operationalProjectId,
            Category = ProjectDocumentCategory.DesignConcept,
            SourceModule = ProjectDocumentSourceModule.Design,
            SourceEntityType = nameof(ConceptOption),
            SourceRecordId = id,
            LocalPath = $"/files/project-documents/TK-M03-{id}.pdf",
            OriginalFileName = $"TK-M03-{id}-signed.pdf",
            ContentType = "application/pdf",
            Sha256 = new string('d', 64),
            SyncStatus = ProjectDocumentSyncStatus.Synced,
        };
        _db.ProjectDocuments.AddRange(document, acceptance);
        await _db.SaveChangesAsync();
        var reviewNumber = await _db.DesignReviews.Where(item => item.DesignProjectId == _projectId &&
            item.Stage == DesignReviewStage.Concept).Select(item => (int?)item.RevisionNumber).MaxAsync() ?? 0;
        _db.DesignReviews.Add(new DesignReview
        {
            DesignProjectId = _projectId,
            Stage = DesignReviewStage.Concept,
            RevisionNumber = reviewNumber + 1,
            ConceptRevisionId = revision.Id,
            ClientAcceptanceDocumentId = acceptance.Id,
            ClientAcceptanceSignedBy = "Đại diện khách hàng",
            ClientAcceptanceSignedAt = DateTime.UtcNow,
            Title = "Concept client approval",
            Status = DesignReviewStatus.ClientApproved,
            DesignManagerUserId = _userId,
            CreatedByUserId = _userId,
            UpdatedByUserId = _userId,
            Documents =
            [
                new DesignReviewDocument
                    {
                        ProjectDocumentId = document.Id,
                        SourceEntityTypeSnapshot = nameof(ConceptOption),
                        SourceRecordIdSnapshot = id,
                        Sha256Snapshot = document.Sha256,
                    },
                ],
        });
        await _db.SaveChangesAsync();
    }
}
