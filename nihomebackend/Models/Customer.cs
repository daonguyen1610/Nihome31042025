namespace NihomeBackend.Models;

/// <summary>
/// CRM Customer — canonical customer record used by every downstream module
/// (Opportunity, Quote, Contract, Project, Invoice). A customer is either
/// an individual (personal buyer) or a company (with tax id + representative).
/// </summary>
public class Customer
{
    public int Id { get; set; }

    public CustomerType Type { get; set; } = CustomerType.Individual;

    /// <summary>Display name — person name (Individual) or company name (Company).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tax identification number (Vietnamese MST). Required when <see cref="Type"/> is Company.</summary>
    public string? TaxId { get; set; }

    /// <summary>Registered address. Required when <see cref="Type"/> is Company.</summary>
    public string? Address { get; set; }

    /// <summary>Legal representative name. Required when <see cref="Type"/> is Company.</summary>
    public string? RepresentativeName { get; set; }

    /// <summary>Master-data code from <c>customer_source</c> (marketing, referral, website…).</summary>
    public string SourceCode { get; set; } = string.Empty;

    public CustomerRelationshipStatus RelationshipStatus { get; set; } = CustomerRelationshipStatus.Prospect;

    /// <summary>Sales owner. Nullable while unassigned; SetNull on user delete.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<CustomerContact> Contacts { get; set; } = new();
    public List<CustomerActivity> Activities { get; set; } = new();
}

public enum CustomerType
{
    Individual = 0,
    Company = 1,
}

public enum CustomerRelationshipStatus
{
    Prospect = 0,
    InProgress = 1,
    Signed = 2,
    Suspended = 3,
}
