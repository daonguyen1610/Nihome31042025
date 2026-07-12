namespace NihomeBackend.Models;

/// <summary>
/// A single follow-up entry on an <see cref="Opportunity"/> — used to build
/// the sales timeline (call log, email sent, meeting notes, stage change
/// annotation…).
/// </summary>
public class OpportunityActivity
{
    public int Id { get; set; }

    public int OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;

    public OpportunityActivityType Type { get; set; }

    /// <summary>When the interaction actually took place (may be back-dated).</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string Content { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum OpportunityActivityType
{
    Call = 0,
    Email = 1,
    Meeting = 2,
    Note = 3,
    StageChange = 4,
}
