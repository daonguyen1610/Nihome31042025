namespace NihomeBackend.Models.DTOs.Responses;

public class DrawingRevisionResponse
{
    public int Id { get; set; }

    /// <summary>Enum name — <c>BasicDesignDoc</c> or <c>ShopDrawing</c>.</summary>
    public string TargetType { get; set; } = string.Empty;
    public int TargetId { get; set; }

    /// <summary>Human-friendly drawing code of the target (KT-BD-001, KT-SD-001 …).</summary>
    public string? TargetCode { get; set; }

    /// <summary>Free-text title of the target drawing.</summary>
    public string? TargetTitle { get; set; }

    public int RevisionNumber { get; set; }
    /// <summary>Display label — <c>R1</c>, <c>R2</c>, …</summary>
    public string RevisionLabel => $"R{RevisionNumber}";

    public string ReasonCode { get; set; } = string.Empty;
    /// <summary>Localised label resolved from <c>drawing_revision_reason</c>.</summary>
    public string? ReasonLabel { get; set; }

    public string Note { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsSuperseded => !IsCurrent;

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

public class DrawingRevisionListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<DrawingRevisionResponse> Items { get; set; } = new();
}

/// <summary>Diff between two revisions of the same target.</summary>
public class DrawingRevisionDiffResponse
{
    public DrawingRevisionResponse From { get; set; } = new();
    public DrawingRevisionResponse To { get; set; } = new();
    /// <summary>Human-friendly diff messages (metadata only in slice 1).</summary>
    public List<string> Changes { get; set; } = new();
}
