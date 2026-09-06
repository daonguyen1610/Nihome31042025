namespace NihomeBackend.Models;

public sealed class ProjectBoqRevision : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int RevisionNumber { get; set; }
    public string Currency { get; set; } = "VND";
    public ProjectBoqRevisionStatus Status { get; set; } = ProjectBoqRevisionStatus.Draft;
    public int? SourceTenderEstimateRevisionId { get; set; }
    public TenderEstimateRevision? SourceTenderEstimateRevision { get; set; }
    public int? SourceContractAppendixId { get; set; }
    public ContractAppendix? SourceContractAppendix { get; set; }
    public decimal CostTotal { get; set; }
    public int PreparedByUserId { get; set; }
    public ApplicationUser PreparedBy { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? SupersededAt { get; set; }
    public DateTime? FinalSelectedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<ProjectBoqLine> Lines { get; set; } = [];
}

public sealed class ProjectBoqLine
{
    public int Id { get; set; }
    public int ProjectBoqRevisionId { get; set; }
    public ProjectBoqRevision ProjectBoqRevision { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ApprovedQuantity { get; set; }
    public decimal BudgetUnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class MaterialRequest : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public MaterialRequestStatus Status { get; set; } = MaterialRequestStatus.Draft;
    public int SiteRequesterUserId { get; set; }
    public ApplicationUser SiteRequester { get; set; } = null!;
    public int ResponsibleSiteUserId { get; set; }
    public ApplicationUser ResponsibleSiteUser { get; set; } = null!;
    public int AssignedProcurementUserId { get; set; }
    public ApplicationUser AssignedProcurementUser { get; set; } = null!;
    public DateTime RequiredAt { get; set; }
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<MaterialRequestLine> Lines { get; set; } = [];
}

public sealed class MaterialRequestLine
{
    public int Id { get; set; }
    public int MaterialRequestId { get; set; }
    public MaterialRequest MaterialRequest { get; set; } = null!;
    public int ProjectBoqLineId { get; set; }
    public ProjectBoqLine ProjectBoqLine { get; set; } = null!;
    public decimal RequestedQuantity { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ContractLine : IConcurrencyTracked
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int ProjectBoqLineId { get; set; }
    public ProjectBoqLine ProjectBoqLine { get; set; } = null!;
    public int ProcurementOwnerUserId { get; set; }
    public ApplicationUser ProcurementOwner { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal NegotiatedUnitPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class WarehouseReceipt : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public WarehouseLedgerStatus Status { get; set; } = WarehouseLedgerStatus.Draft;
    public int? ReversalOfReceiptId { get; set; }
    public WarehouseReceipt? ReversalOfReceipt { get; set; }
    public int ReceivedByUserId { get; set; }
    public ApplicationUser ReceivedBy { get; set; } = null!;
    public DateTime InspectedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public ApplicationUser? PostedBy { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<WarehouseReceiptLine> Lines { get; set; } = [];
}

public sealed class WarehouseReceiptLine
{
    public int Id { get; set; }
    public int WarehouseReceiptId { get; set; }
    public WarehouseReceipt WarehouseReceipt { get; set; } = null!;
    public int MaterialRequestLineId { get; set; }
    public MaterialRequestLine MaterialRequestLine { get; set; } = null!;
    public int? ContractLineId { get; set; }
    public ContractLine? ContractLine { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public sealed class WarehouseIssue : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public WarehouseLedgerStatus Status { get; set; } = WarehouseLedgerStatus.Draft;
    public int? ReversalOfIssueId { get; set; }
    public WarehouseIssue? ReversalOfIssue { get; set; }
    public int ResponsibleSiteUserId { get; set; }
    public ApplicationUser ResponsibleSiteUser { get; set; } = null!;
    public int IssuedByUserId { get; set; }
    public ApplicationUser IssuedBy { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public int? PostedByUserId { get; set; }
    public ApplicationUser? PostedBy { get; set; }
    public string? WorkItemCode { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<WarehouseIssueLine> Lines { get; set; } = [];
}

public sealed class WarehouseIssueLine
{
    public int Id { get; set; }
    public int WarehouseIssueId { get; set; }
    public WarehouseIssue WarehouseIssue { get; set; } = null!;
    public int ProjectBoqLineId { get; set; }
    public ProjectBoqLine ProjectBoqLine { get; set; } = null!;
    public decimal IssuedQuantity { get; set; }
}

public sealed class VendorRating : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int VersionNumber { get; set; }
    public VendorRatingStatus Status { get; set; } = VendorRatingStatus.Draft;
    public int ProcurementOwnerUserId { get; set; }
    public ApplicationUser ProcurementOwner { get; set; } = null!;
    public decimal QualityScore { get; set; }
    public decimal ScheduleScore { get; set; }
    public decimal CostScore { get; set; }
    public decimal HseScore { get; set; }
    public decimal OverallScore { get; set; }
    public string? Comments { get; set; }
    public int PreparedByUserId { get; set; }
    public ApplicationUser PreparedBy { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public string? DecisionReason { get; set; }
    public int? SupersedesVendorRatingId { get; set; }
    public VendorRating? SupersedesVendorRating { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public enum ProjectBoqRevisionStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}

public enum MaterialRequestStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    PartiallyFulfilled = 4,
    Fulfilled = 5,
    Cancelled = 6,
}

public enum WarehouseLedgerStatus
{
    Draft = 0,
    Posted = 1,
    Reversed = 2,
}

public enum VendorRatingStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Superseded = 4,
}