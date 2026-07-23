namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Filters + pagination for GET /api/as-built-documents.</summary>
public class AsBuiltDocumentListParams
{
    public int? DesignProjectId { get; set; }
    public string? Category { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public bool OpenOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>Payload for POST /api/as-built-documents.</summary>
public class CreateAsBuiltDocumentRequest
{
    public int DesignProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileUrl { get; set; }
    public string? Note { get; set; }
}

/// <summary>Payload for PUT /api/as-built-documents/{id}.</summary>
public class UpdateAsBuiltDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileUrl { get; set; }
    public string? Note { get; set; }
}

/// <summary>Payload for POST /api/as-built-documents/{id}/status.</summary>
public class TransitionAsBuiltStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>Payload for POST /api/as-built-documents/bulk-delete.</summary>
public class BulkDeleteAsBuiltDocumentsRequest
{
    public List<int> Ids { get; set; } = new();
}
