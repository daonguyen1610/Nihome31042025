using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Metadata-only request used both on create (after the file has been
/// uploaded via a separate multipart call) and on update. The <c>filePath</c>
/// pair is optional on update; when omitted the current file/version are
/// preserved.
/// </summary>
public class UpsertCapabilityDocumentRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Master-data code from category <c>capability_document_tag</c>.</summary>
    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string TagCode { get; set; } = string.Empty;

    public DateTime? IssuedDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Host-relative URL to the uploaded file (required on create).</summary>
    [StringLength(500)]
    public string? FilePath { get; set; }

    /// <summary>Original client-provided filename (preserved for ZIP downloads).</summary>
    [StringLength(300)]
    public string? OriginalFileName { get; set; }

    public long? FileSize { get; set; }

    [StringLength(150)]
    public string? ContentType { get; set; }
}

/// <summary>
/// Payload for replacing the underlying file on an existing document. The
/// current file becomes an immutable version snapshot and the new file
/// bumps <c>CurrentVersion</c>.
/// </summary>
public class ReplaceCapabilityDocumentFileRequest
{
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    [StringLength(150)]
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>Request body for the bulk ZIP-download endpoint.</summary>
public class CapabilityDocumentsZipRequest
{
    [Required]
    [MinLength(1)]
    public List<int> Ids { get; set; } = new();
}
