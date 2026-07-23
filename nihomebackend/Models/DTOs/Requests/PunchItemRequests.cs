namespace NihomeBackend.Models.DTOs.Requests;

public class PunchItemListParams
{
    public int? DesignProjectId { get; set; }
    public int? AssigneeUserId { get; set; }
    /// <summary>Comma-separated status names.</summary>
    public string? Status { get; set; }
    /// <summary>Comma-separated severity names.</summary>
    public string? Severity { get; set; }
    public bool? OverdueOnly { get; set; }
    /// <summary>When true, hides Verified + Cancelled.</summary>
    public bool? OpenOnly { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class CreatePunchItemRequest
{
    public int DesignProjectId { get; set; }
    public string? PunchCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Severity { get; set; } = "Medium";
    public int? AssigneeUserId { get; set; }
    public DateOnly? Deadline { get; set; }
    public string? Note { get; set; }
}

public class UpdatePunchItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Severity { get; set; } = "Medium";
    public int? AssigneeUserId { get; set; }
    public DateOnly? Deadline { get; set; }
    public string? ResolutionNote { get; set; }
    public string? Note { get; set; }
}

public class TransitionPunchStatusRequest
{
    public string Status { get; set; } = string.Empty;
    /// <summary>Optional note captured with the transition (audit trail).</summary>
    public string? ResolutionNote { get; set; }
}

public class BulkDeletePunchItemsRequest
{
    public List<int> Ids { get; set; } = new();
}
