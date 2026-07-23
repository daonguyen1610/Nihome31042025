namespace NihomeBackend.Models;

/// <summary>
/// M4 Construction & Acceptance — Punch List item (Danh mục lỗi tồn
/// đọng / NIH-146). Tracks a defect / snag raised on site with its
/// severity, assignee, deadline and verification workflow.
///
/// Slice 1 (NIH-146): Open → InProgress → Fixed → Verified plus a
/// terminal Cancelled state. Reopen is expressed by flipping a
/// Verified/Fixed row back to Open. Photos + attachments hang off the
/// same row when Digital Assets (NIH-203) lands.
/// </summary>
public class PunchItem
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Human-friendly code, unique inside the project (P-001).</summary>
    public string PunchCode { get; set; } = string.Empty;

    /// <summary>Short summary — shown on the list row.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Long-form description — reproduction steps, context.</summary>
    public string? Description { get; set; }

    /// <summary>Free-text location on site — "Tầng 3, phòng 302".</summary>
    public string? Location { get; set; }

    public PunchSeverity Severity { get; set; } = PunchSeverity.Medium;

    public int? AssigneeUserId { get; set; }
    public ApplicationUser? Assignee { get; set; }

    /// <summary>Optional target date to have the punch resolved.</summary>
    public DateOnly? Deadline { get; set; }

    public PunchStatus Status { get; set; } = PunchStatus.Open;

    /// <summary>Contractor / crew note on how the item was fixed.</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>How many times this item was reopened — for reporting.</summary>
    public int ReopenCount { get; set; }

    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public ApplicationUser? VerifiedBy { get; set; }

    public string? Note { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Severity ladder — used for both sort and colour.</summary>
public enum PunchSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}

/// <summary>Lifecycle of a <see cref="PunchItem"/>.</summary>
public enum PunchStatus
{
    /// <summary>Raised — not yet worked on.</summary>
    Open = 0,
    /// <summary>Assignee is actively working on the fix.</summary>
    InProgress = 1,
    /// <summary>Assignee marked the fix complete — awaiting site verification.</summary>
    Fixed = 2,
    /// <summary>Site has verified the fix — the punch is closed.</summary>
    Verified = 3,
    /// <summary>Abandoned — not part of the delivered scope any more.</summary>
    Cancelled = 4,
}
