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

    /// <summary>Optional date to call back or visit the customer ("nhắc gọi lại, nhắc thăm").</summary>
    public DateOnly? FollowUpDate { get; set; }

    /// <summary>
    /// The care date when the entry was recorded for a past day without a time (NICON feedback
    /// row 23). It is shown as is, whatever the display time zone in Settings becomes; OccurredAt
    /// holds midday of that day in the zone at save time so entries still sort by day.
    /// </summary>
    public DateOnly? OccurredOn { get; set; }

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
    LegalRepresentativeAssigned = 4,
    LegalRepresentativeCleared = 5,
}
