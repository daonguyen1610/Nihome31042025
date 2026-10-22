using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Models;

public class DepartmentWorkPackage : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public WorkPackageKind Kind { get; set; }
    public WorkPackagePurpose Purpose { get; set; } = WorkPackagePurpose.General;
    public int? LegalChecklistTemplateId { get; set; }
    public string? LegalChecklistSnapshotJson { get; set; }
    public int? LegalChecklistAppliedByUserId { get; set; }
    public DateTime? LegalChecklistAppliedAt { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? OperationalProjectId { get; set; }
    public OperationalProject? OperationalProject { get; set; }
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int? SourceOpportunityId { get; set; }
    public Opportunity? SourceOpportunity { get; set; }
    public int? SourceQuoteId { get; set; }
    public Quote? SourceQuote { get; set; }
    public int? SourceTenderId { get; set; }
    public Tender? SourceTender { get; set; }
    public int DepartmentRoleGroupId { get; set; }
    public RoleGroup DepartmentRoleGroup { get; set; } = null!;
    public int LeadUserId { get; set; }
    public ApplicationUser LeadUser { get; set; } = null!;
    public WorkPackageStatus Status { get; set; } = WorkPackageStatus.Planned;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DepartmentWorkTask> Tasks { get; set; } = new();
    public List<DepartmentWorkPackageParticipantGroup> ParticipantGroups { get; set; } = new();
    public List<DepartmentWorkPackageHistory> History { get; set; } = new();
    public List<ProcurementInputPriceOffer> InputPriceOffers { get; set; } = new();
    public DepartmentFinanceReview? FinanceReview { get; set; }
}

public class ProcurementInputPriceOffer : IConcurrencyTracked
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public string SupplierReference { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public DateTime ValidUntil { get; set; }
    public InputPriceOfferStatus Status { get; set; } = InputPriceOfferStatus.Draft;
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedByUser { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedByUser { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<ProcurementInputPriceLine> Lines { get; set; } = new();
}

public class ProcurementInputPriceLine
{
    public int Id { get; set; }
    public int OfferId { get; set; }
    public ProcurementInputPriceOffer Offer { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPercent { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public class DepartmentFinanceReview : IConcurrencyTracked
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public FinanceReviewStatus Status { get; set; } = FinanceReviewStatus.Draft;
    public bool ContractConditionsReviewed { get; set; }
    public bool GuaranteesReviewed { get; set; }
    public bool PaymentTermsReviewed { get; set; }
    public bool FinancialDossierReviewed { get; set; }
    public string? Findings { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }

    /// <summary>
    /// Version of the source quotation the commercial review covers. A quotation
    /// that is edited after approval is re-versioned, so a review approved for an
    /// older version no longer authorises sending the current one.
    /// </summary>
    public int? ReviewedQuoteVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Departments participating in an internal initiative. Project packages keep
/// exactly the receiving department as their only participant.
/// </summary>
public class DepartmentWorkPackageParticipantGroup
{
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public int RoleGroupId { get; set; }
    public RoleGroup RoleGroup { get; set; } = null!;
}

public class DepartmentWorkTask : IConcurrencyTracked
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public string WbsCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Resources { get; set; }
    public int DepartmentRoleGroupId { get; set; }
    public RoleGroup DepartmentRoleGroup { get; set; } = null!;
    public int AssigneeUserId { get; set; }
    public ApplicationUser AssigneeUser { get; set; } = null!;
    public int? ApproverUserId { get; set; }
    public ApplicationUser? ApproverUser { get; set; }
    public int? KpiDefinitionId { get; set; }
    public KpiDefinition? KpiDefinition { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.Planned;
    public int ProgressPercent { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public ApplicationUser? VerifiedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DepartmentWorkTaskCc> CcUsers { get; set; } = new();
    public List<DepartmentWorkTaskDependency> Predecessors { get; set; } = new();
    public List<DepartmentWorkTaskDependency> Successors { get; set; } = new();
}

public class DepartmentWorkTaskCc
{
    public int WorkTaskId { get; set; }
    public DepartmentWorkTask WorkTask { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
}

public class DepartmentWorkTaskDependency
{
    public int WorkTaskId { get; set; }
    public DepartmentWorkTask WorkTask { get; set; } = null!;
    public int PredecessorTaskId { get; set; }
    public DepartmentWorkTask PredecessorTask { get; set; } = null!;
}

public class DepartmentWorkPackageHistory
{
    public long Id { get; set; }
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedByUser { get; set; } = null!;
}

public enum WorkPackageKind
{
    Project = 0,
    Internal = 1,
}

public enum WorkPackagePurpose
{
    General = 0,
    ProcurementInputPricing = 1,
    FinanceCommercialReview = 2,
    FinanceTenderReview = 3,
    TenderPlanning = 4,
    LegalPermitting = 5,
}

public enum InputPriceOfferStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}

public enum FinanceReviewStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}

public enum WorkPackageStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum WorkTaskStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Verified = 3,
    Cancelled = 4,
}
