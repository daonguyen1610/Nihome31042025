namespace NihomeBackend.Models.DTOs.Responses;

public class AsBuiltDocumentResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string DesignProjectName { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public class AsBuiltDocumentListResponse
{
    public List<AsBuiltDocumentResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// <summary>Count per status for the current project scope.</summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    /// <summary>Count per category for the current project scope.</summary>
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    /// <summary>
    /// Number of required categories that have at least one Approved document
    /// in the current project scope. Only meaningful when the list is
    /// filtered by a single project.
    /// </summary>
    public int CompletedRequiredCategories { get; set; }
    /// <summary>Total number of required categories (constant per business rule).</summary>
    public int TotalRequiredCategories { get; set; }
}

public class AsBuiltDocumentBulkDeleteResponse
{
    public List<int> DeletedIds { get; set; } = new();
    public List<int> SkippedIds { get; set; } = new();
}
