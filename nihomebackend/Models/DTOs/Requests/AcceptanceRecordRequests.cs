namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Filters + pagination for GET /api/acceptance-records.</summary>
public class AcceptanceRecordListParams
{
    public int? DesignProjectId { get; set; }
    public string? Status { get; set; }
    public int? ConstructionTaskId { get; set; }
    public string? Search { get; set; }
    public bool OverdueOnly { get; set; }
    public bool OpenOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Payload for POST /api/acceptance-records.</summary>
public class CreateAcceptanceRecordRequest
{
    public int DesignProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ConstructionTaskId { get; set; }
    public DateOnly AcceptanceDate { get; set; }
    public string? Location { get; set; }
    public string? Participants { get; set; }
    public string? Findings { get; set; }
    public string? Documents { get; set; }
}

/// <summary>Payload for PUT /api/acceptance-records/{id}.</summary>
public class UpdateAcceptanceRecordRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ConstructionTaskId { get; set; }
    public DateOnly AcceptanceDate { get; set; }
    public string? Location { get; set; }
    public string? Participants { get; set; }
    public string? Findings { get; set; }
    public string? Documents { get; set; }
}

/// <summary>Payload for POST /api/acceptance-records/{id}/status.</summary>
public class TransitionAcceptanceStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
}

/// <summary>Payload for POST /api/acceptance-records/bulk-delete.</summary>
public class BulkDeleteAcceptanceRecordsRequest
{
    public List<int> Ids { get; set; } = new();
}
