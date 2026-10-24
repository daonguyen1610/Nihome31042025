namespace NihomeBackend.Models.DTOs.Responses;

public sealed class DepartmentWorkPackageListResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<DepartmentWorkPackageResponse> Items { get; set; } = new();
}

public sealed class DepartmentWorkPackageResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? OperationalProjectId { get; set; }
    public string? OperationalProjectCode { get; set; }
    public string? OperationalProjectName { get; set; }
    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }
    public int? SourceOpportunityId { get; set; }
    public string? SourceOpportunityName { get; set; }
    public int? SourceQuoteId { get; set; }
    public string? SourceQuoteCode { get; set; }
    public int? SourceTenderId { get; set; }
    public string? SourceTenderCode { get; set; }
    public int DepartmentRoleGroupId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentLabelKey { get; set; } = string.Empty;
    public List<DepartmentOptionResponse> ParticipantDepartments { get; set; } = new();
    public int LeadUserId { get; set; }
    public string LeadUserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProgressPercent { get; set; }
    public bool CanManagePackage { get; set; }
    public bool CanAssignTasks { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<DepartmentWorkTaskResponse> Tasks { get; set; } = new();
    public List<InputPriceOfferResponse> InputPriceOffers { get; set; } = new();
    public FinanceReviewResponse? FinanceReview { get; set; }
    public List<WorkPackageApprovalEmailResponse> ApprovalEmails { get; set; } = new();
}

public sealed record WorkPackageApprovalEmailResponse(int Id, string RecordType, int RecordId,
    string ToEmail, string Status, DateTime CreatedAt, DateTime? SentAt, IReadOnlyList<string> CcEmails);

public sealed class InputPriceOfferResponse
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public int VendorId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string VendorType { get; set; } = string.Empty;
    public string SupplierReference { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? DecisionReason { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public int CreatedByUserId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<InputPriceLineResponse> Lines { get; set; } = new();
}

public sealed class InputPriceLineResponse
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatPercent { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public sealed class FinanceReviewResponse
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool ContractConditionsReviewed { get; set; }
    public bool GuaranteesReviewed { get; set; }
    public bool PaymentTermsReviewed { get; set; }
    public bool FinancialDossierReviewed { get; set; }
    public string? Findings { get; set; }
    public string? DecisionReason { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DepartmentWorkTaskResponse
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public string WbsCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Resources { get; set; }
    public int DepartmentRoleGroupId { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentLabelKey { get; set; } = string.Empty;
    public int AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public int? KpiDefinitionId { get; set; }
    public string? KpiDefinitionCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public List<int> CcUserIds { get; set; } = new();
    public List<int> PredecessorTaskIds { get; set; } = new();
    public bool CanUpdateProgress { get; set; }
    public bool CanVerify { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class DepartmentWorkPackageOptionsResponse
{
    public List<DepartmentOptionResponse> Departments { get; set; } = new();
    public List<DepartmentUserOptionResponse> Users { get; set; } = new();
    public List<KpiDefinitionOptionResponse> KpiDefinitions { get; set; } = new();
    public List<VendorOptionResponse> Vendors { get; set; } = new();
}

public sealed class VendorOptionResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class DepartmentOptionResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string LabelKey { get; set; } = string.Empty;
    public bool CanReceiveProjectPackage { get; set; }
}

public sealed class DepartmentUserOptionResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleGroupId { get; set; }
    public bool CanLead { get; set; }
}

public sealed class KpiDefinitionOptionResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
}

public sealed class DepartmentWorkPackageHistoryResponse
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
}
