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

/// <summary>Starts a contract file upload sent in chunks.</summary>
public class StartContractUploadRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string FileName { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long Size { get; set; }

    [StringLength(150)]
    public string? ContentType { get; set; }
}

/// <summary>Attaches a completed chunked upload to the contract.</summary>
public class CompleteContractAttachmentUploadRequest
{
    public ContractAttachmentKind Kind { get; set; } = ContractAttachmentKind.Supporting;

    [StringLength(300)]
    public string? Label { get; set; }
}
