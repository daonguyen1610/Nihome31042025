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
/// Unit coverage for the NIH-118 IFC Release workflow: header CRUD +
/// draft guards, item / recipient management, atomic release flipping
/// shop drawings to Released, cancel semantics, and per-recipient
/// acknowledgement tracking after release.
/// </summary>
public class IfcReleaseServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IfcReleaseService _sut;
    private readonly int _userId;
    private readonly int _projectId;
    private readonly int _approvedShopId;
    private readonly int _pendingIfcShopId;
    private readonly int _draftingShopId;

    public IfcReleaseServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new IfcReleaseService(_db, NullLogger<IfcReleaseService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000080",
            FullName = "IFC Tester",
            Email = "ifc.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "design_discipline", Code = "architecture", Name = "Kiến trúc", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "ifc_recipient_type", Code = "main-contractor", Name = "Nhà thầu chính", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "ifc_recipient_type", Code = "supervisor", Name = "Giám sát", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "ifc_recipient_type", Code = "client", Name = "Chủ đầu tư", IsActive = true, SortOrder = 3 }
        );

        var customer = new Customer { Name = "IfcCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-IFC-TEST",
            Name = "IFC fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        var approved = new ShopDrawing
        {
            DesignProjectId = project.Id,
            DisciplineCode = "architecture",
            ConstructionItem = "Móng cọc",
            DrawingCode = "KT-SD-001",
            Title = "approved fixture",
            Status = ShopDrawingStatus.Approved,
        };
        var pendingIfc = new ShopDrawing
        {
            DesignProjectId = project.Id,
            DisciplineCode = "architecture",
            ConstructionItem = "Móng cọc",
            DrawingCode = "KT-SD-002",
            Title = "pending ifc fixture",
            Status = ShopDrawingStatus.PendingIfc,
        };
        var drafting = new ShopDrawing
        {
            DesignProjectId = project.Id,
            DisciplineCode = "architecture",
            ConstructionItem = "Móng cọc",
            DrawingCode = "KT-SD-003",
            Title = "drafting fixture",
            Status = ShopDrawingStatus.Drafting,
        };
        _db.ShopDrawings.AddRange(approved, pendingIfc, drafting);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
        _approvedShopId = approved.Id;
        _pendingIfcShopId = pendingIfc.Id;
        _draftingShopId = drafting.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateIfcReleaseRequest ValidCreate(string title = "Fixture release") => new()
    {
        DesignProjectId = _projectId,
        Title = title,
    };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesReleaseNumber_AsDraft()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith("IFC-", resp.ReleaseNumber);
        Assert.EndsWith("-001", resp.ReleaseNumber);
        Assert.Equal("Draft", resp.Status);
    }

    [Fact]
    public async Task CreateAsync_ProjectNotAtShopStage_Throws()
    {
        var dp = await _db.DesignProjects.FirstAsync(x => x.Id == _projectId);
        dp.CurrentStage = DesignProjectStage.BasicDesign;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.CreateAsync(ValidCreate(), _userId));
    }

    // ---------------- Items ----------------

    [Fact]
    public async Task AddItems_ApprovedDrawing_Succeeds()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        var updated = await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        Assert.Single(updated.Items);
        Assert.Equal(_approvedShopId, updated.Items[0].ShopDrawingId);
    }

    [Fact]
    public async Task AddItems_DraftingDrawing_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
            {
                ShopDrawingIds = new List<int> { _draftingShopId },
            }, _userId));
    }

    [Fact]
    public async Task AddItems_DuplicateIsIdempotent()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        var again = await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        Assert.Single(again.Items);
    }

    [Fact]
    public async Task RemoveItem_Succeeds_WhileDraft()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        var full = (await _sut.GetAsync(release.Id))!;
        var updated = await _sut.RemoveItemAsync(release.Id, full.Items[0].Id, _userId);
        Assert.Empty(updated.Items);
    }

    // ---------------- Recipients ----------------

    [Fact]
    public async Task AddRecipient_UnknownType_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
            {
                Name = "X",
                RecipientTypeCode = "not-a-type",
            }, _userId));
    }

    [Fact]
    public async Task AddRecipient_HappyPath_Succeeds()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        var updated = await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC Corp",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        Assert.Single(updated.Recipients);
        Assert.False(updated.Recipients[0].IsAcknowledged);
    }

    // ---------------- Release ----------------

    [Fact]
    public async Task ReleaseAsync_HappyPath_FlipsDrawingsAndLocksHeader()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId, _pendingIfcShopId },
        }, _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);

        var released = await _sut.ReleaseAsync(release.Id, _userId);
        Assert.Equal("Released", released.Status);
        Assert.NotNull(released.ReleaseDate);
        Assert.Equal(_userId, released.IssuedByUserId);

        var approvedDrawing = await _db.ShopDrawings.FindAsync(_approvedShopId);
        var pendingDrawing = await _db.ShopDrawings.FindAsync(_pendingIfcShopId);
        Assert.Equal(ShopDrawingStatus.Released, approvedDrawing!.Status);
        Assert.Equal(ShopDrawingStatus.Released, pendingDrawing!.Status);
    }

    [Fact]
    public async Task ReleaseAsync_WithoutItems_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.ReleaseAsync(release.Id, _userId));
    }

    [Fact]
    public async Task ReleaseAsync_WithoutRecipients_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.ReleaseAsync(release.Id, _userId));
    }

    [Fact]
    public async Task ReleaseAsync_Twice_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        await _sut.ReleaseAsync(release.Id, _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.ReleaseAsync(release.Id, _userId));
    }

    // ---------------- Acknowledge ----------------

    [Fact]
    public async Task Acknowledge_BeforeRelease_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        var full = (await _sut.GetAsync(release.Id))!;
        var recipientId = full.Recipients[0].Id;
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.AcknowledgeRecipientAsync(release.Id, recipientId,
                new AcknowledgeIfcReleaseRecipientRequest(), _userId));
    }

    [Fact]
    public async Task Acknowledge_AfterRelease_Succeeds()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        var full = (await _sut.GetAsync(release.Id))!;
        var recipientId = full.Recipients[0].Id;
        await _sut.ReleaseAsync(release.Id, _userId);

        var acked = await _sut.AcknowledgeRecipientAsync(release.Id, recipientId,
            new AcknowledgeIfcReleaseRecipientRequest { AcknowledgementNote = "email confirmed" },
            _userId);
        var recipient = acked.Recipients.First(r => r.Id == recipientId);
        Assert.True(recipient.IsAcknowledged);
        Assert.Equal("email confirmed", recipient.AcknowledgementNote);
    }

    // ---------------- Cancel + delete ----------------

    [Fact]
    public async Task CancelAsync_Draft_MovesToCancelled()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        var cancelled = await _sut.CancelAsync(release.Id, _userId);
        Assert.Equal("Cancelled", cancelled.Status);
    }

    [Fact]
    public async Task CancelAsync_AfterRelease_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        await _sut.ReleaseAsync(release.Id, _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.CancelAsync(release.Id, _userId));
    }

    [Fact]
    public async Task DeleteAsync_Draft_Succeeds()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(release.Id));
    }

    [Fact]
    public async Task DeleteAsync_AfterRelease_Throws()
    {
        var release = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.AddItemsAsync(release.Id, new AddIfcReleaseItemsRequest
        {
            ShopDrawingIds = new List<int> { _approvedShopId },
        }, _userId);
        await _sut.AddRecipientAsync(release.Id, new AddIfcReleaseRecipientRequest
        {
            Name = "ABC",
            RecipientTypeCode = "main-contractor",
        }, _userId);
        await _sut.ReleaseAsync(release.Id, _userId);
        await Assert.ThrowsAsync<IfcReleaseOperationException>(() =>
            _sut.DeleteAsync(release.Id));
    }
}
