namespace NihomeBackend.Models;

/// <summary>
/// M2 Concept Design option (Giai đoạn 1 - Thiết kế Sơ bộ) — NIH-114.
/// A <see cref="DesignProject"/> can have N Concept options in parallel;
/// exactly one may reach the <see cref="ConceptOptionStatus.Finalized"/>
/// state which locks the others as <see cref="ConceptOptionStatus.Discarded"/>
/// and unlocks the parent project's Basic Design stage.
///
/// Slice 1 (NIH-114): metadata + status transitions + finalize workflow.
/// Slice 2 (deferred): media uploads (3D + floor plans + walkthrough video),
/// per-file client-feedback threads, PDF export.
/// </summary>
public class ConceptOption
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Free-text option name — e.g. "Phương án A - hiện đại".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Rich-text description shown on the client-facing preview page.</summary>
    public string? Description { get; set; }

    /// <summary>Internal working note visible only to the design team.</summary>
    public string? InternalNote { get; set; }

    /// <summary>User responsible for driving this option through review.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Date the option was presented to the client (nullable until it happens).</summary>
    public DateTime? PresentedAt { get; set; }

    public ConceptOptionStatus Status { get; set; } = ConceptOptionStatus.Drafting;

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Lifecycle of a <see cref="ConceptOption"/>.
///
/// <list type="bullet">
///   <item><term>Drafting</term><description>Design team is still shaping the option.</description></item>
///   <item><term>PendingInternalReview</term><description>Design Lead review before client show.</description></item>
///   <item><term>PresentedToClient</term><description>Sent to the client for feedback.</description></item>
///   <item><term>ClientRequestedChanges</term><description>Client asked for edits; back to Drafting after applied.</description></item>
///   <item><term>Finalized</term><description>The chosen option. At most one per project.</description></item>
///   <item><term>Discarded</term><description>Locked read-only after another option is finalized (or manually discarded).</description></item>
/// </list>
/// </summary>
public enum ConceptOptionStatus
{
    Drafting = 0,
    PendingInternalReview = 1,
    PresentedToClient = 2,
    ClientRequestedChanges = 3,
    Finalized = 4,
    Discarded = 5,
}
