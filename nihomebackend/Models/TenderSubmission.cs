namespace NihomeBackend.Models;

/// <summary>
/// Immutable evidence bundle captured when a tender is submitted. Later dossier
/// changes must not rewrite what NICON actually filed with the customer.
/// </summary>
public class TenderSubmission
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    public int Sequence { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int SubmittedByUserId { get; set; }
    public ApplicationUser SubmittedBy { get; set; } = null!;
    public int? ApprovedEstimateRevisionId { get; set; }
    public TenderEstimateRevision? ApprovedEstimateRevision { get; set; }
    public int? FinanceReviewWorkPackageId { get; set; }
    public DepartmentWorkPackage? FinanceReviewWorkPackage { get; set; }
    public string SubmissionChannel { get; set; } = string.Empty;
    public string? ReceiptReference { get; set; }
    public bool LegacyUnknown { get; set; }
    public string PlanningSnapshotJson { get; set; } = "[]";
    public string EstimateSnapshotJson { get; set; } = "{}";
    public string FinanceReviewSnapshotJson { get; set; } = "{}";
    public List<TenderSubmissionItem> Items { get; set; } = new();
}

/// <summary>Denormalized checklist evidence retained for one submission.</summary>
public class TenderSubmissionItem
{
    public int Id { get; set; }
    public int TenderSubmissionId { get; set; }
    public TenderSubmission TenderSubmission { get; set; } = null!;
    public int ChecklistItemId { get; set; }
    public int? EvidenceId { get; set; }
    public TenderChecklistEvidence? Evidence { get; set; }
    public string? TemplateCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public TenderChecklistItemStatus Status { get; set; }
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public int? CapabilityDocumentId { get; set; }
    public int? CapabilityDocumentVersion { get; set; }
    public string? Sha256 { get; set; }
    public string? NotApplicableReason { get; set; }
}
