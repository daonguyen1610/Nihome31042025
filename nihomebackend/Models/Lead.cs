namespace NihomeBackend.Models;

/// <summary>
/// CRM Lead — first entry point of the Sales funnel (before Customer/Opportunity).
/// A lead is captured from marketing/website/referral/event/cold-call and either
/// converted into a Customer + Opportunity or discarded (NotInterested / Junk).
/// </summary>
public class Lead
{
    public int Id { get; set; }

    /// <summary>Contact name (person or primary contact of a company lead).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional company name — filled when the lead represents an organisation.</summary>
    public string? CompanyName { get; set; }

    /// <summary>At least one of <see cref="Phone"/> or <see cref="Email"/> must be provided.</summary>
    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>
    /// Master-data code from category <c>customer_source</c> (marketing, referral,
    /// website, event, cold-call, other).
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    public LeadStatus Status { get; set; } = LeadStatus.New;

    /// <summary>User currently responsible for the lead. Null while unassigned.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public string? Note { get; set; }

    // Conversion outcome — populated by /convert action. Nullable FKs because
    // Customer/Opportunity entities land in later phases.
    public DateTime? ConvertedAt { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public int? ConvertedOpportunityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<LeadActivity> Activities { get; set; } = new();
}

public enum LeadStatus
{
    New = 0,
    Contacted = 1,
    Interested = 2,
    NotInterested = 3,
    Converted = 4,
    Junk = 5,
}
