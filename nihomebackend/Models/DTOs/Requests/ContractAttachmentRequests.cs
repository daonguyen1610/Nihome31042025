using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Attachment metadata submitted after
/// <c>POST /api/contracts/{id}/attachments/upload</c> returns the stored
/// path.</summary>
public class CreateContractAttachmentRequest
{
    public ContractAttachmentKind Kind { get; set; } = ContractAttachmentKind.Supporting;

    [Required, StringLength(500, MinimumLength = 1)]
    public string FilePath { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 1)]
    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [StringLength(150)]
    public string? ContentType { get; set; }

    [StringLength(300)]
    public string? Label { get; set; }
}

public enum ContractUploadPurpose
{
    Attachment,
    Appendix,
}

/// <summary>Opens a resumable upload of one contract file.</summary>
public class StartContractUploadRequest
{
    [EnumDataType(typeof(ContractUploadPurpose))]
    public ContractUploadPurpose Purpose { get; set; }
    public string? FileName { get; set; }
    public long FileSize { get; set; }
}

/// <summary>Takes the finished file of an upload session.</summary>
public class CompleteContractUploadRequest
{
    public Guid UploadSessionId { get; set; }
}

/// <summary>Takes the finished file of an upload session as a contract attachment.</summary>
public class CompleteContractAttachmentUploadRequest : CompleteContractUploadRequest
{
    public ContractAttachmentKind Kind { get; set; }

    [StringLength(300)]
    public string? Label { get; set; }
}
