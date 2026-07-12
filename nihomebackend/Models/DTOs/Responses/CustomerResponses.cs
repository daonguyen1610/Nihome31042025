namespace NihomeBackend.Models.DTOs.Responses;

public class CustomerContactResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CustomerActivityResponse
{
    public int Id { get; set; }
    public CustomerActivityType Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerResponse
{
    public int Id { get; set; }
    public CustomerType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Address { get; set; }
    public string? RepresentativeName { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public CustomerRelationshipStatus RelationshipStatus { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CustomerContactResponse> Contacts { get; set; } = new();
    public List<CustomerActivityResponse> Activities { get; set; } = new();
}

public class CustomerListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<CustomerResponse> Items { get; set; } = new();
}

/// <summary>
/// Returned when a caller tries to save a customer whose TaxId (Company)
/// or primary Phone (Individual) already exists and no
/// <c>DuplicateOverrideReason</c> was supplied. Mapped to HTTP 409 so the
/// FE can show the collision + prompt for a reason before retrying.
/// </summary>
public class CustomerDuplicateResponse
{
    public string Field { get; set; } = string.Empty;   // "TaxId" | "Phone"
    public string Value { get; set; } = string.Empty;
    public int ExistingCustomerId { get; set; }
    public string ExistingCustomerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
