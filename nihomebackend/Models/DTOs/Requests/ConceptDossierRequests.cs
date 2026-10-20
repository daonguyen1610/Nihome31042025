using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class AddConceptArtifactRequest
{
    [Range(1, long.MaxValue)] public long ProjectDocumentId { get; set; }
    [Required, MaxLength(30)] public string Kind { get; set; } = string.Empty;
    [MaxLength(80)] public string? FloorLabel { get; set; }
}

public sealed class WaiveConceptChecklistRequest
{
    [Required, MaxLength(30)] public string Kind { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class AddConceptFeedbackRequest
{
    [Required, MaxLength(200)] public string CustomerName { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string Channel { get; set; } = string.Empty;
    [Required] public DateTime OccurredAt { get; set; }
    [Required, MaxLength(4000)] public string Comment { get; set; } = string.Empty;
    public long? ProjectDocumentId { get; set; }
}

public sealed class RespondConceptFeedbackRequest
{
    [Required, MaxLength(4000)] public string Response { get; set; } = string.Empty;
}
