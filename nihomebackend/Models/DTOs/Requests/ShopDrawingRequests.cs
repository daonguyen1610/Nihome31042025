using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create payload for a Shop Drawing (NIH-116).</summary>
public class CreateShopDrawingRequest
{
    [Required]
    public int DesignProjectId { get; set; }

    /// <summary>Master-data code from <c>design_discipline</c> (architecture / structure / mep / interior).</summary>
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string DisciplineCode { get; set; } = string.Empty;

    /// <summary>Free-text construction item / package the drawing belongs to.</summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ConstructionItem { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>Update payload. Status transitions use a dedicated /status endpoint.</summary>
public class UpdateShopDrawingRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string DisciplineCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string ConstructionItem { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class TransitionShopDrawingStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class ShopDrawingListParams
{
    public int? DesignProjectId { get; set; }
    /// <summary>Master-data code from <c>design_discipline</c>.</summary>
    public string? DisciplineCode { get; set; }
    /// <summary>Filter by construction item (case-insensitive contains).</summary>
    public string? ConstructionItem { get; set; }
    /// <summary>Comma-separated <c>ShopDrawingStatus</c> values.</summary>
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Body for bulk delete — used by the Shop Drawing tab so PMs can clean
/// up abandoned drafts in one shot. Server-side each id is validated
/// independently and the response reports partial success.
/// </summary>
public class BulkDeleteShopDrawingsRequest
{
    [Required]
    [MinLength(1)]
    public List<int> Ids { get; set; } = new();
}
