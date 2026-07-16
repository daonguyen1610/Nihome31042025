using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class QuoteServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications;
    private readonly QuoteService _sut;

    public QuoteServiceTests()
    {
        _db = DbContextFactory.Create();
        _notifications = new Mock<INotificationService>();
        _sut = new QuoteService(
            _db,
            _notifications.Object,
            NullLogger<QuoteService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_WithoutManagePermission_Throws()
    {
        var (user, opp) = await SeedOpportunityAsync();

        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: false));
        Assert.Contains("quyền", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_UnitCost_ComputesTotalsAndAssignsCode()
    {
        var (user, opp) = await SeedOpportunityAsync();

        var resp = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 10_000_000m,
            DiscountPercent = 10m,
            VatPercent = 8m,
        }, user.Id, canManage: true);

        Assert.Equal("QT-" + DateTime.UtcNow.Year + "-0001", resp.Code);
        Assert.Equal(1_000_000_000m, resp.Subtotal);            // 100 × 10,000,000
        // afterDiscount = 900,000,000 ; vat 8% = 72,000,000 → grand 972,000,000
        Assert.Equal(972_000_000m, resp.GrandTotal);
        Assert.Equal("Draft", resp.Status);
        Assert.Equal(1, resp.Version);
        Assert.False(string.IsNullOrWhiteSpace(resp.GrandTotalInWords));
    }

    [Fact]
    public async Task CreateAsync_Boq_SumsItemAmountsAndRoundsCorrectly()
    {
        var (user, opp) = await SeedOpportunityAsync();

        var resp = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.Boq,
            DiscountPercent = 0m,
            VatPercent = 10m,
            Items = new()
            {
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 12m, UnitPrice = 1_200_000m },
                new QuoteItemInput { Name = "Cốt thép", Unit = "kg", Quantity = 850m, UnitPrice = 25_000m },
            },
        }, user.Id, canManage: true);

        // 12 × 1.2M = 14.4M ; 850 × 25k = 21.25M ; subtotal = 35.65M ; vat 10% → grand 39.215M
        Assert.Equal(35_650_000m, resp.Subtotal);
        Assert.Equal(39_215_000m, resp.GrandTotal);
        Assert.Equal(2, resp.Items.Count);
        Assert.Equal(14_400_000m, resp.Items[0].Amount);
    }

    [Fact]
    public async Task CreateAsync_Boq_WithoutItems_Throws()
    {
        var (user, opp) = await SeedOpportunityAsync();

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.Boq,
            Items = new(),
        }, user.Id, canManage: true));
    }

    // ---------------- Workflow state machine ----------------

    [Fact]
    public async Task Submit_FromDraft_MovesToPendingApproval()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();

        var resp = await _sut.SubmitAsync(quote.Id, new(), user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(resp);
        Assert.Equal("PendingApproval", resp!.Status);
        Assert.NotNull(resp.SubmittedAt);
    }

    [Fact]
    public async Task Submit_FromNonDraft_Throws()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        await _sut.SubmitAsync(quote.Id, new(), user.Id, true, true);

        await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.SubmitAsync(quote.Id, new(), user.Id, true, true));
    }

    [Fact]
    public async Task Approve_WithoutApprovePermission_Throws()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        await _sut.SubmitAsync(quote.Id, new(), user.Id, true, true);

        await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.ApproveAsync(quote.Id, new(), user.Id, canApprove: false));
    }

    [Fact]
    public async Task FullHappyPath_DraftToCustomerApproved()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();

        await _sut.SubmitAsync(quote.Id, new(), user.Id, true, true);
        await _sut.ApproveAsync(quote.Id, new(), user.Id, canApprove: true);
        await _sut.SendToCustomerAsync(quote.Id, new(), user.Id, canSend: true, canSeeAll: true);
        var final = await _sut.MarkCustomerApprovedAsync(quote.Id, new(), user.Id, true, true);

        Assert.NotNull(final);
        Assert.Equal("CustomerApproved", final!.Status);
        Assert.NotNull(final.ClosedAt);
        // Approval log has at least 5 entries: create + submit + approve + send + customer-approve.
        Assert.True(final.ApprovalLogs.Count >= 5);
    }

    // ---------------- Versioning ----------------

    [Fact]
    public async Task UpdateAsync_AfterApproved_BumpsVersionAndSnapshotsPrevious()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        await _sut.SubmitAsync(quote.Id, new(), user.Id, true, true);
        await _sut.ApproveAsync(quote.Id, new(), user.Id, canApprove: true);

        var updated = await _sut.UpdateAsync(quote.Id, new UpdateQuoteRequest
        {
            AreaSqm = 200m,
            UnitPricePerSqm = 10_000_000m,
            DiscountPercent = 0m,
            VatPercent = 8m,
        }, user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Version);
        Assert.Equal("Draft", updated.Status);

        var versions = await _sut.GetVersionsAsync(quote.Id, user.Id, canSeeAll: true);
        Assert.NotNull(versions);
        Assert.Equal(2, versions!.Versions.Count);
        Assert.True(versions.Versions.Single(v => v.Version == 1).IsCurrent == false);
        Assert.True(versions.Versions.Single(v => v.Version == 2).IsCurrent);
    }

    // ---------------- Ownership scoping ----------------

    [Fact]
    public async Task GetAsync_Sales_CannotSeeOtherSalesQuote()
    {
        var (owner, quote) = await SeedApprovedReadyQuoteAsync();
        var stranger = await SeedUserAsync();

        var mine = await _sut.GetAsync(quote.Id, owner.Id, canSeeAll: false);
        var theirs = await _sut.GetAsync(quote.Id, stranger.Id, canSeeAll: false);

        Assert.NotNull(mine);
        Assert.Null(theirs);
    }

    [Fact]
    public async Task DeleteAsync_NonDraft_Throws()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        await _sut.SubmitAsync(quote.Id, new(), user.Id, true, true);

        await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.DeleteAsync(quote.Id, user.Id, canManage: true, canSeeAll: true));
    }

    // =========================== Helpers ===========================

    private static CreateQuoteRequest NewUnitCostRequest(int oppId) => new()
    {
        OpportunityId = oppId,
        Method = QuoteMethod.UnitCost,
        AreaSqm = 100m,
        UnitPricePerSqm = 5_000_000m,
        VatPercent = 8m,
    };

    private async Task<ApplicationUser> SeedUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser
        {
            PhoneNumber = suffix,
            PasswordHash = "hash",
            Role = UserRole.USER,
            IsActive = true,
            Email = $"user-{suffix}@nihome.test",
            FullName = $"User {suffix[..4]}",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<(ApplicationUser owner, Opportunity opp)> SeedOpportunityAsync()
    {
        var user = await SeedUserAsync();
        var cust = new Customer
        {
            Name = "Test KH " + Guid.NewGuid().ToString("N")[..6],
            Type = CustomerType.Individual,
            SourceCode = "marketing",
            OwnerUserId = user.Id,
            CreatedByUserId = user.Id,
        };
        _db.Customers.Add(cust);
        await _db.SaveChangesAsync();

        var opp = new Opportunity
        {
            Name = "Opp " + Guid.NewGuid().ToString("N")[..6],
            CustomerId = cust.Id,
            OwnerUserId = user.Id,
            EstimatedValue = 1_000_000_000m,
            WinProbability = 30,
            Stage = OpportunityStage.Proposal,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id,
        };
        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        return (user, opp);
    }

    private async Task<(ApplicationUser owner, Quote quote)> SeedApprovedReadyQuoteAsync()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var created = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 10_000_000m,
            DiscountPercent = 0m,
            VatPercent = 8m,
        }, user.Id, canManage: true);

        var quote = await _db.Quotes.FirstAsync(q => q.Id == created.Id);
        return (user, quote);
    }
}
