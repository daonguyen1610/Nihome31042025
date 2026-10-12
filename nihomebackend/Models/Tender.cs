namespace NihomeBackend.Models;

/// <summary>
/// CRM Tender (Gói thầu) — a bidding pursuit against a customer that owns
/// its own multi-item preparation checklist (hồ sơ dự thầu). Sits alongside
/// <see cref="Opportunity"/> and <see cref="Quote"/> in the M1 CRM funnel
/// (Lead → Customer → Opportunity/Tender → Contract). Deadline management
/// and per-item file uploads are the core value proposition — see the
/// story spec for the workflow.
/// </summary>
public class Tender : IConcurrencyTracked
{
    public int Id { get; set; }

    /// <summary>Human-friendly code (e.g. TD-2026-0001), unique, auto-generated on create.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name of the tender.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Owning customer (chủ đầu tư) — hard FK, tender cannot exist without one.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int? OperationalProjectId { get; set; }
    public OperationalProject? OperationalProject { get; set; }

    /// <summary>Bid opening date (ngày mở thầu). Nullable — not always known at create.</summary>
    public DateTime? OpeningDate { get; set; }

    /// <summary>When NICON received the invitation/tender dossier metadata.</summary>
    public DateTime? InvitationReceivedAt { get; set; }

    /// <summary>Customer/portal reference for the invitation, when supplied.</summary>
    public string? InvitationReference { get; set; }

    /// <summary>How the invitation was received (portal, email, paper, etc.).</summary>
    public string InvitationSource { get; set; } = string.Empty;

    /// <summary>User who formally received and registered the invitation.</summary>
    public int InvitationReceivedByUserId { get; set; }
    public ApplicationUser InvitationReceivedByUser { get; set; } = null!;

    /// <summary>Optional HTTP(S) tender-portal URL when the dossier is not stored locally.</summary>
    public string? InvitationPortalUrl { get; set; }

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

    /// <summary>Immutable business timestamp of the first submitted bid.</summary>
    public DateTime? SubmittedAt { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<TenderChecklistItem> ChecklistItems { get; set; } = new();
    public List<TenderEstimateRevision> EstimateRevisions { get; set; } = new();
    public List<TenderSubmission> Submissions { get; set; } = new();
    public List<TenderMilestone> Milestones { get; set; } = new();
    public List<TenderInvitationDocument> InvitationDocuments { get; set; } = new();
}

/// <summary>
/// Preparation checklist entry attached to a <see cref="Tender"/>. Seeded
/// from the <c>tender_checklist_default</c> master-data category on create
/// so every new tender starts with 7 standard items: shared capability,
/// legal, bid security, BOQ, method statement, tender drawings, and schedule.
/// Extra items can be added later via the detail-page inline UI
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

    /// <summary>Only required rows block submission. Optional rows may be marked not applicable.</summary>
    public bool IsRequired { get; set; } = true;

    public string? NotApplicableReason { get; set; }

    public TenderDocumentKind DocumentKind { get; set; } = TenderDocumentKind.Custom;

    /// <summary>Owner of this checklist entry. Nullable — falls back to tender preparer for reminders.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Optional task in the cross-department Tender plan that produces this dossier item.</summary>
    public int? WorkTaskId { get; set; }
    public DepartmentWorkTask? WorkTask { get; set; }

    /// <summary>Internal deadline for this item (deadline nội bộ), optional.</summary>
    public DateTime? InternalDeadline { get; set; }

    /// <summary>Host-relative URL of the uploaded file, when the item is Done/Submitted.</summary>
    public string? FilePath { get; set; }

    /// <summary>Original client-provided filename (preserved for downloads).</summary>
    public string? OriginalFileName { get; set; }

    /// <summary>Shared capability source when this row references a library document.</summary>
    public int? CapabilityDocumentId { get; set; }
    public CapabilityDocument? CapabilityDocument { get; set; }

    /// <summary>Current evidence revision. Older revisions remain immutable.</summary>
    public int? CurrentEvidenceId { get; set; }
    public TenderChecklistEvidence? CurrentEvidence { get; set; }
    public List<TenderChecklistEvidence> EvidenceRevisions { get; set; } = new();

    public int SortOrder { get; set; }

    /// <summary>Stable completion timestamp used for SLA/KPI calculations.</summary>
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public ApplicationUser? CompletedBy { get; set; }

    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public ApplicationUser? VerifiedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum TenderDocumentKind
{
    Capability = 0,
    Legal = 1,
    BidSecurity = 2,
    Boq = 3,
    MethodStatement = 4,
    Drawings = 5,
    Schedule = 6,
    Custom = 7,
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
    NotApplicable = 4,
}
