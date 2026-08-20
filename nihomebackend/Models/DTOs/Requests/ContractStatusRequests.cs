using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Ask the server to transition the contract's status. Transitions
/// enforce business rules (see <c>ContractService.TransitionAsync</c>).</summary>
public class ContractStatusTransitionRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    public ContractStatus NewStatus { get; set; }
}

/// <summary>Update a single milestone's status (Pending / Requested / Paid).
/// Used by the Payment schedule tab actions.</summary>
public class UpdateMilestoneStatusRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    public PaymentMilestoneStatus Status { get; set; }
}
