using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create payload for a new drawing revision (NIH-117).</summary>
public class CreateDrawingRevisionRequest
{
    /// <summary>Enum name — <c>BasicDesignDoc</c> or <c>ShopDrawing</c>.</summary>
    [Required]
    public string TargetType { get; set; } = string.Empty;

    [Required]
    public int TargetId { get; set; }

    /// <summary>Master-data code from <c>drawing_revision_reason</c>.</summary>
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Mandatory free-text explanation.</summary>
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Note { get; set; } = string.Empty;
}

/// <summary>List filter — one target at a time (drawing history view).</summary>
public class DrawingRevisionListParams
{
    /// <summary>Enum name — <c>BasicDesignDoc</c> or <c>ShopDrawing</c>.</summary>
    public string? TargetType { get; set; }
    public int? TargetId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public class DrawingRevisionDiffParams
{
    [Required]
    public int FromId { get; set; }

    [Required]
    public int ToId { get; set; }
}
