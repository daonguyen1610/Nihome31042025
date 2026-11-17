using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class SalesFunnelServiceTests : IDisposable
{
    private readonly AppDbContext db = DbContextFactory.Create();
    private readonly SalesFunnelService service;
    private readonly DateOnly today = DisplaySettings.TodayIn(DisplaySettings.VietnamTimeZone) is var now
        ? DateOnly.FromDateTime(now) : default;

    public SalesFunnelServiceTests()
    {
        service = new SalesFunnelService(db);
    }

    [Fact]
    public async Task GetAsync_CountsTheFunnelForRecordsCreatedInTheRange()
    {
        var sales = SeedUser("0911888001");
        var other = SeedUser("0911888002");
        Lead(sales, LeadStatus.Converted, daysAgo: 2);
        Lead(sales, LeadStatus.Contacted, daysAgo: 3);
        Lead(other, LeadStatus.New, daysAgo: 1);
        Lead(sales, LeadStatus.Converted, daysAgo: 60);
        Opportunity(sales, OpportunityStage.Won, daysAgo: 2);
        Opportunity(sales, OpportunityStage.Lost, daysAgo: 4);
        Opportunity(other, OpportunityStage.Won, daysAgo: 5);
        Opportunity(sales, OpportunityStage.Proposal, daysAgo: 1);
        Contract(sales, ContractStatus.Signed, ContractDirection.Upstream, daysAgo: 1);
        Contract(sales, ContractStatus.Signed, ContractDirection.Downstream, daysAgo: 1);
        Contract(other, ContractStatus.Cancelled, ContractDirection.Upstream, daysAgo: 1);

        var all = await service.GetAsync(today.AddDays(-29), today, sales.Id, canSeeAll: true, CancellationToken.None);

        Assert.True(all.IncludesAllOwners);
        Assert.Equal(3, all.LeadsCreated);
        Assert.Equal(1, all.LeadsConverted);
        Assert.Equal(33.3m, all.LeadConversionRate);
        Assert.Equal(4, all.OpportunitiesCreated);
        Assert.Equal(2, all.OpportunitiesWon);
        Assert.Equal(1, all.OpportunitiesLost);
        Assert.Equal(1, all.OpportunitiesOpen);
        Assert.Equal(66.7m, all.WinRate);
        Assert.Equal(1, all.ContractsSigned);
    }

    [Fact]
    public async Task GetAsync_WithoutViewAll_CountsOnlyOwnRecords()
    {
        var sales = SeedUser("0911888003");
        var other = SeedUser("0911888004");
        Lead(sales, LeadStatus.New, daysAgo: 1);
        Lead(other, LeadStatus.Converted, daysAgo: 1);
        Opportunity(other, OpportunityStage.Won, daysAgo: 1);

        var own = await service.GetAsync(null, null, sales.Id, canSeeAll: false, CancellationToken.None);

        Assert.False(own.IncludesAllOwners);
        Assert.Equal(1, own.LeadsCreated);
        Assert.Equal(0, own.OpportunitiesCreated);
        Assert.Equal(0m, own.LeadConversionRate);
        Assert.Null(own.WinRate);
        Assert.Equal(today, own.To);
        Assert.Equal(today.AddDays(-(SalesFunnelService.DefaultRangeDays - 1)), own.From);
    }

    [Fact]
    public async Task GetAsync_RejectsReversedOrOverlongRanges()
    {
        await Assert.ThrowsAsync<SalesFunnelOperationException>(() =>
            service.GetAsync(today, today.AddDays(-1), 1, true, CancellationToken.None));
        await Assert.ThrowsAsync<SalesFunnelOperationException>(() =>
            service.GetAsync(today.AddDays(-SalesFunnelService.MaxRangeDays), today, 1, true, CancellationToken.None));
    }

    public void Dispose() => db.Dispose();

    private static DateTime DaysAgo(int days) => DateTime.UtcNow.AddDays(-days);

    private ApplicationUser SeedUser(string phone)
    {
        var user = new ApplicationUser
        {
            PhoneNumber = phone,
            Email = $"{phone}@nihome.test",
            FullName = $"Kinh doanh {phone}",
            PasswordHash = "test",
            IsActive = true,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private void Lead(ApplicationUser owner, LeadStatus status, int daysAgo)
    {
        db.Leads.Add(new Lead
        {
            Name = $"Lead {Guid.NewGuid():N}"[..12],
            SourceCode = "referral",
            Status = status,
            OwnerUserId = owner.Id,
            CreatedAt = DaysAgo(daysAgo),
        });
        db.SaveChanges();
    }

    private void Opportunity(ApplicationUser owner, OpportunityStage stage, int daysAgo)
    {
        db.Opportunities.Add(new Opportunity
        {
            Name = $"Cơ hội {Guid.NewGuid():N}"[..14],
            CustomerId = 1,
            OwnerUserId = owner.Id,
            Stage = stage,
            CreatedAt = DaysAgo(daysAgo),
        });
        db.SaveChanges();
    }

    private void Contract(ApplicationUser owner, ContractStatus status, ContractDirection direction, int daysAgo)
    {
        db.Contracts.Add(new Contract
        {
            ContractNumber = $"HD-{Guid.NewGuid():N}"[..14],
            Direction = direction,
            Status = status,
            OwnerUserId = owner.Id,
            SignedDate = DaysAgo(daysAgo),
        });
        db.SaveChanges();
    }
}
