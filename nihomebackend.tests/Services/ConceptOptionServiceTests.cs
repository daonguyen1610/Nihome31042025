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

        var project = new DesignProject
        {
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
    public async Task DeleteAsync_DraftingRow_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Null(await _sut.GetAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_PresentedRow_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await MoveToPresentedAsync(created.Id);
        await Assert.ThrowsAsync<ConceptOptionOperationException>(() =>
            _sut.DeleteAsync(created.Id));
    }

    // ---------------- helpers ----------------

    private async Task MoveToPresentedAsync(int id)
    {
        await _sut.TransitionStatusAsync(id,
            new TransitionConceptOptionStatusRequest { Status = "PendingInternalReview" }, _userId);
        await _sut.TransitionStatusAsync(id,
            new TransitionConceptOptionStatusRequest { Status = "PresentedToClient" }, _userId);
    }
}
