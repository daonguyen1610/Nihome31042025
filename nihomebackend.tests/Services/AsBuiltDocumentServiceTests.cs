using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-145 as-built dossier service —
/// state machine, code allocation, completeness roll-up, update
/// lock on Approved/Archived, and bulk-delete rules.
/// </summary>
public class AsBuiltDocumentServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AsBuiltDocumentService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public AsBuiltDocumentServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new AsBuiltDocumentService(_db, NullLogger<AsBuiltDocumentService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000145",
            FullName = "As-built Tester",
            Email = "asbuilt.test@example.com",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        var customer = new Customer { Name = "AbCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-AB-A",
            Name = "As-built fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.ShopDrawing,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
    }

    private CreateAsBuiltDocumentRequest Req(string? title = null, string category = "Drawing")
        => new()
        {
            DesignProjectId = _projectId,
            Title = title ?? "Bản vẽ hoàn công",
            Category = category,
        };

    [Fact]
    public async Task CreateAsync_allocates_sequential_code()
    {
        var a = await _sut.CreateAsync(Req("A"), _userId);
        var b = await _sut.CreateAsync(Req("B"), _userId);
        var c = await _sut.CreateAsync(Req("C"), _userId);
        Assert.Equal("AB-001", a.DocumentCode);
        Assert.Equal("AB-002", b.DocumentCode);
        Assert.Equal("AB-003", c.DocumentCode);
        Assert.Equal("Draft", a.Status);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_title()
    {
        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.CreateAsync(Req(title: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_category()
    {
        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.CreateAsync(Req(category: "NotAThing"), _userId));
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_project()
    {
        var req = Req("X");
        req.DesignProjectId = 9999;
        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.CreateAsync(req, _userId));
    }

    [Fact]
    public async Task Transition_walks_draft_submitted_approved_archived()
    {
        var a = await _sut.CreateAsync(Req("Full flow"), _userId);
        var sub = await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        Assert.Equal("Submitted", sub!.Status);
        Assert.NotNull(sub.SubmittedAt);

        var app = await _sut.ApproveAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Approved", Note = "OK" }, _userId);
        Assert.Equal("Approved", app!.Status);
        Assert.Equal("OK", app.Note);

        var arch = await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Archived" }, _userId);
        Assert.Equal("Archived", arch!.Status);
        Assert.NotNull(arch.ArchivedAt);
    }

    [Fact]
    public async Task Transition_status_endpoint_refuses_Approved()
    {
        var a = await _sut.CreateAsync(Req("X"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        var ex = await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId));
        Assert.Contains("/approve", ex.Message);
    }

    [Fact]
    public async Task Approved_to_Draft_clears_approval_signature()
    {
        var a = await _sut.CreateAsync(Req("Rev"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);
        var revised = await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Draft" }, _userId);
        Assert.Equal("Draft", revised!.Status);
        Assert.Null(revised.ApprovedAt);
        Assert.Null(revised.ApprovedByUserId);
    }

    [Fact]
    public async Task Archived_is_terminal()
    {
        var a = await _sut.CreateAsync(Req("Term"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Archived" }, _userId);
        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Draft" }, _userId));
    }

    [Fact]
    public async Task Cancelled_can_be_restored_to_Draft()
    {
        var a = await _sut.CreateAsync(Req("Restore"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Cancelled" }, _userId);
        var back = await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Draft" }, _userId);
        Assert.Equal("Draft", back!.Status);
    }

    [Fact]
    public async Task UpdateAsync_locked_after_approved()
    {
        var a = await _sut.CreateAsync(Req("Lock"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);

        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(
            () => _sut.UpdateAsync(a.Id, new UpdateAsBuiltDocumentRequest
            {
                Title = "New",
                Category = "Drawing",
            }, _userId));
    }

    [Fact]
    public async Task DeleteAsync_only_draft_or_cancelled()
    {
        var a = await _sut.CreateAsync(Req("Del"), _userId);
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await Assert.ThrowsAsync<AsBuiltDocumentOperationException>(() => _sut.DeleteAsync(a.Id));

        // Revise back to Draft — now delete works.
        await _sut.TransitionAsync(a.Id, new TransitionAsBuiltStatusRequest { Status = "Draft" }, _userId);
        Assert.True(await _sut.DeleteAsync(a.Id));
    }

    [Fact]
    public async Task BulkDelete_skips_non_deletable_statuses()
    {
        var a1 = await _sut.CreateAsync(Req("Bulk1"), _userId);
        var a2 = await _sut.CreateAsync(Req("Bulk2"), _userId);
        await _sut.TransitionAsync(a2.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a2.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);

        var res = await _sut.BulkDeleteAsync(new BulkDeleteAsBuiltDocumentsRequest { Ids = new List<int> { a1.Id, a2.Id } });
        Assert.Contains(a1.Id, res.DeletedIds);
        Assert.Contains(a2.Id, res.SkippedIds);
    }

    [Fact]
    public async Task ListAsync_completeness_reflects_approved_or_archived_categories()
    {
        // Approve a Drawing + a TestReport, leave others out.
        var draw = await _sut.CreateAsync(Req("Draw", "Drawing"), _userId);
        await _sut.TransitionAsync(draw.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(draw.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);

        var test = await _sut.CreateAsync(Req("Test", "TestReport"), _userId);
        await _sut.TransitionAsync(test.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(test.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);

        // A Draft AcceptanceMinute — doesn't count.
        await _sut.CreateAsync(Req("Minute-draft", "AcceptanceMinute"), _userId);

        var list = await _sut.ListAsync(new AsBuiltDocumentListParams { DesignProjectId = _projectId });
        // Required = 4 (Drawing, AcceptanceMinute, TestReport, WarrantyCertificate).
        Assert.Equal(4, list.TotalRequiredCategories);
        Assert.Equal(2, list.CompletedRequiredCategories);
    }

    [Fact]
    public async Task ListAsync_openOnly_returns_only_draft_or_submitted()
    {
        var a1 = await _sut.CreateAsync(Req("Draft"), _userId);
        var a2 = await _sut.CreateAsync(Req("Done"), _userId);
        await _sut.TransitionAsync(a2.Id, new TransitionAsBuiltStatusRequest { Status = "Submitted" }, _userId);
        await _sut.ApproveAsync(a2.Id, new TransitionAsBuiltStatusRequest { Status = "Approved" }, _userId);

        var list = await _sut.ListAsync(new AsBuiltDocumentListParams { DesignProjectId = _projectId, OpenOnly = true });
        Assert.Single(list.Items);
        Assert.Equal(a1.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task ListAsync_search_matches_title_or_code()
    {
        await _sut.CreateAsync(Req("Ban ve kien truc"), _userId);
        await _sut.CreateAsync(Req("Bao cao thi nghiem"), _userId);

        var byTitle = await _sut.ListAsync(new AsBuiltDocumentListParams { DesignProjectId = _projectId, Search = "kien truc" });
        Assert.Single(byTitle.Items);

        var byCode = await _sut.ListAsync(new AsBuiltDocumentListParams { DesignProjectId = _projectId, Search = "AB-002" });
        Assert.Single(byCode.Items);
    }

    public void Dispose() => _db.Dispose();
}
