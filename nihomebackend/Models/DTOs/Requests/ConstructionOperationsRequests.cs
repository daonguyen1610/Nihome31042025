using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class UpsertConstructionPreparationRequest : IConcurrencyRequest
{
    [Required, MaxLength(500)] public string WbsBaselineReference { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string ScheduleBaselineReference { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string SCurveBaselineReference { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string BchOrganizationReference { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string ResourcePlanReference { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string BudgetBaselineReference { get; set; } = string.Empty;
    public bool BudgetBaselineLocked { get; set; }
    public bool SiteHandoverRequired { get; set; }
    public bool SiteHandoverComplete { get; set; }
    public bool MethodStatementRequired { get; set; }
    public bool MethodStatementApproved { get; set; }
    public bool HsePlanRequired { get; set; }
    public bool HsePlanApproved { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class UpsertConstructionResourceRequest : IConcurrencyRequest
{
    [Required] public ConstructionResourceType Type { get; set; }
    [Required, MaxLength(100)] public string WbsCode { get; set; } = string.Empty;
    [Required] public DateTime? NeedByDate { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
    [Required, MaxLength(50)] public string Unit { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int? ProjectBoqLineId { get; set; }
    [Range(1, int.MaxValue)] public int? ResponsibleSiteUserId { get; set; }
    [Range(1, int.MaxValue)] public int? AssignedProcurementUserId { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class CmDecisionRequest : IConcurrencyRequest
{
    public bool Approved { get; set; }
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public sealed class CreateQaQcTemplateRequest
{
    [Required, MaxLength(60)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string NameVi { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string NameZh { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string NameJa { get; set; } = string.Empty;
    [Required, MaxLength(20000)] public string SchemaJson { get; set; } = "{}";
}

public sealed class CreateQaQcDossierRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int TemplateId { get; set; }
    [Required, MaxLength(100)] public string WbsCode { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Title { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int? IfcReleaseId { get; set; }
    [Required, MaxLength(20000)] public string ChecklistResultsJson { get; set; } = "[]";
    [MaxLength(100)] public List<long> EvidenceDocumentIds { get; set; } = [];
    public string? RowVersion { get; set; }
}

public sealed class CreateConstructionQsBatchRequest : IConcurrencyRequest
{
    [Required] public ConstructionQsDirection Direction { get; set; }
    [Range(1, int.MaxValue)] public int ContractId { get; set; }
    [Required, MaxLength(100)] public string WbsCode { get; set; } = string.Empty;
    [Required] public DateTime? PeriodFrom { get; set; }
    [Required] public DateTime? PeriodTo { get; set; }
    [Range(1, int.MaxValue)] public int? ProjectBoqRevisionId { get; set; }
    [Range(1, int.MaxValue)] public int? ContractAppendixId { get; set; }
    [Range(1, int.MaxValue)] public int? ContractPaymentMilestoneId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999999.99")] public decimal CertifiedAmount { get; set; }
    [Required, MaxLength(2000)] public string Description { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(500)] public List<ConstructionQsBatchLineRequest> Lines { get; set; } = [];
    [MaxLength(100)] public List<long> EvidenceDocumentIds { get; set; } = [];
    [Range(1, int.MaxValue)] public int? SupersedesBatchId { get; set; }
    [Range(1, int.MaxValue)] public int? ReversesBatchId { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ConstructionQsBatchLineRequest
{
    [Required, MaxLength(80)] public string ItemCode { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Unit { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
    [Range(typeof(decimal), "0", "999999999999.9999")] public decimal UnitPrice { get; set; }
}
