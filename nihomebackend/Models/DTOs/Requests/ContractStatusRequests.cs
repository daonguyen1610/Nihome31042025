using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Ask the server to transition the contract's status. Transitions
/// enforce business rules (see <c>ContractService.TransitionAsync</c>).</summary>
/// <summary>Attach an approved quote to an existing customer contract.</summary>
public class LinkContractQuoteRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Range(1, int.MaxValue)]
    public int QuoteId { get; set; }
}

public class ContractStatusTransitionRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    public ContractStatus NewStatus { get; set; }
}

/// <summary>
/// Send a Draft contract for internal review. <see cref="ReviewerUserIds"/> are the people
/// tagged to review first, in order; <see cref="ApproverUserIds"/> holds one chosen person per
/// workflow step that is not approved by everyone in its role.
/// </summary>
public class SubmitContractReviewRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }
    public List<int> ReviewerUserIds { get; set; } = [];
    public List<int> ApproverUserIds { get; set; } = [];
}

/// <summary>The current reviewer's decision. A rejection needs a reason.</summary>
public class ContractReviewDecisionRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

/// <summary>Update a single milestone's status (Pending / Requested / Paid).
/// Used by the Payment schedule tab actions.</summary>
public class UpdateMilestoneStatusRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    public PaymentMilestoneStatus Status { get; set; }

    public DateTime? ActualPaymentDate { get; set; }

    public int? ResponsibleAccountantUserId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}
