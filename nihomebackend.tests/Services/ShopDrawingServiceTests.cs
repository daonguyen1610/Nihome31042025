using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-116 Shop Drawing workflow: per-discipline
/// code allocation, status state-machine enforcement (incl. Released
/// being reachable only via the NIH-118 IFC flow), and bulk delete of
/// drafts with partial-success reporting.
/// </summary>
public class ShopDrawingServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ShopDrawingService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public ShopDrawingServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new ShopDrawingService(_db, NullLogger<ShopDrawingService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000060",
            FullName = "Shop Tester",
            Email = "shop.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "design_discipline", Code = "architecture", Name = "Kiến trúc", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "design_discipline", Code = "structure", Name = "Kết cấu", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "design_discipline", Code = "mep", Name = "MEP", IsActive = true, SortOrder = 3 },
            new MasterDataOption { Category = "design_discipline", Code = "interior", Name = "Nội thất", IsActive = true, SortOrder = 4 }
        );

        var customer = new Customer { Name = "ShopCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-SD-TEST",
            Name = "Shop drawing fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateShopDrawingRequest ValidCreate(
        string? title = null,
        string discipline = "architecture",
        string constructionItem = "Móng cọc",
        int? projectId = null) => new()
        {
            DesignProjectId = projectId ?? _projectId,
            DisciplineCode = discipline,
            ConstructionItem = constructionItem,
            Title = title ?? "Test drawing",
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesPrefixedCode()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith("KT-SD-", resp.DrawingCode);
        Assert.EndsWith("-001", resp.DrawingCode);
        Assert.Equal("Drafting", resp.Status);
        Assert.Equal("Móng cọc", resp.ConstructionItem);
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerDiscipline()
    {
        var a = await _sut.CreateAsync(ValidCreate(title: "A", discipline: "architecture"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(title: "B", discipline: "architecture"), _userId);
        var s = await _sut.CreateAsync(ValidCreate(title: "S", discipline: "structure"), _userId);
        var m = await _sut.CreateAsync(ValidCreate(title: "M", discipline: "mep"), _userId);
        var i = await _sut.CreateAsync(ValidCreate(title: "I", discipline: "interior"), _userId);
        Assert.EndsWith("-001", a.DrawingCode);
        Assert.EndsWith("-002", b.DrawingCode);
        Assert.EndsWith("-001", s.DrawingCode);
        Assert.StartsWith("KC-SD-", s.DrawingCode);
        Assert.StartsWith("MEP-SD-", m.DrawingCode);
        Assert.StartsWith("NT-SD-", i.DrawingCode);
    }

    [Fact]
    public async Task CreateAsync_MissingTitle_Throws()
    {
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.CreateAsync(ValidCreate(title: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_MissingConstructionItem_Throws()
    {
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.CreateAsync(ValidCreate(constructionItem: "  "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownDiscipline_Throws()
    {
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.CreateAsync(ValidCreate(discipline: "not-a-discipline"), _userId));
    }

    [Fact]
    public async Task CreateAsync_ProjectNotInShopStage_Throws()
    {
        var dp = await _db.DesignProjects.FirstAsync(x => x.Id == _projectId);
        dp.CurrentStage = DesignProjectStage.BasicDesign;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.CreateAsync(ValidCreate(), _userId));
    }

    // ---------------- Transition state machine ----------------

    [Fact]
    public async Task TransitionStatus_InvalidJump_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        // Drafting → Approved is not allowed; must go via InReview.
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionShopDrawingStatusRequest { Status = "Approved" }, _userId));
    }

    [Fact]
    public async Task TransitionStatus_HappyPath_ToPendingIfc()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        await Transition(created.Id, "Approved");
        var queued = await Transition(created.Id, "PendingIfc");
        Assert.Equal("PendingIfc", queued!.Status);
    }

    [Fact]
    public async Task TransitionStatus_CanBounceBackFromInReviewToDrafting()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        var back = await Transition(created.Id, "Drafting");
        Assert.Equal("Drafting", back!.Status);
    }

    [Fact]
    public async Task TransitionStatus_Released_NotReachable_Via_StatusEndpoint()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        await Transition(created.Id, "Approved");
        await Transition(created.Id, "PendingIfc");
        // Released is reserved for the NIH-118 IFC release flow.
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionShopDrawingStatusRequest { Status = "Released" }, _userId));
    }

    [Fact]
    public async Task TransitionStatus_RejectedIsTerminal()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "Rejected");
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionShopDrawingStatusRequest { Status = "Drafting" }, _userId));
    }

    // ---------------- Delete + guard ----------------

    [Fact]
    public async Task DeleteAsync_Drafting_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_AfterReview_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.DeleteAsync(created.Id));
    }

    // ---------------- Bulk delete ----------------

    [Fact]
    public async Task BulkDelete_MixedRows_ReportsPartialSuccess()
    {
        var a = await _sut.CreateAsync(ValidCreate(title: "A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(title: "B"), _userId);
        var c = await _sut.CreateAsync(ValidCreate(title: "C"), _userId);

        // c is out of the Drafting state — should fail per-row without
        // aborting the whole batch.
        await Transition(c.Id, "InReview");

        var result = await _sut.BulkDeleteAsync(new[] { a.Id, b.Id, c.Id, 999999 });

        Assert.Equal(4, result.Requested);
        Assert.Equal(2, result.Deleted);
        Assert.Equal(2, result.Failures.Count);
        Assert.Contains(result.Failures, f => f.Id == c.Id);
        Assert.Contains(result.Failures, f => f.Id == 999999);
        Assert.Null(await _db.ShopDrawings.FindAsync(a.Id));
        Assert.NotNull(await _db.ShopDrawings.FindAsync(c.Id));
    }

    [Fact]
    public async Task BulkDelete_EmptyList_Throws()
    {
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.BulkDeleteAsync(Array.Empty<int>()));
    }

    [Fact]
    public async Task BulkDelete_ExceedsLimit_Throws()
    {
        var ids = Enumerable.Range(1, 101).ToArray();
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.BulkDeleteAsync(ids));
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_ChangingDisciplineAfterReview_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.UpdateAsync(created.Id, new UpdateShopDrawingRequest
            {
                DisciplineCode = "structure",
                ConstructionItem = created.ConstructionItem,
                Title = created.Title,
            }, _userId));
    }

    [Fact]
    public async Task UpdateAsync_ChangingDisciplineWhileDrafting_ReallocatesCode()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var updated = await _sut.UpdateAsync(created.Id, new UpdateShopDrawingRequest
        {
            DisciplineCode = "structure",
            ConstructionItem = "Móng cọc",
            Title = "Repurposed drawing",
        }, _userId);
        Assert.NotNull(updated);
        Assert.StartsWith("KC-SD-", updated!.DrawingCode);
    }

    // ---------------- List roll-up ----------------

    [Fact]
    public async Task List_ReturnsStatusCounts_MatchingScope()
    {
        await _sut.CreateAsync(ValidCreate(title: "d1"), _userId);
        var r2 = await _sut.CreateAsync(ValidCreate(title: "d2"), _userId);
        await Transition(r2.Id, "InReview");

        var page = await _sut.ListAsync(new ShopDrawingListParams { DesignProjectId = _projectId });
        Assert.Equal(1, page.StatusCounts["Drafting"]);
        Assert.Equal(1, page.StatusCounts["InReview"]);
    }

    // ---------------- helpers ----------------

    private Task<ShopDrawingResponse?> Transition(int id, string status) =>
        _sut.TransitionStatusAsync(id,
            new TransitionShopDrawingStatusRequest { Status = status }, _userId);
}
