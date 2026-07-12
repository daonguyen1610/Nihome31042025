namespace NihomeBackend.Models;

/// <summary>
/// A single follow-up entry on a <see cref="Lead"/> — used to build the
/// customer-care timeline (call log, email sent, meeting notes…).
/// </summary>
public class LeadActivity
{
    public int Id { get; set; }

    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public LeadActivityType Type { get; set; }

    public string Content { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum LeadActivityType
{
    Call = 0,
    Email = 1,
    Meeting = 2,
    Note = 3,
}
