using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload for POST /api/tenders/{id}/mark-won. Sales Manager gate
/// (crm.tenders.mark-result). A won tender continues as an opportunity in
/// negotiation: either an existing opportunity of the same customer, or one
/// created from the tender (with its project) when CreateOpportunity is set.
/// Exactly one of the two is required. A free-text note is captured for the
/// audit trail.
/// </summary>
public class MarkTenderWonRequest
{
    public int? OpportunityId { get; set; }

    public bool CreateOpportunity { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>
/// Payload for POST /api/tenders/{id}/mark-lost. Reason code comes from the
/// <c>opportunity_lost_reason</c> master-data category so wording stays
/// consistent with the opportunity funnel. Free-text note explains context.
/// </summary>
public class MarkTenderLostRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string ReasonCode { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Note { get; set; }
}
