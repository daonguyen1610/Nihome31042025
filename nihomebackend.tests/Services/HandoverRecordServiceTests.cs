using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class HandoverRecordServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IBusinessDocumentStorageService> _documentStorage = new();
    private readonly HandoverRecordService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public HandoverRecordServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new HandoverRecordService(
            _db,
            NullLogger<HandoverRecordService>.Instance,
            _documentStorage.Object);
        var user = new ApplicationUser
        {
            PhoneNumber = "0900000144",
            FullName = "Handover Tester",
            Email = "handover.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        var customer = new Customer { Name = "Handover Co", Type = CustomerType.Company };
        _db.AddRange(user, customer);
        _db.SaveChanges();
        var project = new DesignProject
        {
            ProjectCode = "DP-2026-HO-A",
            Name = "Handover fixture",
            CustomerId = customer.Id,
            ProjectManagerUserId = user.Id,
            CurrentStage = DesignProjectStage.Completed,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();
        _userId = user.Id;
        _projectId = project.Id;
    }

    private CreateHandoverRecordRequest Request(bool complete = false) => new()
    {
        DesignProjectId = _projectId,
        Title = "Bàn giao toàn bộ dự án",
        PlannedHandoverDate = new DateOnly(2026, 8, 20),
        ResponsibleUserId = _userId,
        CommissioningCompleted = complete,
        ChecklistItems = [new() { Name = "Kiểm tra vận hành", IsCompleted = complete }],
        Documents = ["/files/handover/minutes.pdf"],
        Signatories = complete ? ["Chủ đầu tư"] : [],
    };

    [Fact]
    public async Task CreateAsync_allocates_code_and_initial_history()
    {
        var created = await _sut.CreateAsync(Request(), _userId, false);

        Assert.Equal("HO-0001", created.HandoverCode);
        Assert.Equal("Draft", created.Status);
        var history = Assert.Single(created.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal("Draft", history.ToStatus);
        Assert.False(created.Readiness.IsReady);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_project_and_unrelated_scope()
    {
        await _sut.CreateAsync(Request(), _userId, false);
        await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.CreateAsync(Request(), _userId, false));

        var outsider = AddUser("0900000145", "outsider@example.com");
        var exception = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.CreateAsync(Request(), outsider.Id, false));
        Assert.Contains("phạm vi", exception.Message);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("ftp://example.com/file.pdf")]
    [InlineData("//example.com/file.pdf")]
    [InlineData("relative.pdf")]
    public async Task CreateAsync_rejects_unsafe_document_urls(string url)
    {
        var request = Request();
        request.Documents = [url];
        await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.CreateAsync(request, _userId, false));
    }

    [Fact]
    public async Task CreateAsync_normalizes_null_collections()
    {
        var request = Request();
        request.ChecklistItems = null!;
        request.Documents = null!;
        request.Signatories = null!;

        var created = await _sut.CreateAsync(request, _userId, false);

        Assert.Empty(created.ChecklistItems);
        Assert.Empty(created.Documents);
        Assert.Empty(created.Signatories);
    }

    [Fact]
    public async Task TransitionAsync_rejects_oversized_note()
    {
        var created = await _sut.CreateAsync(Request(), _userId, false);

        await Assert.ThrowsAsync<HandoverRecordOperationException>(() => _sut.TransitionAsync(
            created.Id,
            new TransitionHandoverStatusRequest { Status = "Cancelled", Note = new string('x', 2001) },
            _userId,
            false));
    }

    [Fact]
    public async Task Responsible_user_cannot_reassign_record_without_project_leadership()
    {
        var responsible = AddUser("0900000147", "responsible.handover@example.com");
        var replacement = AddUser("0900000148", "replacement.handover@example.com");
        var request = Request();
        request.ResponsibleUserId = responsible.Id;
        var created = await _sut.CreateAsync(request, _userId, false);

        var update = new UpdateHandoverRecordRequest
        {
            Title = created.Title,
            PlannedHandoverDate = created.PlannedHandoverDate,
            ResponsibleUserId = replacement.Id,
        };
        var exception = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.UpdateAsync(created.Id, update, responsible.Id, false));

        Assert.Contains("thay đổi người phụ trách", exception.Message);
    }

    [Fact]
    public async Task Ready_transition_requires_all_upstream_conditions()
    {
        var created = await _sut.CreateAsync(Request(complete: true), _userId, false);
        var transition = new TransitionHandoverStatusRequest { Status = "ReadyForHandover" };

        var missingAcceptance = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.TransitionAsync(created.Id, transition, _userId, false));
        Assert.Contains("nghiệm thu", missingAcceptance.Message);

        SeedApprovedAcceptance();
        var missingAsBuilt = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.TransitionAsync(created.Id, transition, _userId, false));
        Assert.Contains("hoàn công", missingAsBuilt.Message);

        SeedApprovedAsBuilt();
        var punch = new PunchItem
        {
            DesignProjectId = _projectId,
            PunchCode = "P-HO-001",
            Title = "Outstanding defect",
            Status = PunchStatus.Open,
        };
        _db.PunchItems.Add(punch);
        _db.SaveChanges();
        var unresolved = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.TransitionAsync(created.Id, transition, _userId, false));
        Assert.Contains("tồn đọng", unresolved.Message);

        punch.Status = PunchStatus.Verified;
        _db.SaveChanges();
        var ready = await _sut.TransitionAsync(created.Id, transition, _userId, false);
        Assert.Equal("ReadyForHandover", ready!.Status);
        Assert.True(ready.Readiness.IsReady);
    }

    [Fact]
    public async Task Complete_uses_dedicated_action_and_requires_signatory()
    {
        SeedReadyUpstream();
        var request = Request(complete: true);
        request.Signatories = [];
        var created = await _sut.CreateAsync(request, _userId, false);
        await _sut.TransitionAsync(created.Id,
            new TransitionHandoverStatusRequest { Status = "ReadyForHandover" }, _userId, false);

        var endpointException = await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.TransitionAsync(created.Id,
                new TransitionHandoverStatusRequest { Status = "HandedOver" }, _userId, false));
        Assert.Contains("/complete", endpointException.Message);
        var historyCountBefore = await _db.Set<HandoverStatusHistory>()
            .CountAsync(history => history.HandoverRecordId == created.Id);
        await Assert.ThrowsAsync<HandoverRecordOperationException>(
            () => _sut.CompleteAsync(created.Id, new TransitionHandoverStatusRequest(), _userId, false));

        _db.ChangeTracker.Clear();
        var unchanged = await _db.HandoverRecords.AsNoTracking()
            .SingleAsync(record => record.Id == created.Id);
        Assert.Equal(HandoverStatus.ReadyForHandover, unchanged.Status);
        Assert.Null(unchanged.ActualHandoverDate);
        Assert.Null(unchanged.HandedOverAt);
        Assert.Null(unchanged.HandedOverByUserId);
        Assert.Equal(0, unchanged.ReopenCount);
        Assert.Equal("[]", unchanged.Signatories);
        Assert.Equal(historyCountBefore, await _db.Set<HandoverStatusHistory>()
            .CountAsync(history => history.HandoverRecordId == created.Id));
    }

    [Fact]
    public async Task Complete_then_reopen_clears_completion_and_increments_counter()
    {
        SeedReadyUpstream();
        var created = await _sut.CreateAsync(Request(complete: true), _userId, false);
        await _sut.TransitionAsync(created.Id,
            new TransitionHandoverStatusRequest { Status = "ReadyForHandover" }, _userId, false);
        var completed = await _sut.CompleteAsync(created.Id,
            new TransitionHandoverStatusRequest { Note = "Signed" }, _userId, false);

        Assert.Equal("HandedOver", completed!.Status);
        Assert.NotNull(completed.ActualHandoverDate);
        Assert.NotNull(completed.HandedOverAt);

        var reopened = await _sut.TransitionAsync(created.Id,
            new TransitionHandoverStatusRequest { Status = "Reopened", Note = "Warranty issue" }, _userId, false);
        Assert.Equal("Reopened", reopened!.Status);
        Assert.Equal(1, reopened.ReopenCount);
        Assert.Null(reopened.ActualHandoverDate);
        Assert.Null(reopened.HandedOverAt);
    }

    [Fact]
    public async Task DeleteAsync_removes_any_status_with_history_and_preserves_scope_and_principals()
    {
        var request = Request();
        request.Documents = ["/files/business-documents/handover/handover.pdf"];
        var created = await _sut.CreateAsync(request, _userId, false);
        var outsider = AddUser("0900000149", "delete.outsider@example.com");
        Assert.False(await _sut.DeleteAsync(created.Id, outsider.Id, false));

        var entity = await _db.HandoverRecords.SingleAsync(record => record.Id == created.Id);
        entity.Status = HandoverStatus.HandedOver;
        await _db.SaveChangesAsync();

        Assert.True(await _sut.DeleteAsync(created.Id, _userId, false));
        Assert.False(await _db.HandoverRecords.AnyAsync(record => record.Id == created.Id));
        Assert.False(await _db.HandoverStatusHistory.AnyAsync(history => history.HandoverRecordId == created.Id));
        Assert.True(await _db.DesignProjects.AnyAsync(project => project.Id == _projectId));
        Assert.True(await _db.Users.AnyAsync(user => user.Id == _userId));
        _documentStorage.Verify(storage => storage.Delete(
            request.Documents[0], BusinessDocumentArea.Handover), Times.Once);
    }

    [Fact]
    public async Task ListAsync_readyOnly_and_scope_use_canonical_readiness()
    {
        SeedReadyUpstream();
        var own = await _sut.CreateAsync(Request(complete: true), _userId, false);
        var scoped = await _sut.ListAsync(new HandoverRecordListParams { ReadyOnly = true }, _userId, false);

        Assert.Single(scoped.Items);
        Assert.Equal(own.Id, scoped.Items[0].Id);
        Assert.Equal(1, scoped.ReadyCount);

        var outsider = AddUser("0900000146", "other.handover@example.com");
        var hidden = await _sut.ListAsync(new HandoverRecordListParams(), outsider.Id, false);
        Assert.Empty(hidden.Items);
        Assert.NotNull(await _sut.GetAsync(own.Id, outsider.Id, true));
    }

    private ApplicationUser AddUser(string phone, string email)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            FullName = "Other User",
            Email = email,
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    private void SeedReadyUpstream()
    {
        SeedApprovedAcceptance();
        SeedApprovedAsBuilt();
    }

    private void SeedApprovedAcceptance()
    {
        _db.AcceptanceRecords.Add(new AcceptanceRecord
        {
            DesignProjectId = _projectId,
            AcceptanceCode = "A-HO-001",
            Title = "Approved partial acceptance",
            AcceptanceDate = new DateOnly(2026, 8, 1),
            Status = AcceptanceStatus.Approved,
        });
        _db.SaveChanges();
    }

    private void SeedApprovedAsBuilt()
    {
        var documents = AsBuiltCategoryExtensions.Required.Select((category, index) => new AsBuiltDocument
        {
            DesignProjectId = _projectId,
            DocumentCode = $"AB-HO-{index + 1:000}",
            Title = category.ToString(),
            Category = category,
            Status = AsBuiltStatus.Approved,
        });
        _db.AsBuiltDocuments.AddRange(documents);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
