namespace NihomeBackend.Models;

/// <summary>
/// M2 Drawing Revision (Quản lý phiên bản) — NIH-117.
///
/// Attaches to either a <see cref="BasicDesignDoc"/> or a
/// <see cref="ShopDrawing"/> through a polymorphic
/// <see cref="TargetType"/> + <see cref="TargetId"/> pair. Every time a
/// designer or reviewer says "the drawing changed", we append a new
/// revision row and flip the previous latest to <see cref="IsCurrent"/>
/// = false. Revision numbers increment per-target (R1, R2, R3…) — the
/// living drawing entity itself is implicitly R0.
///
/// Slice 1 (NIH-117) — metadata + reason + immutability:
/// * Auto-allocated <see cref="RevisionNumber"/> per target.
/// * Reason sourced from master data <c>drawing_revision_reason</c>.
/// * Prior revisions auto-marked superseded on new revision create.
/// * No physical delete (spec: revisions are immutable + audit-safe).
/// * No update endpoint (immutability).
///
/// Slice 2 (deferred): file diff (attaches the source + PDF snapshot),
/// notifications to Design team + PM + on-site crew when a released
/// drawing is revised, cross-project revision search.
/// </summary>
public class DrawingRevision
{
    public int Id { get; set; }

    /// <summary>Polymorphic target: BasicDesignDoc or ShopDrawing.</summary>
    public DrawingRevisionTargetType TargetType { get; set; }

    /// <summary>Id of the parent drawing row inside its own table.</summary>
    public int TargetId { get; set; }

    /// <summary>Sequential number per-target, starts at 1. Rendered as "R1".</summary>
    public int RevisionNumber { get; set; }

    /// <summary>Master-data code from category <c>drawing_revision_reason</c>.</summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Free-text explanation of the change (mandatory per spec AC #2).</summary>
    public string Note { get; set; } = string.Empty;

    /// <summary>Exactly one revision per target is current — the newest one.</summary>
    public bool IsCurrent { get; set; }

    // --- Audit (immutable — no UpdatedAt/By) ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}

/// <summary>Which drawing family a revision attaches to.</summary>
public enum DrawingRevisionTargetType
{
    BasicDesignDoc = 0,
    ShopDrawing = 1,
}
