namespace NihomeBackend.Models;

public sealed class ConstructionPreparation : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int Version { get; set; } = 1;
    public int? SupersedesPreparationId { get; set; }
    public ConstructionPreparationStatus Status { get; set; } = ConstructionPreparationStatus.Draft;
    public string WbsBaselineReference { get; set; } = string.Empty;
    public string ScheduleBaselineReference { get; set; } = string.Empty;
    public string SCurveBaselineReference { get; set; } = string.Empty;
    public string BchOrganizationReference { get; set; } = string.Empty;
    public string ResourcePlanReference { get; set; } = string.Empty;
    public string BudgetBaselineReference { get; set; } = string.Empty;
    public long? WbsBaselineDocumentId { get; set; }
    public long? ScheduleBaselineDocumentId { get; set; }
    public long? SCurveBaselineDocumentId { get; set; }
    public long? BchOrganizationDocumentId { get; set; }
    public long? ResourcePlanDocumentId { get; set; }
    public long? BudgetBaselineDocumentId { get; set; }
    public bool BudgetBaselineLocked { get; set; }
    public bool SiteHandoverRequired { get; set; }
    public bool SiteHandoverComplete { get; set; }
    public long? SiteHandoverDocumentId { get; set; }
    public bool MethodStatementRequired { get; set; }
    public bool MethodStatementApproved { get; set; }
    public long? MethodStatementDocumentId { get; set; }
    public bool HsePlanRequired { get; set; }
    public bool HsePlanApproved { get; set; }
    public long? HsePlanDocumentId { get; set; }
    public string? ReadinessSnapshotJson { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? PublishedByUserId { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? StartedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public enum ConstructionPreparationStatus { Draft, Published, ReadyToStart, InConstruction, Superseded }

public sealed class ConstructionResourceRequest : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public ConstructionResourceType Type { get; set; }
    public ConstructionReviewStatus Status { get; set; } = ConstructionReviewStatus.Draft;
    public string WbsCode { get; set; } = string.Empty;
    public DateTime NeedByDate { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? ProjectBoqLineId { get; set; }
    public int? ResponsibleSiteUserId { get; set; }
    public int? AssignedProcurementUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? CmReviewedAt { get; set; }
    public int? CmReviewedByUserId { get; set; }
    public string? CmDecisionReason { get; set; }
    public string? HandoffEntityType { get; set; }
    public long? HandoffEntityId { get; set; }
    public ConstructionHandoffStatus HandoffStatus { get; set; } = ConstructionHandoffStatus.NotReady;
    public int? ProcurementContractId { get; set; }
    public Contract? ProcurementContract { get; set; }
    public DateTime? SuppliedAt { get; set; }
    public int? SuppliedByUserId { get; set; }
    public long? SupplyDocumentId { get; set; }
    public ProjectDocument? SupplyDocument { get; set; }
    public List<ConstructionResourceHandoffEvent> HandoffEvents { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public enum ConstructionResourceType { Material, Equipment, Labour }

public sealed class ConstructionResourceHandoffEvent
{
    public long Id { get; set; }
    public int ConstructionResourceRequestId { get; set; }
    public ConstructionResourceRequest ConstructionResourceRequest { get; set; } = null!;
    public int? PreviousContractId { get; set; }
    public int ContractId { get; set; }
    public long? SupplyDocumentId { get; set; }
    public int ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public enum ConstructionReviewStatus { Draft, PendingCmReview, CmApproved, CmRejected }
public enum ConstructionHandoffStatus { NotReady, ReadyForProcurementAndFinance, MaterialRequestCreated }

public sealed class QaQcTemplate : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string NameVi { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameJa { get; set; } = string.Empty;
    public string SchemaJson { get; set; } = "{}";
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class QaQcDossier : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int QaQcTemplateId { get; set; }
    public QaQcTemplate QaQcTemplate { get; set; } = null!;
    public int TemplateVersion { get; set; }
    public string TemplateSnapshotJson { get; set; } = "{}";
    public int? IfcReleaseId { get; set; }
    public string ChecklistResultsJson { get; set; } = "[]";
    public string WbsCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ConstructionReviewStatus Status { get; set; } = ConstructionReviewStatus.Draft;
    public int CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? CmReviewedByUserId { get; set; }
    public DateTime? CmReviewedAt { get; set; }
    public string? CmDecisionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<QaQcDossierEvidence> Evidence { get; set; } = [];
}

public sealed class QaQcDossierEvidence
{
    public int QaQcDossierId { get; set; }
    public QaQcDossier QaQcDossier { get; set; } = null!;
    public long ProjectDocumentId { get; set; }
    public ProjectDocument ProjectDocument { get; set; } = null!;
    public QaQcEvidenceType EvidenceType { get; set; }
}

public enum QaQcEvidenceType { General, AcceptanceRecord, Sample, LaboratoryResult, MaterialCertificate, SiteDiary }

public sealed class ConstructionQsBatch : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public ConstructionQsDirection Direction { get; set; }
    public ConstructionReviewStatus Status { get; set; } = ConstructionReviewStatus.Draft;
    public int ContractId { get; set; }
    public string WbsCode { get; set; } = string.Empty;
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public int? ProjectBoqRevisionId { get; set; }
    public int? ContractAppendixId { get; set; }
    public int? ContractPaymentMilestoneId { get; set; }
    public decimal CertifiedAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? CmReviewedByUserId { get; set; }
    public DateTime? CmReviewedAt { get; set; }
    public string? CmDecisionReason { get; set; }
    public int? SupersedesBatchId { get; set; }
    public int? ReversesBatchId { get; set; }
    public ConstructionQsAdjustmentKind AdjustmentKind { get; set; } = ConstructionQsAdjustmentKind.Original;
    public ConstructionQsRecordState RecordState { get; set; } = ConstructionQsRecordState.Active;
    public bool IsActiveLineage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<ConstructionQsBatchLine> Lines { get; set; } = [];
    public List<ConstructionQsBatchEvidence> Evidence { get; set; } = [];
}

public enum ConstructionQsDirection { Upstream, Downstream }
public enum ConstructionQsAdjustmentKind { Original, SupersedingCorrection, Reversal }
public enum ConstructionQsRecordState { Active, Superseded, Reversed }

public sealed class ConstructionQsBatchLine
{
    public int Id { get; set; }
    public int ConstructionQsBatchId { get; set; }
    public ConstructionQsBatch ConstructionQsBatch { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ConstructionQsBatchEvidence
{
    public int ConstructionQsBatchId { get; set; }
    public ConstructionQsBatch ConstructionQsBatch { get; set; } = null!;
    public long ProjectDocumentId { get; set; }
    public ProjectDocument ProjectDocument { get; set; } = null!;
}
