namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>
/// Sales funnel for records created in a period: leads → opportunities → won → signed customer contracts.
/// Rates are null when their denominator is zero.
/// </summary>
public sealed class SalesFunnelResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    /// <summary>False when the caller only sees their own records.</summary>
    public bool IncludesAllOwners { get; set; }
    public int LeadsCreated { get; set; }
    public int LeadsConverted { get; set; }
    public int OpportunitiesCreated { get; set; }
    public int OpportunitiesOpen { get; set; }
    public int OpportunitiesWon { get; set; }
    public int OpportunitiesLost { get; set; }
    public int ContractsSigned { get; set; }
    /// <summary>Converted leads / leads created, in percent.</summary>
    public decimal? LeadConversionRate { get; set; }
    /// <summary>Won / (won + lost) opportunities, in percent.</summary>
    public decimal? WinRate { get; set; }
}
