using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    private readonly Mock<IProjectDocumentStagingService> _projectDocuments = new();
    private readonly string _contentRoot;
    private readonly int _userId;
    private readonly int _projectId;
    private readonly int _operationalProjectId;

    public ShopDrawingServiceTests()
    {
        _db = DbContextFactory.Create();
        _contentRoot = Path.Combine(Path.GetTempPath(), $"nihome-shop-drawing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_contentRoot);
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(_contentRoot);
        _sut = new ShopDrawingService(
            _db,
            NullLogger<ShopDrawingService>.Instance,
            _projectDocuments.Object,
            environment.Object);

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

        var operationalProject = new OperationalProject
        {
            Code = "OP-SHOP",
            Name = "Shop operational project",
            CustomerId = customer.Id,
        };
        _db.OperationalProjects.Add(operationalProject);
        _db.SaveChanges();

        var project = new DesignProject
        {
            OperationalProjectId = operationalProject.Id,
            ProjectCode = "DP-2026-SD-TEST",
            Name = "Shop drawing fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
        _operationalProjectId = operationalProject.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

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
    public async Task ListAsync_AccessibleDisciplines_FiltersItemsCountsAndTotal()
    {
        await _sut.CreateAsync(ValidCreate(title: "Architecture", discipline: "architecture"), _userId);
        await _sut.CreateAsync(ValidCreate(title: "Structure", discipline: "structure"), _userId);

        var result = await _sut.ListAsync(
            new ShopDrawingListParams { DesignProjectId = _projectId },
            accessibleDisciplines: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "architecture" });

        var item = Assert.Single(result.Items);
        Assert.Equal("architecture", item.DisciplineCode);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.StatusCounts[nameof(ShopDrawingStatus.Drafting)]);
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
    public async Task UploadFileAsync_ReplacementAndDelete_CleanManagedFiles()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var first = await _sut.UploadFileAsync(created.Id, FormFile("first", "first.pdf"), _userId);
        var firstPath = ManagedPath(first!.FilePath!);
        Assert.True(File.Exists(firstPath));

        var replacement = await _sut.UploadFileAsync(created.Id, FormFile("second", "second.pdf"), _userId);
        var replacementPath = ManagedPath(replacement!.FilePath!);
        Assert.False(File.Exists(firstPath));
        Assert.True(File.Exists(replacementPath));

        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.False(File.Exists(replacementPath));
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            _operationalProjectId, ProjectDocumentSourceModule.Design, nameof(ShopDrawing), "file",
            created.Id, first.FilePath!, _userId, It.IsAny<CancellationToken>()), Times.Once);
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileAsync(
            _operationalProjectId, ProjectDocumentCategory.DesignShopDrawing, ProjectDocumentSourceModule.Design,
            nameof(ShopDrawing), "file", created.Id, replacement.FilePath!, "second.pdf",
            It.IsAny<int?>(), It.IsAny<int?>(), _userId, It.IsAny<CancellationToken>()), Times.Once);
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            _operationalProjectId, ProjectDocumentSourceModule.Design, nameof(ShopDrawing), "file",
            created.Id, replacement.FilePath!, _userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_ProjectOutsideShopStage_IsRejected()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var project = await _db.DesignProjects.FindAsync(_projectId);
        project!.CurrentStage = DesignProjectStage.Completed;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ShopDrawingOperationException>(() =>
            _sut.UploadFileAsync(created.Id, FormFile("blocked", "blocked.pdf"), _userId));
    }

    [Fact]
    public async Task DeleteAsync_AfterReview_RemovesRevisionsAndIfcReferences()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await Transition(created.Id, "InReview");
        var release = new IfcRelease
        {
            DesignProjectId = _projectId,
            ReleaseNumber = "IFC-DELETE-001",
            Title = "Preserved release",
            Status = IfcReleaseStatus.Released,
        };
        _db.IfcReleases.Add(release);
        await _db.SaveChangesAsync();
        _db.IfcReleaseItems.Add(new IfcReleaseItem
        {
            IfcReleaseId = release.Id,
            ShopDrawingId = created.Id,
        });
        _db.DrawingRevisions.Add(new DrawingRevision
        {
            TargetType = DrawingRevisionTargetType.ShopDrawing,
            TargetId = created.Id,
            RevisionNumber = 1,
            ReasonCode = "client-change",
            Note = "Delete cleanup",
            IsCurrent = true,
            CreatedByUserId = _userId,
        });
        await _db.SaveChangesAsync();

        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.Null(await _db.ShopDrawings.FindAsync(created.Id));
        Assert.Empty(await _db.IfcReleaseItems.ToListAsync());
        Assert.Empty(await _db.DrawingRevisions.ToListAsync());
        Assert.NotNull(await _db.IfcReleases.FindAsync(release.Id));
    }

    // ---------------- Bulk delete ----------------

    [Fact]
    public async Task BulkDelete_AllStatuses_RemovesAggregatesAndReportsMissingRows()
    {
        var a = await _sut.CreateAsync(ValidCreate(title: "A"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(title: "B"), _userId);
        var c = await _sut.CreateAsync(ValidCreate(title: "C"), _userId);

        var firstRow = await _db.ShopDrawings.FindAsync(a.Id);
        firstRow!.FilePath = "/files/design/shop/bulk-a.pdf";
        await _db.SaveChangesAsync();

        await Transition(c.Id, "InReview");
        var release = new IfcRelease
        {
            DesignProjectId = _projectId,
            ReleaseNumber = "IFC-BULK-DELETE-001",
            Title = "Preserved release",
        };
        _db.IfcReleases.Add(release);
        await _db.SaveChangesAsync();
        _db.IfcReleaseItems.Add(new IfcReleaseItem
        {
            IfcReleaseId = release.Id,
            ShopDrawingId = c.Id,
        });
        _db.DrawingRevisions.Add(new DrawingRevision
        {
            TargetType = DrawingRevisionTargetType.ShopDrawing,
            TargetId = c.Id,
            RevisionNumber = 1,
            ReasonCode = "client-change",
            Note = "Bulk delete cleanup",
            IsCurrent = true,
            CreatedByUserId = _userId,
        });
        await _db.SaveChangesAsync();

        var result = await _sut.BulkDeleteAsync(new[] { a.Id, b.Id, c.Id, 999999 });

        Assert.Equal(4, result.Requested);
        Assert.Equal(3, result.Deleted);
        Assert.Single(result.Failures);
        Assert.Contains(result.Failures, f => f.Id == 999999);
        Assert.Null(await _db.ShopDrawings.FindAsync(a.Id));
        Assert.Null(await _db.ShopDrawings.FindAsync(c.Id));
        Assert.Empty(await _db.IfcReleaseItems.ToListAsync());
        Assert.Empty(await _db.DrawingRevisions.ToListAsync());
        Assert.NotNull(await _db.IfcReleases.FindAsync(release.Id));
        _projectDocuments.Verify(staging => staging.StageExistingManagedFileDeleteAsync(
            _operationalProjectId, ProjectDocumentSourceModule.Design, nameof(ShopDrawing), "file",
            a.Id, "/files/design/shop/bulk-a.pdf", It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
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

    private string ManagedPath(string relativePath) =>
        Path.Combine(_contentRoot, "wwwroot", relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static FormFile FormFile(string content, string fileName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };
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
