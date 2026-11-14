namespace NihomeBackend.Models;

// A filled investment-rate (suất đầu tư) form uploaded in the project's input-price work package.
// The package lead approves it there; the latest approved revision prices the project's unit-cost quotes.
public sealed class ProjectInvestmentRateRevision : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int? SourceWorkPackageId { get; set; }
    public DepartmentWorkPackage? SourceWorkPackage { get; set; }
    public int RevisionNumber { get; set; }
    public ProjectInvestmentRateStatus Status { get; set; } = ProjectInvestmentRateStatus.Draft;
    public string TemplateCode { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public string SourceWorkbookPath { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public decimal ConvertedAreaSqm { get; set; }
    public decimal RatePerSqm { get; set; }
    public decimal Total { get; set; }
    // Rows of sections A and B as read from the file, kept for display.
    public string AreaLinesJson { get; set; } = "[]";
    public string GroupLinesJson { get; set; } = "[]";
    public int PreparedByUserId { get; set; }
    public ApplicationUser PreparedBy { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public enum ProjectInvestmentRateStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}
