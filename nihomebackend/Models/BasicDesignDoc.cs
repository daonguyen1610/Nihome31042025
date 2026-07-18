namespace NihomeBackend.Models;

/// <summary>
/// M2 Basic Design document (Giai đoạn 2 - Thiết kế Cơ sở) — NIH-115.
/// A <see cref="DesignProject"/> can host N documents per discipline
/// (Kiến trúc / Kết cấu / MEP + optional Interior — sourced from master
/// data <c>design_discipline</c>). Once each of the three core disciplines
/// has at least one <see cref="BasicDesignDocStatus.InternallyApproved"/>
/// document, the parent project is allowed to transition to
/// <see cref="DesignProjectStage.ShopDrawing"/>.
///
/// Slice 1 (NIH-115): CRUD metadata + status transitions + Shop Drawing
/// unlock guard. Slice 2 (deferred): file upload, per-discipline default
/// checklist templates, attach-to-permit bridge with NIH-137.
/// </summary>
public class BasicDesignDoc
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Master-data code from category <c>design_discipline</c>.</summary>
    public string DisciplineCode { get; set; } = string.Empty;

    /// <summary>Human-friendly drawing code (KT-BD-001, KC-BD-014, MEP-BD-007), unique per project.</summary>
    public string DocumentCode { get; set; } = string.Empty;

    /// <summary>Drawing / spec title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Rich-text description or scope of the drawing.</summary>
    public string? Description { get; set; }

    /// <summary>Designer / engineer responsible for the doc.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public BasicDesignDocStatus Status { get; set; } = BasicDesignDocStatus.InProgress;

    /// <summary>Free-text working note.</summary>
    public string? Note { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Lifecycle of a <see cref="BasicDesignDoc"/>.
/// <list type="bullet">
///   <item><term>InProgress</term><description>Being drafted by the discipline team.</description></item>
///   <item><term>SubmittedForReview</term><description>Awaiting Design Lead internal review.</description></item>
///   <item><term>InternallyApproved</term><description>Green-lit inside the company. Counts toward Shop Drawing unlock.</description></item>
///   <item><term>SubmittedForPermit</term><description>Package sent to the permit authority (bridges to NIH-137).</description></item>
///   <item><term>PermitApproved</term><description>Authority has issued the permit for this drawing.</description></item>
///   <item><term>Rejected</term><description>Terminal. Owner should clone into a fresh drawing.</description></item>
/// </list>
/// </summary>
public enum BasicDesignDocStatus
{
    InProgress = 0,
    SubmittedForReview = 1,
    InternallyApproved = 2,
    SubmittedForPermit = 3,
    PermitApproved = 4,
    Rejected = 5,
}
