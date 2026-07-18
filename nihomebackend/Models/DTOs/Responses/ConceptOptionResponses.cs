namespace NihomeBackend.Models.DTOs.Responses;

public class ConceptOptionResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? InternalNote { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? PresentedAt { get; set; }
    /// <summary>Enum name — Drafting / PendingInternalReview / PresentedToClient / ClientRequestedChanges / Finalized / Discarded.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ConceptOptionListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ConceptOptionResponse> Items { get; set; } = new();
}
