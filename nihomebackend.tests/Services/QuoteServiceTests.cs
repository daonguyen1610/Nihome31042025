using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.HardDelete;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class QuoteServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications;
    private readonly QuoteService _sut;
    private readonly ICrmHardDeletePlanService _hardDeletePlans;
    private readonly int _rateCatalogId;
    private readonly DateOnly _pricingDate = new(2026, 9, 1);

    public QuoteServiceTests()
    {
        _db = DbContextFactory.Create();
        _notifications = new Mock<INotificationService>();
        var hardDelete = HardDeleteTestServices.Create(
            _db, Mock.Of<IProjectDocumentStagingService>());
        _hardDeletePlans = hardDelete.CrmPlans;
        _sut = new QuoteService(
            _db,
            _notifications.Object,
            Mock.Of<IQuotePdfService>(),
            NullLogger<QuoteService>.Instance,
            hardDelete.CrmPlans,
            hardDelete.Operations);
        var catalog = new MaterialRateCatalog
        {
            Code = "RATE-TEST",
            Name = "Test rate",
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        var revision = new MaterialRateRevision
        {
            Catalog = catalog,
            Version = 1,
            Status = MaterialRateRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            Lines =
            [
                new MaterialRateLine
                {
                    MaterialCode = "PACKAGE",
                    MaterialName = "Gói tiêu chuẩn",
                    Unit = "m2",
                    AmountPerSqm = 10_000_000m,
                },
            ],
        };
        _db.MaterialRateCatalogs.Add(catalog);
        _db.MaterialRateRevisions.Add(revision);
        _db.SaveChanges();
        _rateCatalogId = catalog.Id;
    }

    public void Dispose() => _db.Dispose();

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_RejectsQuoteOnLostOpportunity()
    {
        var (user, opp) = await SeedOpportunityAsync(OpportunityStage.Lost);

        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: true));

        Assert.Contains("Lost", ex.Message);
        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task CreateAsync_OnWonOpportunity_CreatesAndLinksWinningQuote()
    {
        var (user, opp) = await SeedOpportunityAsync(OpportunityStage.Won);

        var created = await _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: true);

        Assert.Single(_db.Quotes);
        var savedOpportunity = await _db.Opportunities.SingleAsync(o => o.Id == opp.Id);
        Assert.Equal(created.Id, savedOpportunity.WonQuoteId);
        Assert.Equal(opp.CustomerId, created.CustomerId);
        Assert.Equal(opp.OwnerUserId, created.OwnerUserId);
    }

    [Fact]
    public async Task CreateAsync_OnWonOpportunityWithWinningQuote_RejectsDuplicate()
    {
        var (user, opp) = await SeedOpportunityAsync(OpportunityStage.Won);
        var created = await _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: true);

        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: true));

        Assert.Contains(created.Id.ToString(), ex.Message);
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task SubmitAsync_RejectsWhenOpportunityWentLostAfterQuoteWasDrafted()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var quote = await _sut.CreateAsync(NewUnitCostRequest(opp.Id), user.Id, canManage: true);

        var tracked = await _db.Opportunities.SingleAsync(o => o.Id == opp.Id);
        tracked.Stage = OpportunityStage.Lost;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.SubmitAsync(
            quote.Id, new QuoteWorkflowRequest(), user.Id, canManage: true, canSeeAll: true));

        Assert.Contains("Lost", ex.Message);

        var saved = await _db.Quotes.SingleAsync(q => q.Id == quote.Id);
        Assert.Equal(QuoteStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task SubmitAsync_ReportsPermissionFailure_BeforeOpportunityStage()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var quote = await _sut.CreateAsync(NewUnitCostRequest(opp.Id), user.Id, canManage: true);

        var tracked = await _db.Opportunities.SingleAsync(o => o.Id == opp.Id);
        tracked.Stage = OpportunityStage.Lost;
        await _db.SaveChangesAsync();

        // Without the permission the caller must hear about the permission, not
        // about the opportunity — otherwise the stage of a record they may not even
        // be allowed to see leaks out.
        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.SubmitAsync(
            quote.Id, new QuoteWorkflowRequest(), user.Id, canManage: false, canSeeAll: true));

        Assert.DoesNotContain("Lost", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_WithoutManagePermission_Throws()
    {
        var (user, opp) = await SeedOpportunityAsync();

        var ex = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            NewUnitCostRequest(opp.Id), user.Id, canManage: false));
        Assert.Contains("quyền", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_OrdinarySalesCannotAssignAnotherOwner()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var other = new ApplicationUser
        {
            PhoneNumber = Guid.NewGuid().ToString("N")[..12],
            FullName = "Other owner",
            Email = $"other-{Guid.NewGuid():N}@nihome.test",
            Role = UserRole.USER,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(other);
        await _db.SaveChangesAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.OwnerUserId = other.Id;

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            request, user.Id, canManage: true, canSeeAll: false));

        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownOwnerEvenWithFullScope()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.OwnerUserId = int.MaxValue;

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(
            request, user.Id, canManage: true, canSeeAll: true));

        Assert.Empty(_db.Quotes);
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
            MaterialRateCatalogId = _rateCatalogId,
            PricingEffectiveDate = _pricingDate,
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

    [Fact]
    public async Task CreateAsync_UnitCost_RejectsBoqCatalog()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var (catalog, _) = await SeedApprovedRateRevisionAsync(MaterialRateCatalogType.Boq);

        var exception = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            MaterialRateCatalogId = catalog.Id,
            PricingEffectiveDate = _pricingDate,
        }, user.Id, canManage: true));

        Assert.Contains("đã duyệt có hiệu lực", exception.Message);
        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task CreateAndUpdateAsync_BoqCatalogPreservesRevisionProvenanceAndEditableCopiedItems()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var (catalog, revision) = await SeedApprovedRateRevisionAsync(MaterialRateCatalogType.Boq);
        var created = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.Boq,
            MaterialRateCatalogId = catalog.Id,
            PricingEffectiveDate = _pricingDate,
            DiscountPercent = 0m,
            VatPercent = 10m,
            Items =
            [
                new QuoteItemInput
                {
                    ItemCode = "PACKAGE",
                    Name = "Gói tiêu chuẩn",
                    Unit = "m2",
                    Quantity = 10m,
                    UnitPrice = 1_500_000m,
                    SortOrder = 1,
                },
            ],
        }, user.Id, canManage: true);
        var createdRateSource = created.RateSource;

        var updated = await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            MaterialRateCatalogId = catalog.Id,
            PricingEffectiveDate = _pricingDate,
            DiscountPercent = 0m,
            VatPercent = 10m,
            ValidUntil = created.ValidUntil,
            Items =
            [
                new QuoteItemInput
                {
                    ItemCode = "PACKAGE",
                    Name = "Gói tiêu chuẩn đã chỉnh sửa",
                    Unit = "m2",
                    Quantity = 12m,
                    UnitPrice = 1_600_000m,
                    SortOrder = 1,
                },
            ],
        }, user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(updated);
        Assert.Equal(revision.Id, updated!.MaterialRateRevisionId);
        Assert.Equal(catalog.Id, updated.MaterialRateCatalogId);
        Assert.Equal(_pricingDate, updated.PricingEffectiveDate);
        Assert.Equal("CatalogReference", updated.RateSource);
        Assert.Null(updated.RateOverrideReason);
        var item = Assert.Single(updated.Items);
        Assert.Equal("PACKAGE", item.ItemCode);
        Assert.Equal("Gói tiêu chuẩn đã chỉnh sửa", item.Name);
        Assert.Equal(12m, item.Quantity);
        Assert.Equal(1_600_000m, item.UnitPrice);
        Assert.Equal(19_200_000m, item.Amount);
        Assert.Equal(21_120_000m, updated.GrandTotal);
        Assert.Equal("CatalogReference", createdRateSource);
        Assert.Null(created.RateOverrideReason);
    }

    [Theory]
    [InlineData(1.00001, 100)]
    [InlineData(1, 100.001)]
    public async Task CreateAsync_BoqRejectsValuesThatExceedStoredDecimalScale(
        decimal quantity,
        decimal unitPrice)
    {
        var (user, opportunity) = await SeedOpportunityAsync();

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.Boq,
            Items =
            [
                new QuoteItemInput
                {
                    Name = "Hạng mục kiểm thử",
                    Unit = "m2",
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                },
            ],
        }, user.Id, canManage: true));

        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task CreateAsync_BoqRoundsMidpointAwayFromZeroUsingPersistedValues()
    {
        var (user, opportunity) = await SeedOpportunityAsync();

        var result = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.Boq,
            Items =
            [
                new QuoteItemInput
                {
                    Name = "Hạng mục làm tròn",
                    Unit = "m2",
                    Quantity = 1.005m,
                    UnitPrice = 1m,
                },
            ],
            DiscountPercent = 0m,
            VatPercent = 0m,
        }, user.Id, canManage: true);

        var item = Assert.Single(result.Items);
        Assert.Equal(1.005m, item.Quantity);
        Assert.Equal(1m, item.UnitPrice);
        Assert.Equal(1.01m, item.Amount);
        Assert.Equal(1.01m, result.Subtotal);
        Assert.Equal(1.01m, result.GrandTotal);
    }

    [Theory]
    [InlineData("area")]
    [InlineData("unitPrice")]
    [InlineData("discount")]
    [InlineData("vat")]
    public async Task CreateAsync_RejectsFixedScaleValuesThatSqlWouldRound(string field)
    {
        var (user, opportunity) = await SeedOpportunityAsync();

        var exception = await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.UnitCost,
            MaterialRateCatalogId = _rateCatalogId,
            PricingEffectiveDate = _pricingDate,
            AreaSqm = field == "area" ? 100.001m : 100m,
            UnitPricePerSqm = field == "unitPrice" ? 10_000_000.001m : 10_000_000m,
            RateOverrideReason = "Điều chỉnh đơn giá kiểm thử",
            DiscountPercent = field == "discount" ? 1.001m : 0m,
            VatPercent = field == "vat" ? 1.001m : 0m,
        }, user.Id, canManage: true, canOverrideRate: true));

        Assert.Contains("tối đa 2 số lẻ", exception.Message);
        Assert.Empty(_db.Quotes);
    }

    [Theory]
    [InlineData(MaterialRateCatalogType.InvestmentRate, MaterialRateRevisionStatus.Approved, true, "2026-09-01")]
    [InlineData(MaterialRateCatalogType.Boq, MaterialRateRevisionStatus.Draft, true, "2026-09-01")]
    [InlineData(MaterialRateCatalogType.Boq, MaterialRateRevisionStatus.Approved, false, "2026-09-01")]
    [InlineData(MaterialRateCatalogType.Boq, MaterialRateRevisionStatus.Approved, true, "2027-01-01")]
    public async Task CreateAsync_BoqCatalogRejectsWrongTypeUnavailableOrIneffectiveRevision(
        MaterialRateCatalogType catalogType,
        MaterialRateRevisionStatus status,
        bool isActive,
        string pricingDate)
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var (catalog, revision) = await SeedApprovedRateRevisionAsync(catalogType);
        catalog.IsActive = isActive;
        revision.Status = status;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opportunity.Id,
            Method = QuoteMethod.Boq,
            MaterialRateCatalogId = catalog.Id,
            PricingEffectiveDate = DateOnly.Parse(pricingDate),
            Items =
            [
                new QuoteItemInput
                {
                    Name = "Hạng mục kiểm thử",
                    Unit = "m2",
                    Quantity = 1m,
                    UnitPrice = 100m,
                },
            ],
        }, user.Id, canManage: true));

        Assert.Empty(_db.Quotes);
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

    // Regression: Submit for a BOQ quote must load Items in the transition
    // guard, otherwise the "BOQ needs ≥ 1 item" check triggers false-positive.
    [Fact]
    public async Task Submit_BoqWithItems_Succeeds()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var created = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.Boq,
            DiscountPercent = 0m,
            VatPercent = 10m,
            Items = new()
            {
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 10, UnitPrice = 1_000_000 },
            },
        }, user.Id, canManage: true);

        var resp = await _sut.SubmitAsync(created.Id, new(), user.Id, true, true);
        Assert.NotNull(resp);
        Assert.Equal("PendingApproval", resp!.Status);
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

    [Theory]
    [InlineData(QuoteStatus.Approved)]
    [InlineData(QuoteStatus.SentToCustomer)]
    [InlineData(QuoteStatus.Expired)]
    public async Task UpdateAsync_AfterApproval_LogsActualSourceStatus(QuoteStatus sourceStatus)
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.Status = sourceStatus;
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(quote.Id, new UpdateQuoteRequest
        {
            AreaSqm = 120m,
            UnitPricePerSqm = 10_000_000m,
            DiscountPercent = 0m,
            VatPercent = 8m,
        }, user.Id, canManage: true, canSeeAll: true);

        var log = await _db.QuoteApprovalLogs
            .SingleAsync(entry => entry.Action == QuoteWorkflowAction.NewVersion);
        Assert.Equal(sourceStatus, log.FromStatus);
        Assert.Equal(QuoteStatus.Draft, log.ToStatus);
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

    [Theory]
    [InlineData(QuoteStatus.Draft)]
    [InlineData(QuoteStatus.PendingApproval)]
    [InlineData(QuoteStatus.Approved)]
    [InlineData(QuoteStatus.SentToCustomer)]
    [InlineData(QuoteStatus.CustomerApproved)]
    [InlineData(QuoteStatus.Rejected)]
    [InlineData(QuoteStatus.Expired)]
    [InlineData(QuoteStatus.Cancelled)]
    public async Task DeleteAsync_AnyStatus_RemovesAggregateAndPreservesPrincipals(QuoteStatus status)
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.Status = status;
        _db.QuoteItems.Add(new QuoteItem
        {
            QuoteId = quote.Id,
            Name = "Owned item",
            Unit = "item",
            Quantity = 1,
            UnitPrice = 1,
            Amount = 1,
        });
        _db.QuoteVersionSnapshots.Add(new QuoteVersionSnapshot
        {
            QuoteId = quote.Id,
            VersionNumber = 1,
        });
        await _db.SaveChangesAsync();
        var opportunityId = quote.OpportunityId;
        var opportunity = (await _db.Opportunities.FindAsync(opportunityId))!;
        opportunity.WonQuoteId = quote.Id;
        quote.RowVersion = BitConverter.GetBytes(100L + (int)status);
        var contract = new Contract
        {
            ContractNumber = $"HD-QUOTE-{status}",
            CustomerId = opportunity.CustomerId,
            OpportunityId = opportunity.Id,
            QuoteId = quote.Id,
            Value = 1,
        };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        var customerId = opportunity.CustomerId;
        var materialRevisionId = quote.MaterialRateRevisionId;
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        var result = await _sut.DeleteAsync(quote.Id, new ConfirmDeletionRequest
        {
            PlanToken = plan.Impact.PlanToken,
            Confirmation = quote.Code,
            RowVersion = Convert.ToBase64String(quote.RowVersion),
        }, user.Id, canManage: true, canSeeAll: true);

        Assert.True(result!.IsComplete);
        Assert.Empty(_db.Quotes);
        Assert.Empty(_db.QuoteItems);
        Assert.Empty(_db.QuoteApprovalLogs);
        Assert.Empty(_db.QuoteVersionSnapshots);
        Assert.True(await _db.Opportunities.AnyAsync(o => o.Id == opportunityId));
        Assert.Null((await _db.Opportunities.FindAsync(opportunityId))!.WonQuoteId);
        Assert.Null((await _db.Contracts.FindAsync(contract.Id))!.QuoteId);
        Assert.True(await _db.Contracts.AnyAsync(item => item.Id == contract.Id));
        Assert.True(await _db.Customers.AnyAsync(c => c.Id == customerId));
        Assert.True(await _db.Users.AnyAsync(u => u.Id == user.Id));
        if (materialRevisionId.HasValue)
            Assert.True(await _db.MaterialRateRevisions.AnyAsync(item => item.Id == materialRevisionId));
    }

    [Fact]
    public async Task DeleteAsync_MissingId_ReturnsNull()
    {
        var user = await SeedUserAsync();
        Assert.Null(await _sut.DeleteAsync(99999, new ConfirmDeletionRequest(),
            user.Id, canManage: true, canSeeAll: true));
    }

    [Fact]
    public async Task DeleteAsync_OtherOwnersQuote_ReturnsNull()
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        var stranger = await SeedUserAsync();

        Assert.Null(await _sut.DeleteAsync(quote.Id, new ConfirmDeletionRequest(),
            stranger.Id, canManage: true, canSeeAll: false));
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task ForQuoteAsync_GraphChangeChangesTokenAndCountsOwnedAndInboundRows()
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        quote.RowVersion = BitConverter.GetBytes(123L);
        _db.QuoteDocuments.Add(new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/proposal.pdf",
            OriginalFileName = "proposal.pdf",
        });
        await _db.SaveChangesAsync();

        var before = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;
        _db.QuoteItems.Add(new QuoteItem
        {
            QuoteId = quote.Id,
            Name = "Changed graph",
            Unit = "item",
            Quantity = 1,
            UnitPrice = 1,
            Amount = 1,
        });
        await _db.SaveChangesAsync();
        var after = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        Assert.True(before.Impact.CanDelete);
        Assert.NotEqual(before.Impact.PlanToken, after.Impact.PlanToken);
        Assert.Equal(before.Impact.TotalAffected + 1, after.Impact.TotalAffected);
        Assert.Contains(before.Items, item => item.Kind == HardDeleteItemKind.LocalFile &&
            item.ActionIdentifier == $"/files/quotes/{quote.Id}/proposal.pdf");
    }

    [Theory]
    [InlineData(" /files/quotes/1/invalid.pdf")]
    [InlineData("/files/contracts/outside.pdf")]
    public async Task DeleteAsync_InvalidOrOutsideFilePathBlocksWithoutOperation(string filePath)
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.RowVersion = BitConverter.GetBytes(124L);
        _db.QuoteDocuments.Add(new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = filePath,
            OriginalFileName = "unsafe.pdf",
        });
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.DeleteAsync(quote.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = plan.Impact.PlanToken,
                Confirmation = quote.Code,
                RowVersion = Convert.ToBase64String(quote.RowVersion),
            }, user.Id, canManage: true, canSeeAll: true));

        Assert.False(plan.Impact.CanDelete);
        Assert.Empty(_db.HardDeleteOperations);
        Assert.True(await _db.Quotes.AnyAsync(item => item.Id == quote.Id));
    }

    [Fact]
    public async Task DeleteAsync_DuplicatePathBlocksWithoutOperation()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.RowVersion = BitConverter.GetBytes(125L);
        var path = $"/files/quotes/{quote.Id}/duplicate.pdf";
        _db.QuoteDocuments.AddRange(
            new QuoteDocument { QuoteId = quote.Id, FilePath = path, OriginalFileName = "a.pdf" },
            new QuoteDocument { QuoteId = quote.Id, FilePath = path, OriginalFileName = "b.pdf" });
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.DeleteAsync(quote.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = plan.Impact.PlanToken,
                Confirmation = quote.Code,
                RowVersion = Convert.ToBase64String(quote.RowVersion),
            }, user.Id, canManage: true, canSeeAll: true));

        Assert.False(plan.Impact.CanDelete);
        Assert.Empty(_db.HardDeleteOperations);
        Assert.Equal(2, await _db.QuoteDocuments.CountAsync(item => item.QuoteId == quote.Id));
    }

    [Fact]
    public async Task DeleteAsync_RejectsWhitespaceConfirmationAndMissingOrStaleRowVersionWithoutOperation()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.RowVersion = BitConverter.GetBytes(126L);
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        await Assert.ThrowsAsync<QuoteOperationException>(() => _sut.DeleteAsync(quote.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = plan.Impact.PlanToken,
                Confirmation = $" {quote.Code} ",
                RowVersion = Convert.ToBase64String(quote.RowVersion),
            }, user.Id, true, true));
        await Assert.ThrowsAsync<CrmConcurrencyTokenException>(() => _sut.DeleteAsync(quote.Id,
            new ConfirmDeletionRequest { PlanToken = plan.Impact.PlanToken, Confirmation = quote.Code },
            user.Id, true, true));
        await Assert.ThrowsAsync<CrmConcurrencyException>(() => _sut.DeleteAsync(quote.Id,
            new ConfirmDeletionRequest
            {
                PlanToken = plan.Impact.PlanToken,
                Confirmation = quote.Code,
                RowVersion = Convert.ToBase64String(BitConverter.GetBytes(999L)),
            }, user.Id, true, true));

        Assert.Empty(_db.HardDeleteOperations);
        Assert.True(await _db.Quotes.AnyAsync(item => item.Id == quote.Id));
    }

    [Fact]
    public async Task ForQuoteAsync_StableSyncedNiconSidecarCreatesOwnedDriveDefinition()
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        quote.OperationalProjectId = 1;
        quote.RowVersion = BitConverter.GetBytes(127L);
        var document = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/project.pdf",
            OriginalFileName = "project.pdf",
        };
        _db.QuoteDocuments.Add(document);
        await _db.SaveChangesAsync();
        var sidecar = new ProjectDocument
        {
            OperationalProjectId = 1,
            SourceModule = ProjectDocumentSourceModule.Crm,
            SourceType = ProjectDocumentSourceType.ExistingManagedFile,
            SourceEntityType = nameof(QuoteDocument),
            SourceSlot = "file",
            SourceRecordId = document.Id,
            LocalPath = document.FilePath,
            OriginalFileName = document.OriginalFileName,
            Sha256 = "hash",
            DesiredOperation = ProjectDocumentDesiredOperation.None,
            SyncStatus = ProjectDocumentSyncStatus.Synced,
            DriveFileId = "drive-file",
            DriveFolderId = "drive-folder",
            Generation = 7,
        };
        _db.ProjectDocuments.Add(sidecar);
        await _db.SaveChangesAsync();

        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "quote.driveFiles" && item.Action == DeletionImpactActions.Delete && item.Count == 1);
        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "quote.projectDocumentSidecars" && item.Action == DeletionImpactActions.Unlink && item.Count == 1);
        var drive = Assert.Single(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile);
        Assert.Equal("drive-file", drive.ActionIdentifier);
        Assert.Equal("drive-folder", drive.ExpectedParentId);
        Assert.Equal(3, drive.ExpectedAppProperties!.Count);
        Assert.Equal("unit-test", drive.ExpectedAppProperties["niconInstance"]);
        Assert.Equal($"project-document:{sidecar.Id}", drive.ExpectedAppProperties["niconReplicaKey"]);
        Assert.Equal("7", drive.ExpectedAppProperties["niconGeneration"]);
        Assert.Single(plan.Items, item => item.Kind == HardDeleteItemKind.LocalFile &&
            item.ActionIdentifier == document.FilePath);
    }

    [Theory]
    [InlineData("imported")]
    [InlineData("shared")]
    [InlineData("conflicted")]
    [InlineData("pending")]
    [InlineData("pending-operation")]
    [InlineData("failed")]
    [InlineData("mismatched-path")]
    [InlineData("missing-file-id")]
    [InlineData("missing-folder-id")]
    [InlineData("missing-generation")]
    [InlineData("active-claim")]
    [InlineData("expired-claim")]
    [InlineData("claim-expiry-only")]
    [InlineData("wrong-project")]
    [InlineData("wrong-source-slot")]
    [InlineData("conflict-link")]
    [InlineData("conflict-observation")]
    [InlineData("compound-identity-mismatch")]
    public async Task ForQuoteAsync_UnsafeProjectDocumentSidecarBlocks(string unsafeState)
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        quote.OperationalProjectId = 1;
        var document = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/{unsafeState}.pdf",
            OriginalFileName = $"{unsafeState}.pdf",
        };
        _db.QuoteDocuments.Add(document);
        await _db.SaveChangesAsync();
        var sidecar = CreateQuoteSidecar(document, $"drive-{unsafeState}");
        switch (unsafeState)
        {
            case "imported":
                sidecar.SourceType = ProjectDocumentSourceType.GoogleDriveImport;
                break;
            case "shared":
                sidecar.Origin = ProjectDocumentOrigin.GoogleDrive;
                break;
            case "conflicted":
                sidecar.ConflictState = ProjectDocumentConflictState.PendingConfirmation;
                break;
            case "pending":
                sidecar.SyncStatus = ProjectDocumentSyncStatus.Pending;
                break;
            case "pending-operation":
                sidecar.DesiredOperation = ProjectDocumentDesiredOperation.Upsert;
                break;
            case "failed":
                sidecar.SyncStatus = ProjectDocumentSyncStatus.Failed;
                break;
            case "mismatched-path":
                sidecar.LocalPath += ".other";
                break;
            case "missing-file-id":
                sidecar.DriveFileId = null;
                break;
            case "missing-folder-id":
                sidecar.DriveFolderId = null;
                break;
            case "missing-generation":
                sidecar.Generation = 0;
                break;
            case "active-claim":
                sidecar.ClaimToken = Guid.NewGuid();
                sidecar.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(5);
                break;
            case "expired-claim":
                sidecar.ClaimToken = Guid.NewGuid();
                sidecar.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(-5);
                break;
            case "claim-expiry-only":
                sidecar.ClaimExpiresAt = DateTime.UtcNow.AddMinutes(-5);
                break;
            case "wrong-project":
                sidecar.OperationalProjectId = 2;
                break;
            case "wrong-source-slot":
                sidecar.SourceSlot = "other";
                break;
            case "conflict-link":
                sidecar.ConflictWithDocumentId = 123;
                break;
            case "conflict-observation":
                sidecar.ConflictObservedDriveFileId = "observed-drive";
                sidecar.ConflictObservedDriveVersion = "observed-version";
                break;
            case "compound-identity-mismatch":
                sidecar.SourceModule = ProjectDocumentSourceModule.Design;
                sidecar.SourceSlot = "other";
                sidecar.LocalPath += ".other";
                break;
        }
        _db.ProjectDocuments.Add(sidecar);
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        Assert.False(plan.Impact.CanDelete);
        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "quote.projectDocumentSidecarBlockers" &&
            item.Action == DeletionImpactActions.Block && item.Count == 1);
        Assert.DoesNotContain(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile);
    }

    [Fact]
    public async Task ForQuoteAsync_TerminalDeletedSidecarWithoutDriveFileIsPreservedWithoutBlocker()
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        quote.OperationalProjectId = 1;
        var document = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/already-deleted.pdf",
            OriginalFileName = "already-deleted.pdf",
        };
        _db.QuoteDocuments.Add(document);
        await _db.SaveChangesAsync();
        var sidecar = CreateQuoteSidecar(document, null);
        sidecar.DesiredOperation = ProjectDocumentDesiredOperation.None;
        sidecar.SyncStatus = ProjectDocumentSyncStatus.Deleted;
        _db.ProjectDocuments.Add(sidecar);
        await _db.SaveChangesAsync();

        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "quote.projectDocumentSidecars" && item.Action == DeletionImpactActions.Unlink);
        Assert.DoesNotContain(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile);
    }

    [Fact]
    public async Task ForQuoteAsync_DuplicateDriveIdsBlockAllAmbiguousSidecars()
    {
        var (_, quote) = await SeedApprovedReadyQuoteAsync();
        quote.OperationalProjectId = 1;
        var first = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/first.pdf",
            OriginalFileName = "first.pdf",
        };
        var second = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/second.pdf",
            OriginalFileName = "second.pdf",
        };
        _db.QuoteDocuments.AddRange(first, second);
        await _db.SaveChangesAsync();
        _db.ProjectDocuments.AddRange(
            CreateQuoteSidecar(first, "duplicate-drive"),
            CreateQuoteSidecar(second, "duplicate-drive"));
        await _db.SaveChangesAsync();

        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        Assert.Contains(plan.Impact.Items, item =>
            item.Key == "quote.projectDocumentSidecarBlockers" && item.Count == 2);
        Assert.DoesNotContain(plan.Items, item => item.Kind == HardDeleteItemKind.DriveFile);
    }

    [Fact]
    public async Task DeleteAsync_VerifiedDriveDeletionTerminalizesAndPreservesSidecar()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.OperationalProjectId = 1;
        quote.RowVersion = BitConverter.GetBytes(129L);
        var document = new QuoteDocument
        {
            QuoteId = quote.Id,
            FilePath = $"/files/quotes/{quote.Id}/terminalize.pdf",
            OriginalFileName = "terminalize.pdf",
        };
        _db.QuoteDocuments.Add(document);
        await _db.SaveChangesAsync();
        var sidecar = CreateQuoteSidecar(document, "drive-terminalize");
        sidecar.DriveWebViewLink = "https://drive.test/file";
        sidecar.DriveVersion = "3";
        sidecar.DriveModifiedAt = DateTime.UtcNow.AddDays(-1);
        sidecar.SyncAttemptCount = 2;
        sidecar.SyncError = "old error";
        sidecar.NextSyncAttemptAt = DateTime.UtcNow.AddMinutes(1);
        sidecar.LastSyncAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        _db.ProjectDocuments.Add(sidecar);
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        var result = await _sut.DeleteAsync(quote.Id, new ConfirmDeletionRequest
        {
            PlanToken = plan.Impact.PlanToken,
            Confirmation = quote.Code,
            RowVersion = Convert.ToBase64String(quote.RowVersion),
        }, user.Id, canManage: true, canSeeAll: true);

        Assert.True(result!.IsComplete);
        var preserved = await _db.ProjectDocuments.SingleAsync(item => item.Id == sidecar.Id);
        Assert.Equal(ProjectDocumentDesiredOperation.None, preserved.DesiredOperation);
        Assert.Equal(ProjectDocumentSyncStatus.Deleted, preserved.SyncStatus);
        Assert.Null(preserved.DriveFileId);
        Assert.Null(preserved.DriveWebViewLink);
        Assert.Null(preserved.DriveVersion);
        Assert.Null(preserved.DriveModifiedAt);
        Assert.Equal(0, preserved.SyncAttemptCount);
        Assert.Null(preserved.SyncError);
        Assert.Null(preserved.NextSyncAttemptAt);
        Assert.Null(preserved.LastSyncAttemptAt);
        Assert.Null(preserved.ClaimToken);
        Assert.Null(preserved.ClaimExpiresAt);
        Assert.NotNull(preserved.DeletedAt);
        Assert.Equal(user.Id, preserved.DeletedByUserId);
        Assert.Equal(user.Id, preserved.UpdatedByUserId);
        Assert.False(await _db.Quotes.AnyAsync(item => item.Id == quote.Id));
        Assert.False(await _db.QuoteDocuments.AnyAsync(item => item.Id == document.Id));
        Assert.Single(_db.AuditLogs, auditLog =>
            auditLog.Action == "quote.delete" && auditLog.ResourceId == quote.Id.ToString());
    }

    [Fact]
    public async Task QuoteHandler_MissingRootForwardRecovery_IsAuthorizedAndAuditedIdempotently()
    {
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(item => item.HasAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new QuoteHardDeleteHandler(_db, _hardDeletePlans, permissions.Object);
        var operationId = Guid.NewGuid();
        var context = new HardDeleteResourceContext(
            operationId, EntityTypes.Quote, "987654", "plan", "1", "delete-quote-aggregate", true);

        await handler.AuthorizeAsync(context);
        await handler.FinalizeAsync(context);
        await handler.FinalizeAsync(context);

        Assert.Single(_db.AuditLogs, auditLog =>
            auditLog.AuditId == operationId.ToString("N") &&
            auditLog.Action == "quote.delete" && auditLog.ResourceId == "987654");
    }

    [Fact]
    public async Task DeleteAsync_SeededQuoteCodeRecordsDeletionTombstone()
    {
        var (user, quote) = await SeedApprovedReadyQuoteAsync();
        quote.Code = "QT-SAMPLE-999";
        quote.RowVersion = BitConverter.GetBytes(128L);
        await _db.SaveChangesAsync();
        var plan = (await _hardDeletePlans.ForQuoteAsync(quote.Id))!;

        var result = await _sut.DeleteAsync(quote.Id, new ConfirmDeletionRequest
        {
            PlanToken = plan.Impact.PlanToken,
            Confirmation = quote.Code,
            RowVersion = Convert.ToBase64String(quote.RowVersion),
        }, user.Id, canManage: true, canSeeAll: true);

        Assert.True(result!.IsComplete);
        var tombstone = await _db.SeededRootDeletions.SingleAsync(item =>
            item.ResourceType == EntityTypes.Quote && item.ResourceKey == quote.Code);
        Assert.Equal(user.Id, tombstone.DeletedByUserId);
    }

    // =========================== Helpers ===========================

    private CreateQuoteRequest NewUnitCostRequest(int oppId) => new()
    {
        OpportunityId = oppId,
        Method = QuoteMethod.UnitCost,
        AreaSqm = 100m,
        UnitPricePerSqm = 10_000_000m,
        MaterialRateCatalogId = _rateCatalogId,
        PricingEffectiveDate = _pricingDate,
        VatPercent = 8m,
    };

    private static ProjectDocument CreateQuoteSidecar(QuoteDocument document, string? driveFileId) => new()
    {
        OperationalProjectId = 1,
        SourceModule = ProjectDocumentSourceModule.Crm,
        SourceType = ProjectDocumentSourceType.ExistingManagedFile,
        SourceEntityType = nameof(QuoteDocument),
        SourceSlot = "file",
        SourceRecordId = document.Id,
        LocalPath = document.FilePath,
        OriginalFileName = document.OriginalFileName,
        Sha256 = "hash",
        Origin = ProjectDocumentOrigin.Nicon,
        Generation = 1,
        DesiredOperation = ProjectDocumentDesiredOperation.None,
        SyncStatus = ProjectDocumentSyncStatus.Synced,
        DriveFileId = driveFileId,
        DriveFolderId = "drive-folder",
    };

    // ---------------- Dirty check on update ----------------

    [Fact]
    public async Task UpdateAsync_NoOp_KeepsVersionStatusLogsAndItemIds()
    {
        var (user, created) = await SeedApprovedBoqQuoteAsync();

        var itemIdsBefore = await _db.QuoteItems
            .Where(i => i.QuoteId == created.Id)
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToListAsync();
        var logCountBefore = await _db.QuoteApprovalLogs.CountAsync(l => l.QuoteId == created.Id);

        // Send back exactly what is already stored.
        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items =
            [
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m, SortOrder = 1 },
                new QuoteItemInput { Name = "Thép", Unit = "kg", Quantity = 500m, UnitPrice = 20_000m, SortOrder = 2 },
            ],
        }, user.Id, canManage: true, canSeeAll: true);

        var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        Assert.Equal(1, after.Version);
        Assert.Equal(QuoteStatus.Approved, after.Status);

        Assert.Equal(logCountBefore, await _db.QuoteApprovalLogs.CountAsync(l => l.QuoteId == created.Id));

        var itemIdsAfter = await _db.QuoteItems
            .Where(i => i.QuoteId == created.Id)
            .OrderBy(i => i.Id)
            .Select(i => i.Id)
            .ToListAsync();
        Assert.Equal(itemIdsBefore, itemIdsAfter);
    }

    [Fact]
    public async Task UpdateAsync_NoteOnly_LogsUpdateWithoutBumpingVersion()
    {
        var (user, created) = await SeedApprovedUnitCostQuoteAsync();

        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            AreaSqm = created.AreaSqm,
            UnitPricePerSqm = created.UnitPricePerSqm,
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = "Ghi chú nội bộ mới",
        }, user.Id, canManage: true, canSeeAll: true);

        var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        Assert.Equal(1, after.Version);
        Assert.Equal(QuoteStatus.Approved, after.Status);

        var logs = await _db.QuoteApprovalLogs.Where(l => l.QuoteId == created.Id).ToListAsync();
        Assert.Contains(logs, l => l.Action == QuoteWorkflowAction.Update);
        Assert.DoesNotContain(logs, l => l.Action == QuoteWorkflowAction.NewVersion);
    }

    [Fact]
    public async Task UpdateAsync_DiscountChange_BumpsVersionAndReturnsToDraft()
    {
        var (user, created) = await SeedApprovedUnitCostQuoteAsync();

        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            AreaSqm = created.AreaSqm,
            UnitPricePerSqm = created.UnitPricePerSqm,
            DiscountPercent = created.DiscountPercent + 5m,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
        }, user.Id, canManage: true, canSeeAll: true);

        var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        Assert.Equal(2, after.Version);
        Assert.Equal(QuoteStatus.Draft, after.Status);

        var logs = await _db.QuoteApprovalLogs.Where(l => l.QuoteId == created.Id).ToListAsync();
        Assert.Contains(logs, l => l.Action == QuoteWorkflowAction.NewVersion);
    }

    [Fact]
    public async Task UpdateAsync_ReorderingBoqLines_BumpsVersion()
    {
        var (user, created) = await SeedApprovedBoqQuoteAsync();

        // Same lines, swapped order. The customer sees that on the printed quote,
        // so it counts as a real change.
        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items =
            [
                new QuoteItemInput { Name = "Thép", Unit = "kg", Quantity = 500m, UnitPrice = 20_000m, SortOrder = 1 },
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m, SortOrder = 2 },
            ],
        }, user.Id, canManage: true, canSeeAll: true);

        var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        Assert.Equal(2, after.Version);
    }

    [Fact]
    public async Task UpdateAsync_AddingBoqLine_BumpsVersion()
    {
        var (user, created) = await SeedApprovedBoqQuoteAsync();

        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
            Note = created.Note,
            Items =
            [
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m, SortOrder = 1 },
                new QuoteItemInput { Name = "Thép", Unit = "kg", Quantity = 500m, UnitPrice = 20_000m, SortOrder = 2 },
                new QuoteItemInput { Name = "Gạch", Unit = "viên", Quantity = 2000m, UnitPrice = 3_000m, SortOrder = 3 },
            ],
        }, user.Id, canManage: true, canSeeAll: true);

        var after = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        Assert.Equal(2, after.Version);
        Assert.Equal(3, await _db.QuoteItems.CountAsync(i => i.QuoteId == created.Id));
    }

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

    private async Task<(ApplicationUser owner, Opportunity opp)> SeedOpportunityAsync(
        OpportunityStage stage = OpportunityStage.Proposal)
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
            Stage = stage,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id,
        };
        _db.Opportunities.Add(opp);
        await _db.SaveChangesAsync();
        return (user, opp);
    }

    private async Task<(MaterialRateCatalog Catalog, MaterialRateRevision Revision)> SeedApprovedRateRevisionAsync(
        MaterialRateCatalogType catalogType)
    {
        var catalog = new MaterialRateCatalog
        {
            CatalogType = catalogType,
            Code = $"{catalogType.ToString().ToUpperInvariant()}-{Guid.NewGuid():N}"[..30],
            Name = $"{catalogType} test catalog",
            IsActive = true,
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
        };
        var revision = new MaterialRateRevision
        {
            Catalog = catalog,
            Version = 1,
            Status = MaterialRateRevisionStatus.Approved,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            EffectiveTo = new DateOnly(2026, 12, 31),
            CreatedByUserId = 1,
            UpdatedByUserId = 1,
            Lines =
            [
                new MaterialRateLine
                {
                    MaterialCode = "PACKAGE",
                    MaterialName = "Gói tiêu chuẩn",
                    Unit = "m2",
                    Quantity = catalogType == MaterialRateCatalogType.Boq ? 10m : 0m,
                    AmountPerSqm = catalogType == MaterialRateCatalogType.Boq ? 15_000_000m : 10_000_000m,
                },
            ],
        };
        _db.MaterialRateRevisions.Add(revision);
        await _db.SaveChangesAsync();
        return (catalog, revision);
    }

    /// <summary>Unit-cost quote pushed straight to Approved, the state where the
    /// version-bump bug lived.</summary>
    private async Task<(ApplicationUser owner, QuoteResponse quote)> SeedApprovedUnitCostQuoteAsync()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var created = await _sut.CreateAsync(NewUnitCostRequest(opp.Id), user.Id, canManage: true);
        var tracked = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        tracked.Status = QuoteStatus.Approved;
        await _db.SaveChangesAsync();
        return (user, created);
    }

    /// <summary>BOQ quote at Approved with two lines — two are needed so the
    /// reorder test has something to reorder.</summary>
    private async Task<(ApplicationUser owner, QuoteResponse quote)> SeedApprovedBoqQuoteAsync()
    {
        var (user, opp) = await SeedOpportunityAsync();
        var created = await _sut.CreateAsync(new CreateQuoteRequest
        {
            OpportunityId = opp.Id,
            Method = QuoteMethod.Boq,
            DiscountPercent = 0m,
            VatPercent = 8m,
            Items =
            [
                new QuoteItemInput { Name = "Bê tông", Unit = "m3", Quantity = 10m, UnitPrice = 1_500_000m },
                new QuoteItemInput { Name = "Thép", Unit = "kg", Quantity = 500m, UnitPrice = 20_000m },
            ],
        }, user.Id, canManage: true);

        var tracked = await _db.Quotes.SingleAsync(q => q.Id == created.Id);
        tracked.Status = QuoteStatus.Approved;
        await _db.SaveChangesAsync();
        return (user, created);
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
            MaterialRateCatalogId = _rateCatalogId,
            PricingEffectiveDate = _pricingDate,
            DiscountPercent = 0m,
            VatPercent = 8m,
        }, user.Id, canManage: true);

        var quote = await _db.Quotes.FirstAsync(q => q.Id == created.Id);
        return (user, quote);
    }

    [Fact]
    public async Task CreateAsync_UnitCost_DerivesCatalogRateAndProvenance()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.UnitPricePerSqm = null;

        var result = await _sut.CreateAsync(request, user.Id, canManage: true);

        Assert.Equal(10_000_000m, result.UnitPricePerSqm);
        Assert.Equal(10_000_000m, result.CatalogUnitPricePerSqm);
        Assert.Equal(10_000_000m, await _db.MaterialRateRevisions
            .Where(revision => revision.Id == result.MaterialRateRevisionId)
            .SelectMany(revision => revision.Lines)
            .SumAsync(line => line.AmountPerSqm));
        Assert.Equal("Catalog", result.RateSource);
        Assert.NotNull(result.MaterialRateRevisionId);
        Assert.Equal(_pricingDate, result.PricingEffectiveDate);
    }

    [Fact]
    public async Task CreateAsync_UnitCostRoundsFractionalCatalogTotalToStoredMoneyScale()
    {
        var line = await _db.MaterialRateLines.SingleAsync();
        line.AmountPerSqm = 10_000_000.005m;
        await _db.SaveChangesAsync();
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.UnitPricePerSqm = null;

        var result = await _sut.CreateAsync(request, user.Id, canManage: true);

        Assert.Equal(10_000_000.01m, result.UnitPricePerSqm);
        Assert.Equal(10_000_000.01m, result.CatalogUnitPricePerSqm);
        Assert.Equal("Catalog", result.RateSource);
    }

    [Fact]
    public async Task CreateAsync_DifferentRate_RequiresPermissionAndVietnameseReason()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.UnitPricePerSqm = 9_000_000m;
        request.RateOverrideReason = "Điều chỉnh theo phạm vi thi công thực tế.";

        await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.CreateAsync(request, user.Id, canManage: true));

        var result = await _sut.CreateAsync(
            request, user.Id, canManage: true, canOverrideRate: true);
        Assert.Equal("Override", result.RateSource);
        Assert.Equal(user.Id, result.RateOverrideByUserId);
        Assert.Equal(request.RateOverrideReason, result.RateOverrideReason);
        Assert.NotNull(result.RateOverrideAt);
    }

    [Fact]
    public async Task CreateAsync_UnitCost_RejectsDateWithoutApprovedEffectiveRevision()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.PricingEffectiveDate = new DateOnly(2025, 12, 31);

        var exception = await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.CreateAsync(request, user.Id, canManage: true));

        Assert.Contains("đã duyệt có hiệu lực", exception.Message);
        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task CreateAsync_DifferentRate_RejectsNonVietnameseReasonWithPermission()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.UnitPricePerSqm = 9_000_000m;
        request.RateOverrideReason = "Customer requested a lower rate.";

        var exception = await Assert.ThrowsAsync<QuoteOperationException>(() =>
            _sut.CreateAsync(request, user.Id, canManage: true, canOverrideRate: true));

        Assert.Contains("tiếng Việt", exception.Message);
        Assert.Empty(_db.Quotes);
    }

    [Fact]
    public async Task MigratedUnitCostQuote_RemainsReadableAsOverrideAfterUpdate()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var migrated = new Quote
        {
            Code = "QT-MIGRATED-001",
            OpportunityId = opportunity.Id,
            OwnerUserId = user.Id,
            Method = QuoteMethod.UnitCost,
            AreaSqm = 100m,
            UnitPricePerSqm = 5_000_000m,
            RateSource = QuoteRateSource.Override,
            RateOverrideReason = "Giá được chuyển đổi từ dữ liệu trước Module 1.",
            Subtotal = 500_000_000m,
            VatPercent = 8m,
            GrandTotal = 540_000_000m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
        };
        _db.Quotes.Add(migrated);
        await _db.SaveChangesAsync();

        var existing = await _sut.GetAsync(migrated.Id, user.Id, canSeeAll: false);
        Assert.Equal("Override", existing!.RateSource);
        Assert.Null(existing.MaterialRateRevisionId);
        Assert.Null(existing.CatalogUnitPricePerSqm);

        var updated = await _sut.UpdateAsync(migrated.Id, new UpdateQuoteRequest
        {
            AreaSqm = 120m,
            UnitPricePerSqm = 5_000_000m,
            DiscountPercent = 0m,
            VatPercent = 8m,
            ValidUntil = migrated.ValidUntil,
        }, user.Id, canManage: true, canSeeAll: false);

        Assert.Equal("Override", updated!.RateSource);
        Assert.Null(updated.MaterialRateRevisionId);
        Assert.Equal(648_000_000m, updated.GrandTotal);
    }

    [Fact]
    public async Task UpdateAsync_AfterApproval_SnapshotKeepsOriginalRateProvenance()
    {
        var (user, opportunity) = await SeedOpportunityAsync();
        var request = NewUnitCostRequest(opportunity.Id);
        request.UnitPricePerSqm = 9_000_000m;
        request.RateOverrideReason = "Điều chỉnh theo phạm vi thi công thực tế.";
        var created = await _sut.CreateAsync(
            request, user.Id, canManage: true, canOverrideRate: true);
        var tracked = await _db.Quotes.SingleAsync(quote => quote.Id == created.Id);
        tracked.Status = QuoteStatus.Approved;
        await _db.SaveChangesAsync();

        await _sut.UpdateAsync(created.Id, new UpdateQuoteRequest
        {
            AreaSqm = created.AreaSqm,
            UnitPricePerSqm = created.CatalogUnitPricePerSqm,
            MaterialRateCatalogId = _rateCatalogId,
            PricingEffectiveDate = _pricingDate,
            DiscountPercent = created.DiscountPercent,
            VatPercent = created.VatPercent,
            ValidUntil = created.ValidUntil,
        }, user.Id, canManage: true, canSeeAll: true);

        var snapshot = await _db.QuoteVersionSnapshots.SingleAsync();
        Assert.Equal(created.MaterialRateRevisionId, snapshot.MaterialRateRevisionId);
        Assert.Equal(_pricingDate, snapshot.PricingEffectiveDate);
        Assert.Equal(10_000_000m, snapshot.CatalogUnitPricePerSqm);
        Assert.Equal(QuoteRateSource.Override, snapshot.RateSource);
        Assert.Equal(request.RateOverrideReason, snapshot.RateOverrideReason);
        Assert.Equal(user.Id, snapshot.RateOverrideByUserId);
        Assert.Equal(created.RateOverrideAt, snapshot.RateOverrideAt);

        var versions = await _sut.GetVersionsAsync(created.Id, user.Id, canSeeAll: true);
        var original = Assert.Single(versions!.Versions, version => version.Version == 1);
        Assert.Equal(_rateCatalogId, original.MaterialRateCatalogId);
        Assert.Equal("RATE-TEST", original.MaterialRateCatalogCode);
        Assert.Equal("Override", original.RateSource);
        Assert.Equal(9_000_000m, original.UnitPricePerSqm);
    }
}
