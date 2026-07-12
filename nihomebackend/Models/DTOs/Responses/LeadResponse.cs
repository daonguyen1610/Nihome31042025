namespace NihomeBackend.Models.DTOs.Responses;

public class LeadResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public LeadStatus Status { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public string? Note { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public int? ConvertedOpportunityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<LeadActivityResponse> Activities { get; set; } = new();
}

public class LeadActivityResponse
{
    public int Id { get; set; }
    public LeadActivityType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}
