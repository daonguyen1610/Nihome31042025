using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateOpportunityRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    /// <summary>Optional explicit owner. If null, service assigns the caller (when the caller is sales).</summary>
    public int? OwnerUserId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedValue { get; set; }

    [Range(0, 100)]
    public int WinProbability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    /// <summary>Initial stage. Defaults to Prospecting; cannot be Won/Lost on create.</summary>
    public OpportunityStage Stage { get; set; } = OpportunityStage.Prospecting;

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class UpdateOpportunityRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public int? OwnerUserId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedValue { get; set; }

    [Range(0, 100)]
    public int WinProbability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>
/// Dedicated payload for stage transitions — captured via
/// <c>PATCH /api/opportunities/{id}/stage</c>. Won requires at least one of
/// WonQuoteId / WonTenderId; Lost requires LostReasonCode + LostNote.
/// </summary>
public class ChangeOpportunityStageRequest
{
    [Required]
    public OpportunityStage TargetStage { get; set; }

    public int? WonQuoteId { get; set; }
    public int? WonTenderId { get; set; }

    [StringLength(60)]
    public string? LostReasonCode { get; set; }

    [StringLength(2000)]
    public string? LostNote { get; set; }
}

public class AddOpportunityActivityRequest
{
    [Required]
    public OpportunityActivityType Type { get; set; }

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public DateTime? OccurredAt { get; set; }
}
