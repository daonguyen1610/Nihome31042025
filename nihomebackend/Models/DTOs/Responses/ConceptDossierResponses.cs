namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ConceptRevisionResponse
{
    public int Id { get; set; }
    public int ConceptOptionId { get; set; }
    public int Number { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FrozenAt { get; set; }
    public List<ConceptArtifactResponse> Artifacts { get; set; } = new();
    public List<ConceptChecklistWaiverResponse> Waivers { get; set; } = new();
    public List<ConceptFeedbackResponse> Feedback { get; set; } = new();
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
