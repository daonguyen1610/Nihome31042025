using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public sealed record ConstructionPreparationResponse(
    int Id, int OperationalProjectId, int Version, ConstructionPreparationStatus Status,
    string WbsBaselineReference, string ScheduleBaselineReference, string SCurveBaselineReference,
    string BchOrganizationReference, string ResourcePlanReference, string BudgetBaselineReference,
    long? WbsBaselineDocumentId, long? ScheduleBaselineDocumentId, long? SCurveBaselineDocumentId,
    long? BchOrganizationDocumentId, long? ResourcePlanDocumentId, long? BudgetBaselineDocumentId,
    bool BudgetBaselineLocked,
    bool SiteHandoverRequired, bool SiteHandoverComplete, bool MethodStatementRequired,
    bool MethodStatementApproved, bool HsePlanRequired, bool HsePlanApproved,
    long? SiteHandoverDocumentId, long? MethodStatementDocumentId, long? HsePlanDocumentId,
    IReadOnlyList<string> MissingConditions, string? ReadinessSnapshotJson, string RowVersion);

public sealed record ConstructionResourceRequestResponse(
    int Id, string Code, ConstructionResourceType Type, ConstructionReviewStatus Status,
    string WbsCode, DateTime NeedByDate, decimal Quantity, string Unit, string Reason,
    int? ProjectBoqLineId, int? ResponsibleSiteUserId, int? AssignedProcurementUserId,
    int CreatedByUserId, string? CmDecisionReason,
    string? HandoffEntityType, long? HandoffEntityId, ConstructionHandoffStatus HandoffStatus,
    string RowVersion);

public sealed record ConstructionResourceReferenceDataResponse(
    IReadOnlyList<ConstructionResourceBoqLineReferenceResponse> BoqLines,
    IReadOnlyList<ConstructionResourceUserReferenceResponse> SiteUsers,
    IReadOnlyList<ConstructionResourceUserReferenceResponse> ProcurementUsers);

public sealed record ConstructionResourceBoqLineReferenceResponse(
    int Id, string ItemCode, string Description, string Unit,
    decimal ApprovedQuantity, decimal RemainingQuantity);

public sealed record ConstructionResourceUserReferenceResponse(int UserId, string UserName);

public sealed record QaQcTemplateResponse(int Id, string Code, int Version, string NameVi,
    string Name, string NameZh, string NameJa, string SchemaJson, bool IsPublished, string RowVersion);

public sealed record QaQcDossierResponse(int Id, string Code, int TemplateId, int TemplateVersion,
    string TemplateSnapshotJson, int? IfcReleaseId, string ChecklistResultsJson, string WbsCode, string Title, ConstructionReviewStatus Status,
    int CreatedByUserId, IReadOnlyList<long> EvidenceDocumentIds, string? CmDecisionReason, string RowVersion,
    IReadOnlyList<QaQcEvidenceResponse> Evidence);

public sealed record QaQcEvidenceResponse(long ProjectDocumentId, QaQcEvidenceType EvidenceType);

public sealed record ConstructionQsBatchResponse(int Id, string Code, ConstructionQsDirection Direction,
    ConstructionReviewStatus Status, int ContractId, string WbsCode, DateTime PeriodFrom, DateTime PeriodTo, int? ProjectBoqRevisionId,
    int? ContractAppendixId, int? ContractPaymentMilestoneId, decimal CertifiedAmount,
    string Description, int CreatedByUserId, string? CmDecisionReason, int? SupersedesBatchId,
    int? ReversesBatchId, ConstructionQsAdjustmentKind AdjustmentKind,
    ConstructionQsRecordState RecordState, IReadOnlyList<ConstructionQsBatchLineResponse> Lines,
    IReadOnlyList<long> EvidenceDocumentIds, string RowVersion);

public sealed record ConstructionQsBatchLineResponse(int Id, string ItemCode, string Description,
    string Unit, decimal Quantity, decimal UnitPrice, decimal Amount, int SortOrder);

public sealed record ConstructionQsReferenceDataResponse(
    IReadOnlyList<ConstructionQsContractReferenceResponse> Contracts,
    IReadOnlyList<ConstructionQsBoqReferenceResponse> ApprovedBoqRevisions);

public sealed record ConstructionQsContractReferenceResponse(int Id, string ContractNumber,
    ContractDirection Direction, ContractStatus Status,
    IReadOnlyList<ConstructionQsMilestoneReferenceResponse> PaymentMilestones,
    IReadOnlyList<ConstructionQsAppendixReferenceResponse> ApprovedAppendices);

public sealed record ConstructionQsMilestoneReferenceResponse(int Id, int Order, string Name,
    decimal Amount, PaymentMilestoneStatus Status);

public sealed record ConstructionQsAppendixReferenceResponse(int Id, int VoNumber, string Title);
public sealed record ConstructionQsBoqReferenceResponse(int Id, int RevisionNumber, string Currency);
