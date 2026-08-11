namespace NihomeBackend.Models.DTOs.Responses;

public class VendorDocumentResponse
{
    public int Id { get; set; }
    public VendorDocumentType DocumentType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
}

public class VendorEvaluationResponse
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public byte ScoreQuality { get; set; }
    public byte ScoreSchedule { get; set; }
    public byte ScoreCost { get; set; }
    public byte ScoreSafety { get; set; }
    public decimal AverageScore { get; set; }
    public string? Comment { get; set; }
    public int EvaluatedByUserId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public int UpdatedByUserId { get; set; }
    public string UpdatedByName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class VendorResponse
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public VendorType VendorType { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? LicenseNo { get; set; }
    public string ServiceGroupCode { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UpdatedByUserId { get; set; }
    public decimal? AverageScore { get; set; }
    public List<VendorDocumentResponse> Documents { get; set; } = new();
    public List<VendorEvaluationResponse> Evaluations { get; set; } = new();
}

public class VendorListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<VendorResponse> Items { get; set; } = new();
}

public class VendorOwnerOptionResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class VendorProjectOptionResponse
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class VendorDocumentDownload
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class VendorAuditResponse
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? ActorUserId { get; set; }
    public string? ActorPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
