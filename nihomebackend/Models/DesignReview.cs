namespace NihomeBackend.Models;

public enum DesignReviewStage
{
    Concept = 0,
    BasicDesign = 1,
    ShopDrawing = 2,
}

public enum DesignReviewStatus
{
    Draft = 0,
    InternalReview = 1,
    ReadyForClient = 2,
    SubmittedToClient = 3,
    ClientChangesRequested = 4,
    ClientApproved = 5,
    Rejected = 6,
}

public class DesignReview : IConcurrencyTracked
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;
    public DesignReviewStage Stage { get; set; }
    public int RevisionNumber { get; set; }
    public int? ConceptRevisionId { get; set; }
    public ConceptRevision? ConceptRevision { get; set; }
    public long? ClientAcceptanceDocumentId { get; set; }
    public ProjectDocument? ClientAcceptanceDocument { get; set; }
    public string? ClientAcceptanceSignedBy { get; set; }
    public DateTime? ClientAcceptanceSignedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public DesignReviewStatus Status { get; set; } = DesignReviewStatus.Draft;
    public int DesignManagerUserId { get; set; }
    public ApplicationUser DesignManager { get; set; } = null!;
    public string? InternalSummary { get; set; }
    public DateTime? InternalSubmittedAt { get; set; }
    public DateTime? ClientSubmittedAt { get; set; }
    public string? ClientDecisionNote { get; set; }
    public DateTime? ClientDecidedAt { get; set; }
    public int? ClientDecisionRecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DesignReviewRecipient> Recipients { get; set; } = new();
    public List<DesignReviewComment> Comments { get; set; } = new();
    public List<DesignReviewDocument> Documents { get; set; } = new();
}

public class DesignReviewRecipient
{
    public int DesignReviewId { get; set; }
    public DesignReview DesignReview { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}

public class DesignReviewComment
{
    public long Id { get; set; }
    public int DesignReviewId { get; set; }
    public DesignReview DesignReview { get; set; } = null!;
    public long? ProjectDocumentId { get; set; }
    public int AuthorUserId { get; set; }
    public ApplicationUser Author { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DesignReviewDocument
{
    public int DesignReviewId { get; set; }
    public DesignReview DesignReview { get; set; } = null!;
    public long ProjectDocumentId { get; set; }
    public ProjectDocument ProjectDocument { get; set; } = null!;
    public string SourceEntityTypeSnapshot { get; set; } = string.Empty;
    public long SourceRecordIdSnapshot { get; set; }
    public string Sha256Snapshot { get; set; } = string.Empty;
}
