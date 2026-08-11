namespace NihomeBackend.Models;

public class Vendor
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string NormalizedCompanyName { get; set; } = string.Empty;
    public VendorType VendorType { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? LicenseNo { get; set; }
    public string ServiceGroupCode { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public List<VendorDocument> Documents { get; set; } = new();
    public List<VendorEvaluation> Evaluations { get; set; } = new();
}

public enum VendorType
{
    Supplier = 0,
    SubContractor = 1,
    Both = 2,
}

public enum VendorDocumentType
{
    Capability = 0,
    License = 1,
    Other = 2,
}

public class VendorDocument
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public VendorDocumentType DocumentType { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}

public class VendorEvaluation
{
    public int Id { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int ProjectId { get; set; }
    public DesignProject Project { get; set; } = null!;
    public byte ScoreQuality { get; set; }
    public byte ScoreSchedule { get; set; }
    public byte ScoreCost { get; set; }
    public byte ScoreSafety { get; set; }
    public string? Comment { get; set; }
    public int EvaluatedByUserId { get; set; }
    public ApplicationUser EvaluatedBy { get; set; } = null!;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public ApplicationUser UpdatedBy { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
