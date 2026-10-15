using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public sealed record ConstructionPreparationResponse(
    int Id, int OperationalProjectId, int Version, ConstructionPreparationStatus Status,
    string WbsBaselineReference, string ScheduleBaselineReference, string SCurveBaselineReference,
    string BchOrganizationReference, string ResourcePlanReference, string BudgetBaselineReference,
    bool BudgetBaselineLocked,
    bool SiteHandoverRequired, bool SiteHandoverComplete, bool MethodStatementRequired,
    bool MethodStatementApproved, bool HsePlanRequired, bool HsePlanApproved,
    IReadOnlyList<string> MissingConditions, string? ReadinessSnapshotJson, string RowVersion);

public sealed record ConstructionResourceRequestResponse(
    int Id, string Code, ConstructionResourceType Type, ConstructionReviewStatus Status,
    string WbsCode, DateTime NeedByDate, decimal Quantity, string Unit, string Reason,
    int? ProjectBoqLineId, int CreatedByUserId, string? CmDecisionReason,
    string? HandoffEntityType, long? HandoffEntityId, ConstructionHandoffStatus HandoffStatus,
    string RowVersion);

public sealed record QaQcTemplateResponse(int Id, string Code, int Version, string NameVi,
    string Name, string NameZh, string NameJa, string SchemaJson, bool IsPublished, string RowVersion);

public sealed record QaQcDossierResponse(int Id, string Code, int TemplateId, int TemplateVersion,
    string TemplateSnapshotJson, int? IfcReleaseId, string ChecklistResultsJson, string WbsCode, string Title, ConstructionReviewStatus Status,
    int CreatedByUserId, IReadOnlyList<long> EvidenceDocumentIds, string? CmDecisionReason, string RowVersion);

public sealed record ConstructionQsBatchResponse(int Id, string Code, ConstructionQsDirection Direction,
    ConstructionReviewStatus Status, int ContractId, string WbsCode, DateTime PeriodFrom, DateTime PeriodTo, int? ProjectBoqRevisionId,
    int? ContractAppendixId, int? ContractPaymentMilestoneId, decimal CertifiedAmount,
    string Description, int CreatedByUserId, string? CmDecisionReason, int? SupersedesBatchId,
    int? ReversesBatchId, IReadOnlyList<ConstructionQsBatchLineResponse> Lines,
    IReadOnlyList<long> EvidenceDocumentIds, string RowVersion);

public sealed record ConstructionQsBatchLineResponse(int Id, string ItemCode, string Description,
    string Unit, decimal Quantity, decimal UnitPrice, decimal Amount, int SortOrder);
