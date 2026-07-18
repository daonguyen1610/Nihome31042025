using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Payload used by both create and update. Number is optional on
/// create (server auto-generates) but must round-trip on update.</summary>
public class UpsertContractRequest
{
    /// <summary>Human-friendly contract number. Optional on create — the
    /// server generates <c>HD-YYYY-NNNN</c> when empty.</summary>
    [StringLength(40)]
    public string? ContractNumber { get; set; }

    [Required]
    public int CustomerId { get; set; }

    public int? OpportunityId { get; set; }
    public int? QuoteId { get; set; }
    public int? OwnerUserId { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public DateTime? SignedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Value { get; set; }

    [StringLength(20000)]
    public string? ScopeOfWork { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    /// <summary>
    /// Ordered payment schedule. When null the milestones list is left
    /// untouched by the server (useful for a status-only patch); when an
    /// empty list is sent the existing schedule is fully wiped; when a
    /// list is sent every existing milestone is replaced by it and Σ%
    /// must equal 100 (validated server-side).
    /// </summary>
    public List<ContractPaymentMilestoneRequest>? PaymentMilestones { get; set; }
}

/// <summary>One line in a contract's payment schedule.</summary>
public class ContractPaymentMilestoneRequest
{
    [Range(1, 99)]
    public int Order { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 100)]
    public decimal PercentValue { get; set; }

    public DateTime? DueDate { get; set; }

    public PaymentMilestoneStatus Status { get; set; } = PaymentMilestoneStatus.Pending;

    [StringLength(500)]
    public string? Note { get; set; }
}
