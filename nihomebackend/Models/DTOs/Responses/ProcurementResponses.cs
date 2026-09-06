namespace NihomeBackend.Models.DTOs.Responses;

public sealed class ProjectBoqRevisionResponse
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public int RevisionNumber { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? SourceTenderEstimateRevisionId { get; set; }
    public int? SourceContractAppendixId { get; set; }
    public decimal CostTotal { get; set; }
    public int PreparedByUserId { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? DecisionReason { get; set; }
    public bool IsFinal { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<ProjectBoqLineResponse> Lines { get; set; } = [];
}

public sealed class ProjectBoqLineResponse
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ApprovedQuantity { get; set; }
    public decimal BudgetUnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class MaterialRequestResponse
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SiteRequesterUserId { get; set; }
    public string? SiteRequesterName { get; set; }
    public int ResponsibleSiteUserId { get; set; }
    public string? ResponsibleSiteUserName { get; set; }
    public int AssignedProcurementUserId { get; set; }
    public string? AssignedProcurementUserName { get; set; }
    public DateTime RequiredAt { get; set; }
    public string? Note { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public string? DecisionReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<MaterialRequestLineResponse> Lines { get; set; } = [];
}

public sealed class MaterialRequestLineResponse
{
    public int Id { get; set; }
    public int ProjectBoqLineId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
}

public sealed class ContractLineResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int ProjectBoqLineId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public int ProcurementOwnerUserId { get; set; }
    public string? ProcurementOwnerName { get; set; }
    public decimal Quantity { get; set; }
    public decimal BudgetUnitPrice { get; set; }
    public decimal NegotiatedUnitPrice { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class WarehouseReceiptResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ReversalOfReceiptId { get; set; }
    public DateTime InspectedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? ReversalReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<WarehouseReceiptLineResponse> Lines { get; set; } = [];
}

public sealed class WarehouseReceiptLineResponse
{
    public int Id { get; set; }
    public int MaterialRequestLineId { get; set; }
    public int? ContractLineId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal ReceivedQuantity { get; set; }
}

public sealed class WarehouseIssueResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ReversalOfIssueId { get; set; }
    public int ResponsibleSiteUserId { get; set; }
    public string? ResponsibleSiteUserName { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? WorkItemCode { get; set; }
    public string? ReversalReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<WarehouseIssueLineResponse> Lines { get; set; } = [];
}

public sealed class WarehouseIssueLineResponse
{
    public int Id { get; set; }
    public int ProjectBoqLineId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public decimal IssuedQuantity { get; set; }
}

public sealed class VendorRatingResponse
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ProcurementOwnerUserId { get; set; }
    public string? ProcurementOwnerName { get; set; }
    public decimal QualityScore { get; set; }
    public decimal ScheduleScore { get; set; }
    public decimal CostScore { get; set; }
    public decimal HseScore { get; set; }
    public decimal OverallScore { get; set; }
    public string? Comments { get; set; }
    public int PreparedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DecisionReason { get; set; }
    public int? SupersedesVendorRatingId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementWorkspaceResponse
{
    public List<ProjectBoqRevisionResponse> BoqRevisions { get; set; } = [];
    public List<MaterialRequestResponse> MaterialRequests { get; set; } = [];
    public List<ContractLineResponse> ContractLines { get; set; } = [];
    public List<WarehouseReceiptResponse> Receipts { get; set; } = [];
    public List<WarehouseIssueResponse> Issues { get; set; } = [];
    public List<VendorRatingResponse> VendorRatings { get; set; } = [];
}