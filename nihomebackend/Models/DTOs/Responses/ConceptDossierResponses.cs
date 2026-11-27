namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ConceptRevisionResponse
{
    public int Id { get; set; }
    public int ConceptOptionId { get; set; }
    public int Number { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FrozenAt { get; set; }
    public bool CanCreateRevision { get; set; }
    public string? ReasonCode { get; set; }
    /// <summary>Items that need a file or a recorded exception before submission.</summary>
    public List<string> RequiredKinds { get; set; } = new();
    /// <summary>What still blocks the internal submission. Only set for the latest unsubmitted version.</summary>
    public ConceptReadinessResponse? Readiness { get; set; }
    public List<ConceptArtifactResponse> Artifacts { get; set; } = new();
    public List<ConceptChecklistWaiverResponse> Waivers { get; set; } = new();
    public List<ConceptFeedbackResponse> Feedback { get; set; } = new();
}

public sealed class ConceptReadinessResponse
{
    public List<string> MissingKinds { get; set; } = new();
    public bool NeedsVisual { get; set; }
    public int FilesWaiting { get; set; }
    public int FilesBlocked { get; set; }
    public bool HasUnansweredEarlierFeedback { get; set; }
}

public sealed class ConceptArtifactResponse
{
    public int Id { get; set; }
    public long ProjectDocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? FloorLabel { get; set; }
    public string SyncStatus { get; set; } = string.Empty;
}

public sealed class ConceptChecklistWaiverResponse
{
    public string Kind { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ConceptFeedbackResponse
{
    public int Id { get; set; }
    public long? ProjectDocumentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? EvidenceSource { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? Response { get; set; }
    public string? Outcome { get; set; }
    public DateTime? RespondedAt { get; set; }
}
