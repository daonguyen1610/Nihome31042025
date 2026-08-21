namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Which branch ran when a lead conversion was undone.</summary>
public enum UnconvertOutcome
{
    /// <summary>Customer and opportunity were both created by the convert and both still clean — both deleted.</summary>
    DeletedBoth = 0,

    /// <summary>The customer pre-dated the convert; only the opportunity was auto-created — opportunity deleted.</summary>
    DeletedOpportunity = 1,

    /// <summary>Past the window, or child data exists — both kept, only the link removed.</summary>
    UnlinkedOnly = 2,
}

public class UnconvertLeadResponse
{
    public UnconvertOutcome Outcome { get; set; }

    /// <summary>Customer that was kept, if any — lets the caller link to it.</summary>
    public int? KeptCustomerId { get; set; }

    /// <summary>Opportunity that was kept, if any.</summary>
    public int? KeptOpportunityId { get; set; }

    public LeadResponse Lead { get; set; } = null!;
}
