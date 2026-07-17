namespace NihomeBackend.Models;

/// <summary>
/// CRM Capability-Document (hồ sơ năng lực) — a shared repository of
/// legal / discipline documents (Kiến trúc / Kết cấu / MEP / ISO / Giấy phép
/// …) that Sales can pick from when preparing a Tender, so the same file
/// does not have to be re-uploaded for every bid. The repository is the
/// single source of truth per <see cref="TagCode"/> so the latest version
/// is used everywhere. Physical file lives under
/// <c>/files/capability/{guid}.{ext}</c>; <see cref="FilePath"/> stores the
/// host-relative URL. Previous file versions are preserved in
/// <see cref="CapabilityDocumentVersion"/> so history is not lost when a
/// document is replaced.
/// </summary>
public class CapabilityDocument
{
    public int Id { get; set; }

    /// <summary>Display name (e.g. "Giấy chứng nhận ISO 9001:2015").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Master-data code from category <c>capability_document_tag</c>.</summary>
    public string TagCode { get; set; } = string.Empty;

    /// <summary>Issued date (nullable — some documents have no issue date).</summary>
    public DateTime? IssuedDate { get; set; }

    /// <summary>Expiry date (nullable — permanent documents never expire).</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Free-text description (optional).</summary>
    public string? Description { get; set; }

    // --- Current version pointer ---

    /// <summary>Host-relative URL of the current file (e.g. <c>/files/capability/abc.pdf</c>).</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Original client-provided filename (kept for ZIP download to preserve VN diacritics).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>MIME type reported at upload.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Current version number; starts at 1, bumps on every Replace.</summary>
    public int CurrentVersion { get; set; } = 1;

    // --- Audit ---
    public int? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<CapabilityDocumentVersion> Versions { get; set; } = new();
}

/// <summary>
/// Immutable history of a previous file bound to a
/// <see cref="CapabilityDocument"/>. Written whenever the current file is
/// replaced so the old physical asset (and its metadata) are still available
/// for audit and rollback needs.
/// </summary>
public class CapabilityDocumentVersion
{
    public int Id { get; set; }
    public int CapabilityDocumentId { get; set; }
    public CapabilityDocument CapabilityDocument { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;

    public int? UploadedByUserId { get; set; }
    public ApplicationUser? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
