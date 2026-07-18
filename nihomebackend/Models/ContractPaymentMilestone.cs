namespace NihomeBackend.Models;

/// <summary>
/// One line in a <see cref="Contract"/>'s payment schedule. The percent
/// splits total contract value into ordered milestones so accounting can
/// track billing and receipt separately from the master record.
///
/// NIH-103 scope: create + edit the milestone list along with the contract.
/// Milestone status transitions (Pending → Requested → Paid) and overdue
/// highlighting live on the contract detail page (NIH-104).
/// </summary>
public class ContractPaymentMilestone
{
    public int Id { get; set; }

    /// <summary>Owning contract. Cascade-delete when the parent is removed.</summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    /// <summary>1-based order within the schedule. Unique per contract.</summary>
    public int Order { get; set; }

    /// <summary>Short display name, e.g. "Đợt 1 - Tạm ứng".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Percent of the contract Value this milestone represents.</summary>
    public decimal PercentValue { get; set; }

    /// <summary>Planned billing / payment date.</summary>
    public DateTime? DueDate { get; set; }

    public PaymentMilestoneStatus Status { get; set; } = PaymentMilestoneStatus.Pending;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentMilestoneStatus
{
    /// <summary>Planned but not yet requested from the customer.</summary>
    Pending = 0,
    /// <summary>Invoice / payment request sent, awaiting funds.</summary>
    Requested = 1,
    /// <summary>Funds received in full for this milestone.</summary>
    Paid = 2,
}
