namespace NihomeBackend.Models;

/// <summary>
/// CRM Opportunity — a qualified sales pursuit against a specific
/// <see cref="Customer"/>. Sits between Customer and Quote/Contract in the
/// sales funnel (Lead → Customer → Opportunity → Quote/Bid → Contract).
/// </summary>
public class Opportunity
{
    public int Id { get; set; }

    /// <summary>Display name of the opportunity, e.g. "Nhà máy Alpha giai đoạn 2".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Owning customer — hard FK, opportunity cannot exist without it.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Sales owner. Nullable while unassigned; SetNull on user delete.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Estimated deal value in VND (≥ 0).</summary>
    public decimal EstimatedValue { get; set; }

    /// <summary>Win probability 0-100 (%).</summary>
    public int WinProbability { get; set; }

    /// <summary>Expected close date (business-provided, nullable).</summary>
    public DateTime? ExpectedCloseDate { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;

    /// <summary>Optional master-data code (<c>opportunity_lost_reason</c>) — required when Stage=Lost.</summary>
    public string? LostReasonCode { get; set; }

    /// <summary>Free-text note captured on Lost transition (required when Stage=Lost).</summary>
    public string? LostNote { get; set; }

    /// <summary>Timestamp of the transition into a terminal stage (Won or Lost).</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Optional Quote id captured on Won (forward-compatible with NIH-84).</summary>
    public int? WonQuoteId { get; set; }

    /// <summary>Optional Tender id captured on Won (forward-compatible with NIH-85).</summary>
    public int? WonTenderId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<OpportunityActivity> Activities { get; set; } = new();
}

/// <summary>
/// Six-stage opportunity pipeline. Order is meaningful — service enforces
/// forward-only progression once a terminal stage (Won/Lost) is reached.
/// </summary>
public enum OpportunityStage
{
    Prospecting = 0,
    Qualification = 1,
    Proposal = 2,
    Negotiation = 3,
    Won = 4,
    Lost = 5,
}
