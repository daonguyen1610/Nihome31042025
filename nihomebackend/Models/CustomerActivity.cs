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
    /// Recorded for a past day without a time (NICON feedback row 23). OccurredAt then holds
    /// midday of that day in the display time zone, so entries still sort by day.
    /// </summary>
    public bool IsDateOnly { get; set; }

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
