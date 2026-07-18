namespace NihomeBackend.Models.DTOs.Responses;

public class ShopDrawingResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }

    public string DisciplineCode { get; set; } = string.Empty;
    /// <summary>Localised label resolved from <c>design_discipline</c> master data.</summary>
    public string? DisciplineLabel { get; set; }

    public string ConstructionItem { get; set; } = string.Empty;
    public string DrawingCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    /// <summary>Enum name — Drafting / InReview / Approved / PendingIfc / Released / Rejected.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ShopDrawingListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ShopDrawingResponse> Items { get; set; } = new();

    /// <summary>
    /// Per-status roll-up for the current project scope so the tab can
    /// render header stat pills without a second round-trip. Keys are
    /// <see cref="Models.ShopDrawingStatus"/> enum names.
    /// </summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

/// <summary>Bulk delete result — per-id success + failure list.</summary>
public class ShopDrawingBulkDeleteResponse
{
    public int Requested { get; set; }
    public int Deleted { get; set; }
    public List<ShopDrawingBulkDeleteFailure> Failures { get; set; } = new();
}

public class ShopDrawingBulkDeleteFailure
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
