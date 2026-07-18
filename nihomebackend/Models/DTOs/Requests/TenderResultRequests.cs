using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload for POST /api/tenders/{id}/mark-won. Sales Manager gate
/// (crm.tenders.mark-result). Requires a linked opportunity so the
/// downstream contract-generation flow (later story) has something to
/// hang off; a free-text note is captured for the audit trail.
/// </summary>
public class MarkTenderWonRequest
{
    [Required]
    public int OpportunityId { get; set; }

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
