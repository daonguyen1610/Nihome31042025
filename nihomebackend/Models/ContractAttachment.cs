namespace NihomeBackend.Models;

/// <summary>
/// File attached to a <see cref="Contract"/> — signed scan of the master
/// contract, VO attachments (mirrored via <see cref="ContractAppendix"/>
/// on the FE), or supporting documents.
///
/// NIH-104 gates <c>Signed → InProgress</c> on the presence of at least one
/// <see cref="ContractAttachmentKind.SignedScan"/> row.
/// </summary>
public class ContractAttachment
{
    public int Id { get; set; }

    /// <summary>Owning contract. Cascade-delete when the parent is removed.</summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public ContractAttachmentKind Kind { get; set; } = ContractAttachmentKind.Supporting;

    /// <summary>Host-relative storage path, e.g. <c>/files/contracts/{guid}.pdf</c>.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Filename as uploaded by the user; used when serving the download.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Optional caption / label shown in the list.</summary>
    public string? Label { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedBy { get; set; }
}

public enum ContractAttachmentKind
{
    /// <summary>Signed scan of the master contract. Required to enter InProgress.</summary>
    SignedScan = 0,
    /// <summary>Additional supporting document (photos, drawings, etc.).</summary>
    Supporting = 1,
}
