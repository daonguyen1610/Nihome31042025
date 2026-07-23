namespace NihomeBackend.Models.DTOs.Responses;

public class PunchItemResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string? DesignProjectName { get; set; }

    public string PunchCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";

    public int? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public DateOnly? Deadline { get; set; }

    public string? ResolutionNote { get; set; }
    public int ReopenCount { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public string? VerifiedByName { get; set; }

    public string? Note { get; set; }

    /// <summary>Server-computed: Deadline &lt; today &amp; not Verified/Cancelled.</summary>
    public bool IsOverdue { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PunchItemListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<PunchItemResponse> Items { get; set; } = new();
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public int OverdueCount { get; set; }
}

public class PunchItemBulkDeleteResponse
{
    public int Requested { get; set; }
    public int Deleted { get; set; }
    public List<PunchItemBulkDeleteFailure> Failures { get; set; } = new();
}

public class PunchItemBulkDeleteFailure
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
