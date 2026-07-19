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
/// Unit coverage for the NIH-117 Drawing Revision workflow: auto-numbered
/// append-only revisions with previous-latest superseded, target
/// validation across BasicDesignDoc + ShopDrawing, reason master-data
/// gate, and metadata-only diff between two revisions of the same target.
/// </summary>
public class DrawingRevisionServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DrawingRevisionService _sut;
    private readonly int _userId;
    private readonly int _basicId;
    private readonly int _shopId;
    private readonly int _otherShopId;

    public DrawingRevisionServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new DrawingRevisionService(_db, NullLogger<DrawingRevisionService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000070",
            FullName = "Revision Tester",
            Email = "rev.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "drawing_revision_reason", Code = "client-request", Name = "Yêu cầu khách", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "drawing_revision_reason", Code = "technical-fix", Name = "Sai kỹ thuật", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "drawing_revision_reason", Code = "mep-sync", Name = "Đồng bộ MEP", IsActive = true, SortOrder = 3 }
        );

        var customer = new Customer { Name = "RevCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-REV-TEST",
            Name = "Revision fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        var basic = new BasicDesignDoc
        {
            DesignProjectId = project.Id,
            DisciplineCode = "architecture",
            DocumentCode = "KT-BD-001",
            Title = "Basic fixture",
            Status = BasicDesignDocStatus.InProgress,
        };
        var shop = new ShopDrawing
        {
            DesignProjectId = project.Id,
            DisciplineCode = "architecture",
            ConstructionItem = "Móng cọc",
            DrawingCode = "KT-SD-001",
            Title = "Shop fixture",
            Status = ShopDrawingStatus.Drafting,
        };
        var otherShop = new ShopDrawing
        {
            DesignProjectId = project.Id,
            DisciplineCode = "structure",
            ConstructionItem = "Cột kết cấu",
            DrawingCode = "KC-SD-001",
            Title = "Second shop fixture",
            Status = ShopDrawingStatus.Drafting,
        };
        _db.BasicDesignDocs.Add(basic);
        _db.ShopDrawings.AddRange(shop, otherShop);
        _db.SaveChanges();

        _userId = user.Id;
        _basicId = basic.Id;
        _shopId = shop.Id;
        _otherShopId = otherShop.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateDrawingRevisionRequest ValidCreate(
        string targetType = "ShopDrawing",
        int? targetId = null,
        string reason = "client-request",
        string note = "sample change") => new()
        {
            TargetType = targetType,
            TargetId = targetId ?? _shopId,
            ReasonCode = reason,
            Note = note,
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_StartsAtR1_AndIsCurrent()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.Equal(1, resp.RevisionNumber);
        Assert.True(resp.IsCurrent);
        Assert.False(resp.IsSuperseded);
        Assert.Equal("ShopDrawing", resp.TargetType);
    }

    [Fact]
    public async Task CreateAsync_SecondRevision_PreviousBecomesSuperseded()
    {
        var first = await _sut.CreateAsync(ValidCreate(note: "first"), _userId);
        var second = await _sut.CreateAsync(ValidCreate(reason: "mep-sync", note: "second"), _userId);

        Assert.Equal(2, second.RevisionNumber);
        Assert.True(second.IsCurrent);

        var reloadedFirst = await _sut.GetAsync(first.Id);
        Assert.NotNull(reloadedFirst);
        Assert.False(reloadedFirst!.IsCurrent);
        Assert.True(reloadedFirst.IsSuperseded);
    }

    [Fact]
    public async Task CreateAsync_MissingNote_Throws()
    {
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(note: "  "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownReason_Throws()
    {
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(reason: "not-a-reason"), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownTargetType_Throws()
    {
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(targetType: "Bogus"), _userId));
    }

    [Fact]
    public async Task CreateAsync_MissingTarget_Throws()
    {
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.CreateAsync(ValidCreate(targetId: 9999999), _userId));
    }

    [Fact]
    public async Task CreateAsync_PerTargetSequence_IsIndependent()
    {
        var s1 = await _sut.CreateAsync(ValidCreate(targetId: _shopId, note: "s1"), _userId);
        var s2 = await _sut.CreateAsync(ValidCreate(targetId: _shopId, note: "s2"), _userId);
        var o1 = await _sut.CreateAsync(ValidCreate(targetId: _otherShopId, note: "o1"), _userId);

        Assert.Equal(1, s1.RevisionNumber);
        Assert.Equal(2, s2.RevisionNumber);
        Assert.Equal(1, o1.RevisionNumber);
    }

    [Fact]
    public async Task CreateAsync_WorksAcrossTargetFamilies()
    {
        await _sut.CreateAsync(ValidCreate(targetType: "ShopDrawing", targetId: _shopId, note: "s"), _userId);
        var basicR1 = await _sut.CreateAsync(ValidCreate(targetType: "BasicDesignDoc", targetId: _basicId, reason: "technical-fix", note: "b"), _userId);
        Assert.Equal(1, basicR1.RevisionNumber);
        Assert.Equal("BasicDesignDoc", basicR1.TargetType);
    }

    // ---------------- List ----------------

    [Fact]
    public async Task List_ByTarget_ReturnsNewestFirst_WithOneCurrent()
    {
        await _sut.CreateAsync(ValidCreate(note: "R1"), _userId);
        await _sut.CreateAsync(ValidCreate(reason: "mep-sync", note: "R2"), _userId);
        await _sut.CreateAsync(ValidCreate(reason: "technical-fix", note: "R3"), _userId);

        var page = await _sut.ListAsync(new DrawingRevisionListParams
        {
            TargetType = "ShopDrawing",
            TargetId = _shopId,
        });
        Assert.Equal(3, page.Total);
        Assert.Equal(new[] { 3, 2, 1 }, page.Items.Select(i => i.RevisionNumber));
        Assert.Single(page.Items, i => i.IsCurrent);
        Assert.True(page.Items[0].IsCurrent);
    }

    [Fact]
    public async Task List_UnknownTargetType_Throws()
    {
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.ListAsync(new DrawingRevisionListParams { TargetType = "Bogus" }));
    }

    // ---------------- Diff ----------------

    [Fact]
    public async Task Diff_ReturnsMetadataChanges()
    {
        var a = await _sut.CreateAsync(ValidCreate(reason: "client-request", note: "first note"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(reason: "mep-sync", note: "different note"), _userId);

        var diff = await _sut.DiffAsync(a.Id, b.Id);
        Assert.NotNull(diff);
        Assert.Contains(diff!.Changes, msg => msg.Contains("Lý do", StringComparison.Ordinal));
        Assert.Contains(diff.Changes, msg => msg.Contains("Ghi chú", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Diff_AcrossDifferentTargets_Throws()
    {
        var a = await _sut.CreateAsync(ValidCreate(targetId: _shopId), _userId);
        var b = await _sut.CreateAsync(ValidCreate(targetId: _otherShopId, note: "other"), _userId);
        await Assert.ThrowsAsync<DrawingRevisionOperationException>(() =>
            _sut.DiffAsync(a.Id, b.Id));
    }

    [Fact]
    public async Task Diff_MissingRevision_ReturnsNull()
    {
        Assert.Null(await _sut.DiffAsync(9999999, 9999998));
    }
}
