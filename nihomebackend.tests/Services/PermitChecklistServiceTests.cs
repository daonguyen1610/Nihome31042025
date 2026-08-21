using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
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
/// Unit coverage for the NIH-137 permit checklist slice — auto-generation
/// against the master-data catalogue, patch semantics + risk-flag rules.
/// </summary>
public class PermitChecklistServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IBusinessDocumentStorageService> _documentStorage = new();
    private readonly PermitChecklistService _sut;
    private readonly int _userId;
    private readonly int _designProjectId;

    public PermitChecklistServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new PermitChecklistService(
            _db,
            _documentStorage.Object,
            NullLogger<PermitChecklistService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000030",
            FullName = "Legal Tester",
            Email = "legal.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);

        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "permit_type", Code = "gpxd", Name = "GPXD", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "permit_type", Code = "pccc", Name = "PCCC", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "permit_type", Code = "electricity", Name = "Cấp điện", IsActive = true, SortOrder = 3 },
            new MasterDataOption { Category = "permit_type", Code = "retired-perm", Name = "Retired", IsActive = false, SortOrder = 4 }
        );

        var customer = new Customer { Name = "PermitCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var dp = new DesignProject
        {
            ProjectCode = "DP-2026-TEST",
            Name = "Test project",
            CustomerId = customer.Id,
        };
        _db.DesignProjects.Add(dp);
        _db.SaveChanges();

        _userId = user.Id;
        _designProjectId = dp.Id;
    }

    public void Dispose() => _db.Dispose();

    // ---------------- EnsureForProjectAsync ----------------

    [Fact]
    public async Task EnsureForProjectAsync_CreatesRowPerActiveTemplate()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var codes = await _db.PermitChecklistItems
            .Where(p => p.DesignProjectId == _designProjectId)
            .Select(p => p.PermitTypeCode)
            .ToListAsync();
        Assert.Equal(3, codes.Count);
        Assert.DoesNotContain("retired-perm", codes);
    }

    [Fact]
    public async Task EnsureForProjectAsync_IsIdempotent()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var count = await _db.PermitChecklistItems.CountAsync(p => p.DesignProjectId == _designProjectId);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task EnsureForProjectAsync_UnknownProject_Throws()
    {
        await Assert.ThrowsAsync<PermitChecklistOperationException>(() =>
            _sut.EnsureForProjectAsync(999999, _userId));
    }

    [Fact]
    public async Task EnsureForProjectAsync_TopUpsAfterTemplateChange()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        // Simulate a new permit type showing up later.
        _db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "permit_type",
            Code = "new-type",
            Name = "New",
            IsActive = true,
            SortOrder = 10,
        });
        await _db.SaveChangesAsync();
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        Assert.Equal(4, await _db.PermitChecklistItems.CountAsync(p => p.DesignProjectId == _designProjectId));
    }

    // ---------------- Create / Delete ----------------

    [Fact]
    public async Task CreateAsync_CreatesRequestedChecklistItem()
    {
        var response = await _sut.CreateAsync(new CreatePermitChecklistItemRequest
        {
            DesignProjectId = _designProjectId,
            PermitTypeCode = "gpxd",
            Status = "Preparing",
            OwnerUserId = _userId,
            IssuingAgency = " Sở Xây dựng ",
        }, _userId);

        Assert.Equal("gpxd", response.PermitTypeCode);
        Assert.Equal("Preparing", response.Status);
        Assert.Equal("Sở Xây dựng", response.IssuingAgency);
        Assert.Equal(_userId, response.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateProjectAndType_Throws()
    {
        var request = new CreatePermitChecklistItemRequest
        {
            DesignProjectId = _designProjectId,
            PermitTypeCode = "gpxd",
        };
        await _sut.CreateAsync(request, _userId);

        await Assert.ThrowsAsync<PermitChecklistDuplicateException>(() =>
            _sut.CreateAsync(request, _userId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesRowAndReturnsSnapshot()
    {
        var created = await _sut.CreateAsync(new CreatePermitChecklistItemRequest
        {
            DesignProjectId = _designProjectId,
            PermitTypeCode = "pccc",
        }, _userId);
        var entity = await _db.PermitChecklistItems.FindAsync(created.Id);
        entity!.SubmittedFilePath = "/files/business-documents/permits/submitted.pdf";
        entity.IssuedFilePath = "/files/business-documents/permits/issued.pdf";
        await _db.SaveChangesAsync();

        var deleted = await _sut.DeleteAsync(created.Id);

        Assert.NotNull(deleted);
        Assert.Equal(created.Id, deleted!.Id);
        Assert.Null(await _db.PermitChecklistItems.FindAsync(created.Id));
        Assert.Null(await _sut.DeleteAsync(created.Id));
        _documentStorage.Verify(storage => storage.Delete(
            "/files/business-documents/permits/submitted.pdf", BusinessDocumentArea.Permits), Times.Once);
        _documentStorage.Verify(storage => storage.Delete(
            "/files/business-documents/permits/issued.pdf", BusinessDocumentArea.Permits), Times.Once);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_UnknownReturnsNull()
    {
        Assert.Null(await _sut.UpdateAsync(999999, new UpdatePermitChecklistItemRequest(), _userId));
    }

    [Fact]
    public async Task UpdateAsync_InvalidStatus_Throws()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var id = _db.PermitChecklistItems.First().Id;
        await Assert.ThrowsAsync<PermitChecklistOperationException>(() =>
            _sut.UpdateAsync(id, new UpdatePermitChecklistItemRequest { Status = "NotAStatus" }, _userId));
    }

    [Fact]
    public async Task UpdateAsync_MoveToIssued_StampsIssuedAt()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var id = _db.PermitChecklistItems.First().Id;
        var resp = await _sut.UpdateAsync(id, new UpdatePermitChecklistItemRequest
        {
            Status = "Issued",
        }, _userId);
        Assert.NotNull(resp);
        Assert.Equal("Issued", resp!.Status);
        Assert.NotNull(resp.IssuedAt);
    }

    [Fact]
    public async Task UpdateAsync_UnknownOwner_Throws()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var id = _db.PermitChecklistItems.First().Id;
        await Assert.ThrowsAsync<PermitChecklistOperationException>(() =>
            _sut.UpdateAsync(id, new UpdatePermitChecklistItemRequest { OwnerUserId = 999999 }, _userId));
    }

    [Fact]
    public async Task UpdateAsync_ClearOwner_ClearsPreviouslyAssigned()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var id = _db.PermitChecklistItems.First().Id;
        await _sut.UpdateAsync(id, new UpdatePermitChecklistItemRequest { OwnerUserId = _userId }, _userId);
        var resp = await _sut.UpdateAsync(id, new UpdatePermitChecklistItemRequest { ClearOwner = true }, _userId);
        Assert.NotNull(resp);
        Assert.Null(resp!.OwnerUserId);
    }

    [Fact]
    public async Task UploadDocumentAsync_StoresAndAssignsRequestedDocumentKind()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var id = _db.PermitChecklistItems.First().Id;
        var entity = await _db.PermitChecklistItems.FindAsync(id);
        entity!.SubmittedFilePath = "/files/business-documents/permits/old.pdf";
        await _db.SaveChangesAsync();
        var file = new FormFile(new MemoryStream("permit"u8.ToArray()), 0, 6, "file", "permit.pdf");
        _documentStorage
            .Setup(storage => storage.StoreAsync(file, BusinessDocumentArea.Permits, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BusinessDocumentUploadResponse { Path = "/files/business-documents/permits/permit.pdf" });

        var response = await _sut.UploadDocumentAsync(
            id, PermitDocumentKind.SubmittedPackage, file, _userId);

        Assert.NotNull(response);
        Assert.Equal("/files/business-documents/permits/permit.pdf", response!.SubmittedFilePath);
        Assert.Null(response.IssuedFilePath);
        _documentStorage.Verify(storage => storage.Delete(
            "/files/business-documents/permits/old.pdf", BusinessDocumentArea.Permits), Times.Once);
    }

    // ---------------- Risk flags ----------------

    [Fact]
    public async Task ListAsync_ComputesRiskAggregates()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var items = await _db.PermitChecklistItems.ToListAsync();
        var now = DateTime.UtcNow;
        items[0].TargetDeadline = now.AddDays(-1);   // overdue
        items[1].TargetDeadline = now.AddDays(3);    // due soon
        items[2].Status = PermitStatus.Issued;
        items[2].IssuedAt = now.AddDays(-10);
        items[2].ExpiresAt = now.AddDays(20);        // expiring soon
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(new PermitChecklistListParams { PageSize = 100 });
        Assert.Equal(1, list.Risk.Overdue);
        Assert.Equal(1, list.Risk.DueSoon);
        Assert.Equal(1, list.Risk.ExpiringSoon);
        Assert.Equal(2, list.Risk.TotalOpen); // items 0 + 1 (item 2 is Issued)
    }

    [Fact]
    public async Task ListAsync_OverdueFilter_RestrictsToOverdueOpenRows()
    {
        await _sut.EnsureForProjectAsync(_designProjectId, _userId);
        var items = await _db.PermitChecklistItems.ToListAsync();
        var now = DateTime.UtcNow;
        items[0].TargetDeadline = now.AddDays(-1); // overdue
        items[1].TargetDeadline = now.AddDays(-2);
        items[1].Status = PermitStatus.Issued;      // overdue but Issued → excluded
        items[2].TargetDeadline = now.AddDays(3);
        await _db.SaveChangesAsync();

        var list = await _sut.ListAsync(new PermitChecklistListParams { Overdue = true, PageSize = 100 });
        Assert.Single(list.Items);
        Assert.Equal(items[0].Id, list.Items[0].Id);
    }
}
