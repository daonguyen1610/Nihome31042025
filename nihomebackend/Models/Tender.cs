namespace NihomeBackend.Models;

/// <summary>
/// CRM Tender (Gói thầu) — a bidding pursuit against a customer that owns
/// its own multi-item preparation checklist (hồ sơ dự thầu). Sits alongside
/// <see cref="Opportunity"/> and <see cref="Quote"/> in the M1 CRM funnel
/// (Lead → Customer → Opportunity/Tender → Contract). Deadline management
/// and per-item file uploads are the core value proposition — see the
/// story spec for the workflow.
/// </summary>
public class Tender
{
    public int Id { get; set; }

    /// <summary>Human-friendly code (e.g. TD-2026-0001), unique, auto-generated on create.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name of the tender.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Owning customer (chủ đầu tư) — hard FK, tender cannot exist without one.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Bid opening date (ngày mở thầu). Nullable — not always known at create.</summary>
    public DateTime? OpeningDate { get; set; }

    /// <summary>Submission deadline (deadline nộp) — hard requirement, must be &gt; now on create.</summary>
    public DateTime SubmissionDeadline { get; set; }

    /// <summary>Sales owner responsible for preparing the bid. Nullable while unassigned.</summary>
    public int? PreparerUserId { get; set; }
    public ApplicationUser? Preparer { get; set; }

    /// <summary>Free-text source of the tender lead (nguồn thông tin), e.g. "Website", "Referral".</summary>
    public string? InfoSource { get; set; }

    public TenderStatus Status { get; set; } = TenderStatus.Preparing;

    public string? Note { get; set; }

    // --- Result fields (set when transitioning to Won/Lost via NIH-97 workflow) ---

    /// <summary>Opportunity linked on Mark Won (optional forward-compatibility). Set later by NIH-97.</summary>
    public int? WonOpportunityId { get; set; }

    /// <summary>Master-data code from <c>opportunity_lost_reason</c>, populated on Mark Lost.</summary>
    public string? LostReasonCode { get; set; }

    /// <summary>Free-text note captured on Lost transition.</summary>
    public string? LostNote { get; set; }

    public DateTime? ClosedAt { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<TenderChecklistItem> ChecklistItems { get; set; } = new();
}

/// <summary>
/// Preparation checklist entry attached to a <see cref="Tender"/>. Seeded
/// from the <c>tender_checklist_default</c> master-data category on create
/// so every new tender starts with the standard 6 items (Hồ sơ năng lực,
/// Hồ sơ pháp nhân, Bảo lãnh dự thầu, BOQ, Thuyết minh biện pháp thi công,
/// Tiến độ). Extra items can be added later via the detail-page inline UI
/// (NIH-97).
/// </summary>
public class TenderChecklistItem
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;

    /// <summary>Master-data code the item was seeded from, when applicable. Nullable for ad-hoc items.</summary>
    public string? TemplateCode { get; set; }

    /// <summary>Display title of the checklist entry, localised at write-time.</summary>
    public string Title { get; set; } = string.Empty;

    public TenderChecklistItemStatus Status { get; set; } = TenderChecklistItemStatus.NotStarted;

    /// <summary>Owner of this checklist entry. Nullable — falls back to tender preparer for reminders.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Internal deadline for this item (deadline nội bộ), optional.</summary>
    public DateTime? InternalDeadline { get; set; }

    /// <summary>Host-relative URL of the uploaded file, when the item is Done/Submitted.</summary>
    public string? FilePath { get; set; }

    /// <summary>Original client-provided filename (preserved for downloads).</summary>
    public string? OriginalFileName { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum TenderStatus
{
    Preparing = 0,
    Submitted = 1,
    Won = 2,
    Lost = 3,
    Cancelled = 4,
}

public enum TenderChecklistItemStatus
{
    NotStarted = 0,
    Preparing = 1,
    Done = 2,
    Submitted = 3,
}
