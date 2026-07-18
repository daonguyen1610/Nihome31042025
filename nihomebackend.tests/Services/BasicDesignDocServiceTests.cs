using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

/// <summary>
/// Unit coverage for the NIH-115 Basic Design workflow: per-discipline
/// code allocation, status state-machine enforcement, and the 3-discipline
/// readiness gate that unlocks the Shop Drawing stage.
/// </summary>
public class BasicDesignDocServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly BasicDesignDocService _sut;
    private readonly int _userId;
    private readonly int _projectId;

    public BasicDesignDocServiceTests()
    {
        _db = DbContextFactory.Create();
        _sut = new BasicDesignDocService(_db, NullLogger<BasicDesignDocService>.Instance);

        var user = new ApplicationUser
        {
            PhoneNumber = "0900000050",
            FullName = "Design Tester",
            Email = "basic.test@example.com",
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

        var customer = new Customer { Name = "BasicCo", Type = CustomerType.Company };
        _db.Customers.Add(customer);
        _db.SaveChanges();

        var project = new DesignProject
        {
            ProjectCode = "DP-2026-BD-TEST",
            Name = "Basic design fixture",
            CustomerId = customer.Id,
            CurrentStage = DesignProjectStage.BasicDesign,
        };
        _db.DesignProjects.Add(project);
        _db.SaveChanges();

        _userId = user.Id;
        _projectId = project.Id;
    }

    public void Dispose() => _db.Dispose();

    private CreateBasicDesignDocRequest ValidCreate(
        string? title = null,
        string discipline = "architecture",
        int? projectId = null) => new()
        {
            DesignProjectId = projectId ?? _projectId,
            DisciplineCode = discipline,
            Title = title ?? "Test drawing",
        };

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_AllocatesPrefixedCode()
    {
        var resp = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.StartsWith("KT-BD-", resp.DocumentCode);
        Assert.EndsWith("-001", resp.DocumentCode);
        Assert.Equal("InProgress", resp.Status);
    }

    [Fact]
    public async Task CreateAsync_SequentialCodesPerDiscipline()
    {
        var a = await _sut.CreateAsync(ValidCreate(title: "A", discipline: "architecture"), _userId);
        var b = await _sut.CreateAsync(ValidCreate(title: "B", discipline: "architecture"), _userId);
        var s = await _sut.CreateAsync(ValidCreate(title: "S", discipline: "structure"), _userId);
        Assert.EndsWith("-001", a.DocumentCode);
        Assert.EndsWith("-002", b.DocumentCode);
        Assert.EndsWith("-001", s.DocumentCode);
        Assert.StartsWith("KC-BD-", s.DocumentCode);
    }

    [Fact]
    public async Task CreateAsync_MissingTitle_Throws()
    {
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.CreateAsync(ValidCreate(title: "   "), _userId));
    }

    [Fact]
    public async Task CreateAsync_UnknownDiscipline_Throws()
    {
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.CreateAsync(ValidCreate(discipline: "not-a-discipline"), _userId));
    }

    [Fact]
    public async Task CreateAsync_ProjectNotInBasicStage_Throws()
    {
        var dp = await _db.DesignProjects.FirstAsync(x => x.Id == _projectId);
        dp.CurrentStage = DesignProjectStage.Concept;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.CreateAsync(ValidCreate(), _userId));
    }

    // ---------------- Transition ----------------

    [Fact]
    public async Task TransitionStatus_InvalidJump_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        // InProgress → InternallyApproved is not allowed; must go via SubmittedForReview.
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.TransitionStatusAsync(created.Id,
                new TransitionBasicDesignDocStatusRequest { Status = "InternallyApproved" }, _userId));
    }

    [Fact]
    public async Task TransitionStatus_HappyPathToInternallyApproved()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.TransitionStatusAsync(created.Id,
            new TransitionBasicDesignDocStatusRequest { Status = "SubmittedForReview" }, _userId);
        var approved = await _sut.TransitionStatusAsync(created.Id,
            new TransitionBasicDesignDocStatusRequest { Status = "InternallyApproved" }, _userId);
        Assert.NotNull(approved);
        Assert.Equal("InternallyApproved", approved!.Status);
    }

    // ---------------- Readiness / unlock ----------------

    [Fact]
    public async Task Readiness_NotReady_WhenMissingDiscipline()
    {
        await ApproveDocAsync("architecture");
        await ApproveDocAsync("structure");
        // MEP intentionally missing.
        var list = await _sut.ListAsync(new BasicDesignDocListParams { DesignProjectId = _projectId });
        Assert.False(list.Readiness.ReadyForShopDrawing);
        Assert.Contains("architecture", list.Readiness.InternallyApprovedDisciplineCodes);
        Assert.Contains("structure", list.Readiness.InternallyApprovedDisciplineCodes);
        Assert.DoesNotContain("mep", list.Readiness.InternallyApprovedDisciplineCodes);
    }

    [Fact]
    public async Task Readiness_Ready_WhenAll3DisciplinesApproved()
    {
        await ApproveDocAsync("architecture");
        await ApproveDocAsync("structure");
        await ApproveDocAsync("mep");
        var list = await _sut.ListAsync(new BasicDesignDocListParams { DesignProjectId = _projectId });
        Assert.True(list.Readiness.ReadyForShopDrawing);
    }

    [Fact]
    public async Task UnlockShopDrawing_Blocked_WhenNotReady()
    {
        await ApproveDocAsync("architecture");
        await ApproveDocAsync("structure");
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.UnlockShopDrawingAsync(_projectId, _userId));
    }

    [Fact]
    public async Task UnlockShopDrawing_Succeeds_WhenReady_AndMovesStage()
    {
        await ApproveDocAsync("architecture");
        await ApproveDocAsync("structure");
        await ApproveDocAsync("mep");

        var resp = await _sut.UnlockShopDrawingAsync(_projectId, _userId);
        Assert.Equal("ShopDrawing", resp.CurrentStage);

        var project = await _db.DesignProjects.FindAsync(_projectId);
        Assert.Equal(DesignProjectStage.ShopDrawing, project!.CurrentStage);
    }

    [Fact]
    public async Task UnlockShopDrawing_Blocked_WhenProjectNotAtBasicStage()
    {
        var dp = await _db.DesignProjects.FirstAsync(x => x.Id == _projectId);
        dp.CurrentStage = DesignProjectStage.Concept;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.UnlockShopDrawingAsync(_projectId, _userId));
    }

    // ---------------- Delete ----------------

    [Fact]
    public async Task DeleteAsync_InProgress_Succeeds()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        Assert.True(await _sut.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_AfterReview_Throws()
    {
        var created = await _sut.CreateAsync(ValidCreate(), _userId);
        await _sut.TransitionStatusAsync(created.Id,
            new TransitionBasicDesignDocStatusRequest { Status = "SubmittedForReview" }, _userId);
        await Assert.ThrowsAsync<BasicDesignDocOperationException>(() =>
            _sut.DeleteAsync(created.Id));
    }

    // ---------------- helpers ----------------

    private async Task ApproveDocAsync(string discipline)
    {
        var doc = await _sut.CreateAsync(ValidCreate(discipline: discipline), _userId);
        await _sut.TransitionStatusAsync(doc.Id,
            new TransitionBasicDesignDocStatusRequest { Status = "SubmittedForReview" }, _userId);
        await _sut.TransitionStatusAsync(doc.Id,
            new TransitionBasicDesignDocStatusRequest { Status = "InternallyApproved" }, _userId);
    }
}
