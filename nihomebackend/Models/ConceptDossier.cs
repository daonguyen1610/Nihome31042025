namespace NihomeBackend.Models;

/// <summary>A version of one Concept option; the first version is R00.</summary>
public sealed class ConceptRevision
{
    public int Id { get; set; }
    public int ConceptOptionId { get; set; }
    public ConceptOption ConceptOption { get; set; } = null!;
    public int Number { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime? FrozenAt { get; set; }
    public List<ConceptArtifact> Artifacts { get; set; } = new();
    public List<ConceptChecklistWaiver> Waivers { get; set; } = new();
    public List<ConceptFeedback> Feedback { get; set; } = new();
}

public sealed class ConceptChecklistWaiver
{
    public int Id { get; set; }
    public int ConceptRevisionId { get; set; }
    public ConceptRevision ConceptRevision { get; set; } = null!;
    public ConceptArtifactKind Kind { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}

public enum ConceptArtifactKind
{
    Model3D,
    Perspective,
    SiteRoofPlan,
    FloorPlan,
    Section,
    Presentation,
    Other,
}

/// <summary>Typed binding to the durable Project Document workflow.</summary>
public sealed class ConceptArtifact
{
    public int Id { get; set; }
    public int ConceptRevisionId { get; set; }
    public ConceptRevision ConceptRevision { get; set; } = null!;
    public long ProjectDocumentId { get; set; }
    public ProjectDocument ProjectDocument { get; set; } = null!;
    public ConceptArtifactKind Kind { get; set; }
    public string? FloorLabel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}

/// <summary>Customer statement is immutable; the team's response can be added once.</summary>
public sealed class ConceptFeedback
{
    public int Id { get; set; }
    public int ConceptRevisionId { get; set; }
    public ConceptRevision ConceptRevision { get; set; } = null!;
    public long? ProjectDocumentId { get; set; }
    public ProjectDocument? ProjectDocument { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string? Response { get; set; }
    public DateTime? RespondedAt { get; set; }
    public int? RespondedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}
