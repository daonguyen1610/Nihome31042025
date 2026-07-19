using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create payload for a new IFC release (NIH-118). Starts as Draft.</summary>
public class CreateIfcReleaseRequest
{
    [Required]
    public int DesignProjectId { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>Update payload — only allowed while Draft.</summary>
public class UpdateIfcReleaseRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class AddIfcReleaseItemsRequest
{
    [Required]
    [MinLength(1)]
    public List<int> ShopDrawingIds { get; set; } = new();
}

public class AddIfcReleaseRecipientRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Master-data code from <c>ifc_recipient_type</c>.</summary>
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string RecipientTypeCode { get; set; } = string.Empty;
}

public class AcknowledgeIfcReleaseRecipientRequest
{
    [StringLength(1000)]
    public string? AcknowledgementNote { get; set; }
}

public class IfcReleaseListParams
{
    public int? DesignProjectId { get; set; }
    /// <summary>Comma-separated <c>IfcReleaseStatus</c> values.</summary>
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
