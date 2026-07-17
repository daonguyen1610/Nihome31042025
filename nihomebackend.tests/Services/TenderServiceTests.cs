using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class TenderServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications;
    private readonly TenderService _sut;
    private readonly int _customerId;
    private readonly int _userId;

    public TenderServiceTests()
    {
        _db = DbContextFactory.Create();
        _notifications = new Mock<INotificationService>();
        _sut = new TenderService(_db, _notifications.Object, NullLogger<TenderService>.Instance);

        // Seed a bare-minimum user + customer + master-data checklist so
        // the create-path can succeed without touching production seeders.
        var user = new ApplicationUser
        {
            PhoneNumber = "0900000000",
            FullName = "Sale Tester",
            Email = "sale.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        var customer = new Customer { Name = "ACME Corp", Type = CustomerType.Company, SourceCode = "referral" };
        _db.Customers.Add(customer);
        _db.MasterDataOptions.AddRange(
            new MasterDataOption { Category = "tender_checklist_default", Code = "capability", Name = "Hồ sơ năng lực", IsActive = true, SortOrder = 1 },
            new MasterDataOption { Category = "tender_checklist_default", Code = "legal", Name = "Hồ sơ pháp nhân", IsActive = true, SortOrder = 2 },
            new MasterDataOption { Category = "tender_checklist_default", Code = "boq", Name = "BOQ", IsActive = true, SortOrder = 3 },
            new MasterDataOption { Category = "tender_checklist_default", Code = "inactive", Name = "Skipped", IsActive = false, SortOrder = 9 }
        );
        _db.SaveChanges();
        _customerId = customer.Id;
        _userId = user.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateTenderRequest ValidCreate(DateTime? deadline = null) => new()
    {
        Name = "Gói thầu số 1",
        CustomerId = _customerId,
        OpeningDate = DateTime.UtcNow.AddDays(7),
        SubmissionDeadline = deadline ?? DateTime.UtcNow.AddDays(14),
        PreparerUserId = _userId,
        InfoSource = "Website",
        Note = "Ghi chú",
    };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesCodeAndDefaultChecklist()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);

        Assert.StartsWith($"TD-{DateTime.UtcNow.Year}-", resp.Code);
        Assert.EndsWith("-0001", resp.Code);
        Assert.Equal("Preparing", resp.Status);
        // Should have 3 checklist items (only active master-data rows).
        Assert.Equal(3, resp.ChecklistItems.Count);
        Assert.Equal(0, resp.ChecklistCompletionPercent);
        Assert.False(resp.IsDeadlineImminent); // 14 days out
    }

    [Fact]
    public async Task CreateAsync_DeadlineInPast_Throws()
    {
        await Assert.ThrowsAsync<TenderOperationException>(() =>
            _sut.CreateAsync(ValidCreate(deadline: DateTime.UtcNow.AddDays(-1)), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownCustomer_Throws()
    {
        var req = ValidCreate();
        req.CustomerId = 9999;
        await Assert.ThrowsAsync<TenderOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownPreparer_Throws()
    {
        var req = ValidCreate();
        req.PreparerUserId = 9999;
        await Assert.ThrowsAsync<TenderOperationException>(() => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task CreateAsync_AllocatesSequentialCodesPerYear()
    {
        var a = await _sut.CreateAsync(ValidCreate(), _userId);
        var b = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.EndsWith("-0001", a.Code);
        Assert.EndsWith("-0002", b.Code);
    }

    [Fact]
    public async Task CreateAsync_NotifiesAssignedPreparer()
    {
        await _sut.CreateAsync(ValidCreate(), _userId);
        _notifications.Verify(n => n.CreateAsync(_userId, "crm.tenders.assigned",
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ImmimentDeadlineFlagged()
    {
        var resp = await _sut.CreateAsync(ValidCreate(deadline: DateTime.UtcNow.AddDays(2)), _userId);
        Assert.True(resp.IsDeadlineImminent);
    }

    [Fact]
    public async Task CreateAsync_WithoutPreparer_DefaultsToCaller()
    {
        var req = ValidCreate();
        req.PreparerUserId = null;

        var resp = await _sut.CreateAsync(req, _userId);

        Assert.Equal(_userId, resp.PreparerUserId);
    }

    // ---------------- Update ----------------

    [Fact]
    public async Task UpdateAsync_WhilePreparing_UpdatesAllEditableFields()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var newDeadline = DateTime.UtcNow.AddDays(30);
        var updated = await _sut.UpdateAsync(created.Id, new UpdateTenderRequest
        {
            Name = "Gói thầu số 1 - v2",
            OpeningDate = DateTime.UtcNow.AddDays(21),
            SubmissionDeadline = newDeadline,
            PreparerUserId = _userId,
            InfoSource = "Referral",
            Note = "Updated note",
        }, _userId);

        Assert.NotNull(updated);
        Assert.Equal("Gói thầu số 1 - v2", updated!.Name);
        Assert.Equal("Updated note", updated.Note);
        Assert.Equal(newDeadline, updated.SubmissionDeadline, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpdateAsync_AfterSubmitted_OnlyNoteChanges()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        // Simulate a submitted tender.
        var raw = await _db.Tenders.FirstAsync(t => t.Id == created.Id);
        raw.Status = TenderStatus.Submitted;
        await _db.SaveChangesAsync();

        var futureDeadline = DateTime.UtcNow.AddDays(30);
        var updated = await _sut.UpdateAsync(created.Id, new UpdateTenderRequest
        {
            Name = "Bị bỏ qua",
            SubmissionDeadline = futureDeadline,
            PreparerUserId = null,
            InfoSource = "Bị bỏ qua",
            Note = "Only this changes",
        }, _userId);

        Assert.NotNull(updated);
        Assert.Equal(created.Name, updated!.Name);                        // name unchanged
        Assert.Equal(created.SubmissionDeadline, updated.SubmissionDeadline); // deadline unchanged
        Assert.Equal(created.PreparerUserId, updated.PreparerUserId);
        Assert.Equal(created.InfoSource, updated.InfoSource);
        Assert.Equal("Only this changes", updated.Note);
    }

    [Fact]
    public async Task UpdateAsync_ReassigningPreparer_FiresNotification()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        _notifications.Invocations.Clear();

        // Create a second user to reassign to.
        var newUser = new ApplicationUser
        {
            PhoneNumber = "0900000001",
            FullName = "Other Sale",
            Email = "other.sale@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(created.Id, new UpdateTenderRequest
        {
            Name = created.Name,
            SubmissionDeadline = created.SubmissionDeadline,
            PreparerUserId = newUser.Id,
            Note = created.Note,
        }, _userId);

        _notifications.Verify(n => n.CreateAsync(newUser.Id, "crm.tenders.assigned",
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNull()
    {
        var updated = await _sut.UpdateAsync(999, new UpdateTenderRequest
        {
            Name = "x",
            SubmissionDeadline = DateTime.UtcNow.AddDays(5),
        }, _userId);
        Assert.Null(updated);
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_WhilePreparing_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(created.Id));
        Assert.False(await _db.Tenders.AnyAsync(t => t.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_AfterSubmitted_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        var raw = await _db.Tenders.FirstAsync(t => t.Id == created.Id);
        raw.Status = TenderStatus.Submitted;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<TenderOperationException>(() => _sut.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ReturnsFalse()
    {
        Assert.False(await _sut.DeleteAsync(999));
    }

    // ---------------- List ----------------

    [Fact]
    public async Task ListAsync_DefaultsToDeadlineAscending()
    {
        var a = await _sut.CreateAsync(ValidCreate(deadline: DateTime.UtcNow.AddDays(20)), _userId);
        var b = await _sut.CreateAsync(ValidCreate(deadline: DateTime.UtcNow.AddDays(5)), _userId);

        var list = await _sut.ListAsync(new TenderListParams { PageSize = 50 });
        Assert.Equal(2, list.Total);
        Assert.Equal(b.Id, list.Items[0].Id); // shorter deadline first
        Assert.Equal(a.Id, list.Items[1].Id);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndSearch()
    {
        await _sut.CreateAsync(ValidCreate(), _userId);
        var deadline = DateTime.UtcNow.AddDays(30);
        await _sut.CreateAsync(new CreateTenderRequest
        {
            Name = "Nhà máy Alpha",
            CustomerId = _customerId,
            SubmissionDeadline = deadline,
        }, _userId);

        var searched = await _sut.ListAsync(new TenderListParams { Search = "Alpha" });
        Assert.Single(searched.Items);
        Assert.Contains("Alpha", searched.Items[0].Name);

        var preparing = await _sut.ListAsync(new TenderListParams { Status = "Preparing" });
        Assert.Equal(2, preparing.Total);

        var submitted = await _sut.ListAsync(new TenderListParams { Status = "Submitted" });
        Assert.Equal(0, submitted.Total);
    }
}
