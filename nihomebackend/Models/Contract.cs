namespace NihomeBackend.Models;

/// <summary>
/// CRM Contract (hợp đồng) — the signed commitment sitting at the end of the
/// sales funnel (<see cref="Lead"/> → <see cref="Customer"/> →
/// <see cref="Opportunity"/> → <see cref="Quote"/> → <see cref="Contract"/>).
///
/// NIH-102 scope covers the header record + list surface. Payment milestones,
/// variation orders, and the tabbed detail page are follow-up stories
/// (NIH-103, NIH-104). Runtime routing of the seeded <c>contracts/sign</c>
/// approval workflow is out of scope here as well.
/// </summary>
public class Contract
{
    public int Id { get; set; }

    /// <summary>Human-friendly contract number (e.g. HD-2026-0001) — unique.</summary>
    public string ContractNumber { get; set; } = string.Empty;

    /// <summary>Owning customer — hard FK. Every contract must be tied to a customer.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Optional source opportunity (nullable — a contract can be drafted directly).</summary>
    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    /// <summary>Optional source quote used as the pricing baseline.</summary>
    public int? QuoteId { get; set; }
    public Quote? Quote { get; set; }

    /// <summary>Sales owner — Sales role sees only rows owned by their user unless view.all.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    /// <summary>Date the contract was signed. Null until moved out of Draft.</summary>
    public DateTime? SignedDate { get; set; }

    /// <summary>Planned execution window.</summary>
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Face value in VND (base amount, before VO adjustments).</summary>
    public decimal Value { get; set; }

    /// <summary>Free-text scope of work (rich-text HTML is fine — sanitised at render time).</summary>
    public string? ScopeOfWork { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public enum ContractStatus
{
    /// <summary>Being drafted. No obligations yet.</summary>
    Draft = 0,
    /// <summary>Signed by both sides but execution has not started.</summary>
    Signed = 1,
    /// <summary>Execution in progress — the operational status where deadline warnings apply.</summary>
    InProgress = 2,
    /// <summary>Temporarily paused by mutual agreement.</summary>
    OnHold = 3,
    /// <summary>All milestones fulfilled and closed.</summary>
    Completed = 4,
    /// <summary>Cancelled before or during execution.</summary>
    Cancelled = 5,
}
