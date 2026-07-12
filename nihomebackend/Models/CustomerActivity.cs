namespace NihomeBackend.Models;

/// <summary>
/// A single follow-up entry on a <see cref="Customer"/> — call log, email,
/// meeting notes… feeds the customer-care timeline. Attachment support is
/// tracked as a follow-up ticket.
/// </summary>
public class CustomerActivity
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public CustomerActivityType Type { get; set; }

    /// <summary>When the interaction actually took place (may be back-dated).</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string Content { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum CustomerActivityType
{
    Call = 0,
    Email = 1,
    Meeting = 2,
    Note = 3,
}
