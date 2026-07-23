namespace NihomeBackend.Models;

/// <summary>
/// M4 Construction & Acceptance — partial acceptance record
/// (Nghiệm thu từng phần / Nghiệm thu bộ phận-giai đoạn — NIH-143).
///
/// One row = one signed-off partial acceptance for a construction
/// phase or work package on a <see cref="DesignProject"/>. Covers:
/// <list type="bullet">
///   <item>Scheduled acceptance date + optional link to the
///     <see cref="ConstructionTask"/> being accepted.</item>
///   <item>Lifecycle Draft → Submitted → Approved / Rejected with a
///     Revised branch back to Draft so QA rework can loop.</item>
///   <item>Approval signature (approver + timestamp + note).</item>
/// </list>
///
/// Attached documents (photos, signed minutes, checklists) are stored
/// as <see cref="Documents"/> — a JSON array of relative URLs so the
/// existing storage helpers can serve them.
/// </summary>
public class AcceptanceRecord
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Human-friendly code, unique inside a project (e.g. <c>A-001</c>).</summary>
    public string AcceptanceCode { get; set; } = string.Empty;

    /// <summary>Display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Free-text scope / body of the acceptance minute.</summary>
    public string? Description { get; set; }

    /// <summary>Optional link to the phase / task being accepted.</summary>
    public int? ConstructionTaskId { get; set; }
    public ConstructionTask? ConstructionTask { get; set; }

    /// <summary>
    /// Planned acceptance date. Records past this date with status
    /// still in Draft/Submitted are surfaced as overdue.
    /// </summary>
    public DateOnly AcceptanceDate { get; set; }

    /// <summary>Location on site (block / floor / room).</summary>
    public string? Location { get; set; }

    /// <summary>Free-text list of participants (client + contractor rep, etc.).</summary>
    public string? Participants { get; set; }

    /// <summary>Findings / issues raised during the walk-through.</summary>
    public string? Findings { get; set; }

    /// <summary>Note recorded on approve/reject.</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>JSON array of relative document paths (photos, signed PDF).</summary>
    public string? Documents { get; set; }

    public AcceptanceStatus Status { get; set; } = AcceptanceStatus.Draft;

    // --- Workflow audit ---
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }

    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }

    /// <summary>Number of times this record has been revised (rejected → draft).</summary>
    public int RevisionCount { get; set; }

    // --- Standard audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Lifecycle of an <see cref="AcceptanceRecord"/>.</summary>
public enum AcceptanceStatus
{
    /// <summary>Being drafted — editable, no client signature yet.</summary>
    Draft = 0,
    /// <summary>Submitted for approval — awaiting sign-off.</summary>
    Submitted = 1,
    /// <summary>Approved — closes the record; not editable.</summary>
    Approved = 2,
    /// <summary>Rejected — can be revised back to Draft.</summary>
    Rejected = 3,
    /// <summary>Cancelled — not part of the acceptance ledger any more.</summary>
    Cancelled = 4,
}
