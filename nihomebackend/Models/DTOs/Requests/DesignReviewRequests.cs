using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class CreateDesignReviewRequest
{
    [Required, MaxLength(30)] public string Stage { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    [MinLength(1)] public List<int> RecipientUserIds { get; set; } = new();
    [MinLength(1)] public List<long> ProjectDocumentIds { get; set; } = new();
    public int? ConceptRevisionId { get; set; }
}

public sealed class SubmitDesignReviewRequest
{
    [Required] public string? RowVersion { get; set; }
}

public sealed class AddDesignReviewCommentRequest
{
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    public long? ProjectDocumentId { get; set; }
}

public sealed class ConsolidateDesignReviewRequest
{
    [Required, MaxLength(4000)] public string InternalSummary { get; set; } = string.Empty;
    [Required] public string? RowVersion { get; set; }
}

public sealed class RecordDesignClientDecisionRequest
{
    [Required, MaxLength(30)] public string Decision { get; set; } = string.Empty;
    [MaxLength(4000)] public string? Note { get; set; }
    public long? ClientAcceptanceDocumentId { get; set; }
    [MaxLength(200)] public string? ClientAcceptanceSignedBy { get; set; }
    public DateTime? ClientAcceptanceSignedAt { get; set; }
    [Required] public string? RowVersion { get; set; }
}
