namespace NihomeBackend.Models.DTOs.Responses;

public class AcceptanceRecordResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string DesignProjectName { get; set; } = string.Empty;
    public string AcceptanceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ConstructionTaskId { get; set; }
    public string? ConstructionTaskName { get; set; }
    public DateOnly AcceptanceDate { get; set; }
    public string? Location { get; set; }
    public string? Participants { get; set; }
    public string? Findings { get; set; }
    public string? ResolutionNote { get; set; }
    public string? Documents { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public int RevisionCount { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }

    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public string? RejectedByName { get; set; }

    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

public class AcceptanceRecordListResponse
{
    public List<AcceptanceRecordResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    /// <summary>Count per status for the currently-filtered project scope.</summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public int OverdueCount { get; set; }
}

public class AcceptanceRecordBulkDeleteResponse
{
    public List<int> DeletedIds { get; set; } = new();
    public List<int> SkippedIds { get; set; } = new();
}
