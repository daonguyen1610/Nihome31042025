using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ProjectBoqRevisionRequest : IConcurrencyRequest
{
    [Required, RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; set; } = "VND";
    [Range(1, int.MaxValue)]
    public int? SourceTenderEstimateRevisionId { get; set; }
    [Range(1, int.MaxValue)]
    public int? SourceContractAppendixId { get; set; }
    [Required, MinLength(1), MaxLength(1000)]
    public List<ProjectBoqLineRequest> Lines { get; set; } = [];
    public string? RowVersion { get; set; }
}

public sealed class ProjectBoqLineRequest
{
    [Required, MaxLength(80), RegularExpression("^[A-Za-z0-9][A-Za-z0-9._/-]*$")]
    public string ItemCode { get; set; } = string.Empty;
    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    [Required, MaxLength(50)]
    public string Unit { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "999999999999.999999")]
    public decimal ApprovedQuantity { get; set; }
    [Range(typeof(decimal), "0", "999999999999.9999")]
    public decimal BudgetUnitPrice { get; set; }
}

public sealed class ProcurementDecisionRequest : IConcurrencyRequest
{
    public bool Approved { get; set; }
    [MaxLength(2000)]
    public string? Reason { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ProcurementTransitionRequest : IConcurrencyRequest
{
    [MaxLength(2000)]
    public string? Reason { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class MaterialRequestListParams
{
    [MaxLength(200)]
    public string? Search { get; set; }
    public MaterialRequestStatus? Status { get; set; }
    [Range(1, int.MaxValue)]
    public int? SiteRequesterUserId { get; set; }
    [Range(1, int.MaxValue)]
    public int? ResponsibleSiteUserId { get; set; }
    [Range(1, int.MaxValue)]
    public int? AssignedProcurementUserId { get; set; }
    public DateOnly? RequiredFrom { get; set; }
    public DateOnly? RequiredTo { get; set; }
    [MaxLength(30)]
    public string? SortBy { get; set; }
    [MaxLength(4)]
    public string? SortDirection { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class MaterialRequestUpsertRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)]
    public int ResponsibleSiteUserId { get; set; }
    [Range(1, int.MaxValue)]
    public int AssignedProcurementUserId { get; set; }
    [Required]
    public DateTime? RequiredAt { get; set; }
    [MaxLength(2000)]
    public string? Note { get; set; }
    [Required, MinLength(1), MaxLength(500)]
    public List<MaterialRequestLineRequest> Lines { get; set; } = [];
    public string? RowVersion { get; set; }
}

public sealed class MaterialRequestLineRequest
{
    [Range(1, int.MaxValue)]
    public int ProjectBoqLineId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal RequestedQuantity { get; set; }
}

public sealed class ContractLineUpsertRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)]
    public int ContractId { get; set; }
    [Range(1, int.MaxValue)]
    public int ProjectBoqLineId { get; set; }
    [Range(1, int.MaxValue)]
    public int ProcurementOwnerUserId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999999.9999")]
    public decimal NegotiatedUnitPrice { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class WarehouseReceiptCreateRequest
{
    [Required]
    public DateTime? InspectedAt { get; set; }
    [Range(1, int.MaxValue)]
    public int ReceivedByUserId { get; set; }
    [Required, MinLength(1), MaxLength(500)]
    public List<WarehouseReceiptLineRequest> Lines { get; set; } = [];
}

public sealed class WarehouseReceiptLineRequest
{
    [Range(1, int.MaxValue)]
    public int MaterialRequestLineId { get; set; }
    [Range(1, int.MaxValue)]
    public int? ContractLineId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal ReceivedQuantity { get; set; }
}

public sealed class WarehouseIssueCreateRequest
{
    [Required]
    public DateTime? IssuedAt { get; set; }
    [Range(1, int.MaxValue)]
    public int ResponsibleSiteUserId { get; set; }
    [Range(1, int.MaxValue)]
    public int IssuedByUserId { get; set; }
    [MaxLength(100)]
    public string? WorkItemCode { get; set; }
    [Required, MinLength(1), MaxLength(500)]
    public List<WarehouseIssueLineRequest> Lines { get; set; } = [];
}

public sealed class WarehouseIssueLineRequest
{
    [Range(1, int.MaxValue)]
    public int ProjectBoqLineId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal IssuedQuantity { get; set; }
}

public sealed class WarehouseReversalRequest : IConcurrencyRequest
{
    [Required, MinLength(3), MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public sealed class VendorRatingUpsertRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)]
    public int ContractId { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal QualityScore { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal ScheduleScore { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal CostScore { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal HseScore { get; set; }
    [MaxLength(4000)]
    public string? Comments { get; set; }
    public string? RowVersion { get; set; }
}