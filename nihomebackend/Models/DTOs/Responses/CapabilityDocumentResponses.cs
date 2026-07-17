namespace NihomeBackend.Models.DTOs.Responses;

public class CapabilityDocumentVersionResponse
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CapabilityDocumentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TagCode { get; set; } = string.Empty;

    /// <summary>Localised label for the tag; sourced from master data.</summary>
    public string? TagLabel { get; set; }

    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Description { get; set; }

    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;

    public int CurrentVersion { get; set; }

    /// <summary>
    /// Expiry state relative to the caller's current time:
    /// <c>none</c> · <c>expired</c> · <c>critical</c> (≤30 days) ·
    /// <c>warning</c> (≤60 days) · <c>ok</c>.
    /// </summary>
    public string ExpiryState { get; set; } = "none";

    public int? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Total number of previous versions (does not include current).</summary>
    public int PreviousVersionCount { get; set; }
}

public class CapabilityDocumentListResponse
{
    public List<CapabilityDocumentResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CapabilityDocumentDetailResponse : CapabilityDocumentResponse
{
    /// <summary>Previous file versions, newest-first. Does not include the current file.</summary>
    public List<CapabilityDocumentVersionResponse> Versions { get; set; } = new();
}
