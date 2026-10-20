namespace NihomeBackend.Models.DTOs.Responses;

public sealed class DesignReviewResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DesignManagerUserId { get; set; }
    public string? DesignManagerName { get; set; }
    public string? InternalSummary { get; set; }
    public DateTime? InternalSubmittedAt { get; set; }
    public DateTime? ClientSubmittedAt { get; set; }
    public string? ClientDecisionNote { get; set; }
    public DateTime? ClientDecidedAt { get; set; }
    public bool CanManage { get; set; }
    public bool CanComment { get; set; }
    public List<DesignReviewRecipientResponse> Recipients { get; set; } = new();
    public List<DesignReviewCommentResponse> Comments { get; set; } = new();
    public List<long> ProjectDocumentIds { get; set; } = new();
    public List<DesignReviewDocumentResponse> Documents { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DesignReviewDocumentResponse
{
    public long ProjectDocumentId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public long SourceRecordId { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class DesignReviewRecipientResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class DesignReviewCommentResponse
{
    public long Id { get; set; }
    public long? ProjectDocumentId { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
