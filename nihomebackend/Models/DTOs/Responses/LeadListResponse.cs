namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Paginated envelope for lead list endpoint.</summary>
public class LeadListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<LeadResponse> Items { get; set; } = new();

    /// <summary>Records per status under every other filter, for the list quick views.</summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}
