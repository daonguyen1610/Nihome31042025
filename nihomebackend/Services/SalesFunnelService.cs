using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class SalesFunnelOperationException(string message) : Exception(message);

/// <summary>
/// Counts the CRM funnel for records created between two business dates (configured time zone).
/// Callers without view-all rights see only records they own.
/// </summary>
public sealed class SalesFunnelService(AppDbContext db)
{
    public const int DefaultRangeDays = 90;
    public const int MaxRangeDays = 366 * 3;

    public async Task<SalesFunnelResponse> GetAsync(
        DateOnly? from, DateOnly? to, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var zone = await DisplaySettings.ZoneAsync(db, ct);
        var end = to ?? DateOnly.FromDateTime(DisplaySettings.TodayIn(zone));
        var start = from ?? end.AddDays(-(DefaultRangeDays - 1));
        if (start > end)
            throw new SalesFunnelOperationException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber >= MaxRangeDays)
            throw new SalesFunnelOperationException("Khoảng thời gian tối đa là 3 năm. Hãy chọn khoảng ngắn hơn.");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);

        var leads = db.Leads.AsNoTracking()
            .Where(lead => lead.CreatedAt >= startUtc && lead.CreatedAt < endUtc &&
                (canSeeAll || lead.OwnerUserId == callerUserId));
        var opportunities = db.Opportunities.AsNoTracking()
            .Where(opportunity => opportunity.CreatedAt >= startUtc && opportunity.CreatedAt < endUtc &&
                (canSeeAll || opportunity.OwnerUserId == callerUserId));
        var contracts = db.Contracts.AsNoTracking()
            .Where(contract => contract.Direction == ContractDirection.Upstream &&
                contract.SignedDate >= startUtc && contract.SignedDate < endUtc &&
                contract.Status != ContractStatus.Cancelled &&
                (canSeeAll || contract.OwnerUserId == callerUserId));

        var leadsCreated = await leads.CountAsync(ct);
        var leadsConverted = await leads.CountAsync(lead => lead.Status == LeadStatus.Converted, ct);
        var stages = await opportunities
            .GroupBy(opportunity => opportunity.Stage)
            .Select(group => new { Stage = group.Key, Count = group.Count() })
            .ToListAsync(ct);
        int Count(OpportunityStage stage) => stages.SingleOrDefault(item => item.Stage == stage)?.Count ?? 0;
        var won = Count(OpportunityStage.Won);
        var lost = Count(OpportunityStage.Lost);
        var created = stages.Sum(item => item.Count);

        return new SalesFunnelResponse
        {
            From = start,
            To = end,
            IncludesAllOwners = canSeeAll,
            LeadsCreated = leadsCreated,
            LeadsConverted = leadsConverted,
            OpportunitiesCreated = created,
            OpportunitiesOpen = created - won - lost,
            OpportunitiesWon = won,
            OpportunitiesLost = lost,
            ContractsSigned = await contracts.CountAsync(ct),
            LeadConversionRate = Percent(leadsConverted, leadsCreated),
            WinRate = Percent(won, won + lost),
        };
    }

    private static decimal? Percent(int part, int whole) =>
        whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
}
