namespace NihomeBackend.Models;

/// <summary>An immutable file/library revision attached to one dossier row.</summary>
public class TenderChecklistEvidence
{
    public int Id { get; set; }
    public int ChecklistItemId { get; set; }
    public TenderChecklistItem ChecklistItem { get; set; } = null!;
    public int Revision { get; set; }
    public TenderEvidenceSource Source { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Sha256 { get; set; }
    public int? CapabilityDocumentId { get; set; }
    public CapabilityDocument? CapabilityDocument { get; set; }
    public int? CapabilityDocumentVersion { get; set; }
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TenderEvidenceSource
{
    DirectUpload = 0,
    CapabilityLibrary = 1,
    Legacy = 2,
}

/// <summary>First time a derived Tender readiness gate was achieved.</summary>
public class TenderMilestone
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    public TenderMilestoneKind Kind { get; set; }
    public DateTime AchievedAt { get; set; }
    public int AchievedByUserId { get; set; }
    public ApplicationUser AchievedByUser { get; set; } = null!;
}

public enum TenderMilestoneKind
{
    PlanningReady = 0,
    DossierReady = 1,
    InternalReviewReady = 2,
    ReadyToSubmit = 3,
}

/// <summary>Optional file or portal link received with the invitation metadata.</summary>
public class TenderInvitationDocument
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? Sha256 { get; set; }
    public string? ExternalUrl { get; set; }
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
