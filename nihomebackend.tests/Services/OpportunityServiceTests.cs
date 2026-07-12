using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public class OpportunityServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<INotificationService> _notifications;
    private readonly OpportunityService _sut;

    public OpportunityServiceTests()
    {
        _db = DbContextFactory.Create();
        _notifications = new Mock<INotificationService>();
        _sut = new OpportunityService(
            _db,
            _notifications.Object,
            NullLogger<OpportunityService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ---------------- Create ----------------

    [Fact]
    public async Task CreateAsync_WithoutManagePermission_Throws()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);

        var ex = await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.CreateAsync(
            new CreateOpportunityRequest
            {
                Name = "New deal",
                CustomerId = customer.Id,
                EstimatedValue = 1000,
                WinProbability = 20,
            },
            user.Id,
            canManage: false));

        Assert.Contains("quyền", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownCustomer()
    {
        var user = await SeedUserAsync();

        await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.CreateAsync(
            new CreateOpportunityRequest
            {
                Name = "New deal",
                CustomerId = 9999,
                EstimatedValue = 1000,
                WinProbability = 20,
            },
            user.Id,
            canManage: true));
    }

    [Theory]
    [InlineData(OpportunityStage.Won)]
    [InlineData(OpportunityStage.Lost)]
    public async Task CreateAsync_RejectsTerminalStagesOnCreate(OpportunityStage stage)
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);

        await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.CreateAsync(
            new CreateOpportunityRequest
            {
                Name = "New deal",
                CustomerId = customer.Id,
                Stage = stage,
                EstimatedValue = 1000,
                WinProbability = 50,
            },
            user.Id,
            canManage: true));
    }

    [Fact]
    public async Task CreateAsync_AssignsCallerAsOwner_WhenOwnerNotSpecified()
    {
        var caller = await SeedUserAsync();
        var customer = await SeedCustomerAsync(caller.Id);

        var response = await _sut.CreateAsync(
            new CreateOpportunityRequest
            {
                Name = "New deal",
                CustomerId = customer.Id,
                EstimatedValue = 500_000_000,
                WinProbability = 30,
            },
            caller.Id,
            canManage: true);

        Assert.Equal(caller.Id, response.OwnerUserId);
        Assert.Equal(OpportunityStage.Prospecting, response.Stage);
    }

    [Fact]
    public async Task CreateAsync_FiresAssignedNotification_WhenOwnerDiffersFromCaller()
    {
        var caller = await SeedUserAsync();
        var owner = await SeedUserAsync();
        var customer = await SeedCustomerAsync(caller.Id);

        await _sut.CreateAsync(
            new CreateOpportunityRequest
            {
                Name = "Deal",
                CustomerId = customer.Id,
                OwnerUserId = owner.Id,
                EstimatedValue = 1000,
                WinProbability = 10,
            },
            caller.Id,
            canManage: true);

        _notifications.Verify(n => n.NotifyFromTemplateAsync(
                owner.Id,
                "opportunity.assigned",
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<string>()),
            Times.Once);
    }

    // ---------------- Owner scoping (List / Get / Update / Delete) ----------------

    [Fact]
    public async Task ListAsync_WithoutSeeAll_OnlyReturnsOwnRows()
    {
        var salesA = await SeedUserAsync();
        var salesB = await SeedUserAsync();
        var customer = await SeedCustomerAsync(salesA.Id);

        await SeedOpportunityAsync(customer, salesA);
        await SeedOpportunityAsync(customer, salesA);
        await SeedOpportunityAsync(customer, salesB);

        var result = await _sut.ListAsync(salesA.Id, canSeeAll: false);
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, o => Assert.Equal(salesA.Id, o.OwnerUserId));
    }

    [Fact]
    public async Task GetAsync_WithoutSeeAll_HidesOtherOwnersOpportunity()
    {
        var salesA = await SeedUserAsync();
        var salesB = await SeedUserAsync();
        var customer = await SeedCustomerAsync(salesA.Id);
        var op = await SeedOpportunityAsync(customer, salesB);

        var found = await _sut.GetAsync(op.Id, salesA.Id, canSeeAll: false);
        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateAsync_SalesWithoutSeeAll_CannotReassignToAnotherUser()
    {
        var salesA = await SeedUserAsync();
        var salesB = await SeedUserAsync();
        var customer = await SeedCustomerAsync(salesA.Id);
        var op = await SeedOpportunityAsync(customer, salesA);

        var ex = await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.UpdateAsync(
            op.Id,
            new UpdateOpportunityRequest
            {
                Name = op.Name,
                CustomerId = customer.Id,
                OwnerUserId = salesB.Id,
                EstimatedValue = op.EstimatedValue,
                WinProbability = op.WinProbability,
            },
            salesA.Id,
            canManage: true,
            canSeeAll: false));

        Assert.Contains("gán", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_SalesUser_CannotDeleteOtherOwnersOpportunity()
    {
        var salesA = await SeedUserAsync();
        var salesB = await SeedUserAsync();
        var customer = await SeedCustomerAsync(salesA.Id);
        var op = await SeedOpportunityAsync(customer, salesB);

        var removed = await _sut.DeleteAsync(op.Id, salesA.Id, canManage: true, canSeeAll: false);
        Assert.False(removed);
        Assert.NotNull(await _db.Opportunities.FindAsync(op.Id));
    }

    // ---------------- Stage transitions ----------------

    [Fact]
    public async Task ChangeStageAsync_ForwardTransition_PersistsAndAddsActivity()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user, OpportunityStage.Prospecting);

        var response = await _sut.ChangeStageAsync(op.Id,
            new ChangeOpportunityStageRequest { TargetStage = OpportunityStage.Qualification },
            user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(response);
        Assert.Equal(OpportunityStage.Qualification, response!.Stage);
        Assert.Single(_db.OpportunityActivities.Where(a => a.Type == OpportunityActivityType.StageChange));
    }

    [Theory]
    [InlineData(OpportunityStage.Won, OpportunityStage.Prospecting)]
    [InlineData(OpportunityStage.Won, OpportunityStage.Lost)]
    [InlineData(OpportunityStage.Lost, OpportunityStage.Negotiation)]
    public async Task ChangeStageAsync_FromTerminal_IsRejected(OpportunityStage from, OpportunityStage to)
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user, from);
        if (from == OpportunityStage.Lost)
        {
            op.LostReasonCode = "other";
            op.LostNote = "seeded";
            await _db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.ChangeStageAsync(
            op.Id,
            new ChangeOpportunityStageRequest { TargetStage = to },
            user.Id, canManage: true, canSeeAll: true));

        Assert.Contains("giai đoạn", ex.Message);
    }

    [Fact]
    public async Task ChangeStageAsync_ToWon_SetsProbability100AndClosedAt()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user, OpportunityStage.Negotiation);

        var response = await _sut.ChangeStageAsync(op.Id,
            new ChangeOpportunityStageRequest
            {
                TargetStage = OpportunityStage.Won,
                WonQuoteId = 42,
            },
            user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(response);
        Assert.Equal(OpportunityStage.Won, response!.Stage);
        Assert.Equal(100, response.WinProbability);
        Assert.Equal(42, response.WonQuoteId);
        Assert.NotNull(response.ClosedAt);
    }

    [Fact]
    public async Task ChangeStageAsync_ToLost_WithoutReason_IsRejected()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user);

        await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.ChangeStageAsync(
            op.Id,
            new ChangeOpportunityStageRequest { TargetStage = OpportunityStage.Lost },
            user.Id, canManage: true, canSeeAll: true));
    }

    [Fact]
    public async Task ChangeStageAsync_ToLost_WithUnknownReasonCode_IsRejected()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user);

        await Assert.ThrowsAsync<OpportunityOperationException>(() => _sut.ChangeStageAsync(
            op.Id,
            new ChangeOpportunityStageRequest
            {
                TargetStage = OpportunityStage.Lost,
                LostReasonCode = "does-not-exist",
                LostNote = "n/a",
            },
            user.Id, canManage: true, canSeeAll: true));
    }

    [Fact]
    public async Task ChangeStageAsync_ToLost_WithValidReason_PersistsReasonAndNote()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);
        var op = await SeedOpportunityAsync(customer, user);
        SeedLostReason("price");

        var response = await _sut.ChangeStageAsync(op.Id,
            new ChangeOpportunityStageRequest
            {
                TargetStage = OpportunityStage.Lost,
                LostReasonCode = "price",
                LostNote = "Khách bảo giá cao.",
            },
            user.Id, canManage: true, canSeeAll: true);

        Assert.NotNull(response);
        Assert.Equal(OpportunityStage.Lost, response!.Stage);
        Assert.Equal("price", response.LostReasonCode);
        Assert.Equal("Khách bảo giá cao.", response.LostNote);
        Assert.Equal(0, response.WinProbability);
    }

    // ---------------- Pipeline ----------------

    [Fact]
    public async Task GetPipelineAsync_GroupsBucketedByStage_WithTotals()
    {
        var user = await SeedUserAsync();
        var customer = await SeedCustomerAsync(user.Id);

        await SeedOpportunityAsync(customer, user, OpportunityStage.Prospecting, value: 100);
        await SeedOpportunityAsync(customer, user, OpportunityStage.Prospecting, value: 200);
        await SeedOpportunityAsync(customer, user, OpportunityStage.Negotiation, value: 500);

        var pipeline = await _sut.GetPipelineAsync(user.Id, canSeeAll: true);

        Assert.Equal(6, pipeline.Columns.Count);
        var prospecting = pipeline.Columns.Single(c => c.Stage == OpportunityStage.Prospecting);
        Assert.Equal(2, prospecting.Count);
        Assert.Equal(300, prospecting.TotalValue);
        var negotiation = pipeline.Columns.Single(c => c.Stage == OpportunityStage.Negotiation);
        Assert.Equal(500, negotiation.TotalValue);
    }

    // ---------------- Helpers ----------------

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

    private async Task<Customer> SeedCustomerAsync(int ownerId)
    {
        var customer = new Customer
        {
            Name = "Test Customer " + Guid.NewGuid().ToString("N")[..6],
            Type = CustomerType.Individual,
            SourceCode = "marketing",
            OwnerUserId = ownerId,
            CreatedByUserId = ownerId,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return customer;
    }

    private async Task<Opportunity> SeedOpportunityAsync(
        Customer customer,
        ApplicationUser owner,
        OpportunityStage stage = OpportunityStage.Prospecting,
        decimal value = 1_000_000m)
    {
        var op = new Opportunity
        {
            Name = "Deal " + Guid.NewGuid().ToString("N")[..6],
            CustomerId = customer.Id,
            OwnerUserId = owner.Id,
            EstimatedValue = value,
            WinProbability = 25,
            Stage = stage,
            CreatedByUserId = owner.Id,
            UpdatedByUserId = owner.Id,
        };
        _db.Opportunities.Add(op);
        await _db.SaveChangesAsync();
        return op;
    }

    private void SeedLostReason(string code)
    {
        if (_db.MasterDataOptions.Any(m => m.Category == "opportunity_lost_reason" && m.Code == code))
            return;

        _db.MasterDataOptions.Add(new MasterDataOption
        {
            Category = "opportunity_lost_reason",
            Code = code,
            Name = code,
            SortOrder = 1,
            IsActive = true,
        });
        _db.SaveChanges();
    }
}
