namespace NihomeBackend.Models;

/// <summary>
/// M2 Shop Drawing (Giai đoạn 3 - Thiết kế Chi tiết) — NIH-116.
/// A <see cref="DesignProject"/> hosts N shop drawings, organised by
/// discipline (<c>design_discipline</c> master data — Kiến trúc / Kết cấu
/// / MEP / Nội thất) then further grouped inside each discipline by a
/// human-typed <see cref="ConstructionItem"/> label (VD "Móng cọc",
/// "Trần thạch cao tầng 3"). Free text keeps slice 1 flexible — a per-
/// project catalog can be layered in slice 2 without a data migration.
///
/// Shop drawings are only created once the parent project has reached
/// <see cref="DesignProjectStage.ShopDrawing"/> (unlocked by the Basic
/// Design readiness gate from NIH-115). The state-machine ends at
/// <see cref="ShopDrawingStatus.PendingIfc"/>; the actual
/// <see cref="ShopDrawingStatus.Released"/> transition is flipped by the
/// IFC release flow shipping in NIH-118.
///
/// Slice 1 (NIH-116): metadata + state-machine + bulk delete (drafts).
/// Slice 2 (deferred): source-file/PDF upload, cross-discipline review
/// workflow, in-drawing markup, IFC bundle bridge with NIH-118.
/// </summary>
public class ShopDrawing
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Master-data code from category <c>design_discipline</c>.</summary>
    public string DisciplineCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-typed construction item / package the drawing belongs to
    /// (e.g. "Móng cọc", "Trần thạch cao tầng 3"). Free text so PMs can
    /// group drawings the way the site team actually asks for them.
    /// </summary>
    public string ConstructionItem { get; set; } = string.Empty;

    /// <summary>Human-friendly drawing code (KT-SD-001, KC-SD-014, MEP-SD-007, NT-SD-002), unique per project.</summary>
    public string DrawingCode { get; set; } = string.Empty;

    /// <summary>Drawing / spec title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Rich-text description or scope of the drawing.</summary>
    public string? Description { get; set; }

    /// <summary>Designer / engineer responsible for the doc.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public ShopDrawingStatus Status { get; set; } = ShopDrawingStatus.Drafting;

    /// <summary>Free-text working note.</summary>
    public string? Note { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Lifecycle of a <see cref="ShopDrawing"/> (NIH-116 slice 1).
/// <list type="bullet">
///   <item><term>Drafting</term><description>Being drawn by the discipline team.</description></item>
///   <item><term>InReview</term><description>Awaiting Design Lead / cross-discipline review.</description></item>
///   <item><term>Approved</term><description>Green-lit by Design Lead. Eligible to be bundled into an IFC release.</description></item>
///   <item><term>PendingIfc</term><description>Queued for the next IFC release bundle. Locked from edits.</description></item>
///   <item><term>Released</term><description>Set by the IFC release flow (NIH-118) — watermark + full lock.</description></item>
///   <item><term>Rejected</term><description>Terminal. Owner should clone into a fresh drawing.</description></item>
/// </list>
/// </summary>
public enum ShopDrawingStatus
{
    Drafting = 0,
    InReview = 1,
    Approved = 2,
    PendingIfc = 3,
    Released = 4,
    Rejected = 5,
}
