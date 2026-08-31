using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ProjectDocumentUploadRequest
{
    [Required]
    public IFormFile? File { get; set; }

    [Required]
    public ProjectDocumentCategory Category { get; set; }

    public ProjectDocumentSourceModule SourceModule { get; set; } = ProjectDocumentSourceModule.General;
    public long? SourceRecordId { get; set; }
    public int? CustomerId { get; set; }
    public int? ContractId { get; set; }
}

public sealed class ClassifyProjectDocumentRequest
{
    [Required]
    public ProjectDocumentCategory Category { get; set; }
}

public sealed class ResolveProjectDocumentConflictRequest
{
    [Required]
    public bool ConfirmKeepBoth { get; set; }
}
