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

    public string? ScopeOfWork { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}
