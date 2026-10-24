using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class CreateDepartmentWorkPackageRequest
{
    [Required, StringLength(20)]
    public string Kind { get; set; } = "Project";

    [Required, StringLength(40)]
    public string Purpose { get; set; } = "General";

    [Required, StringLength(300, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OperationalProjectId { get; set; }
    public int? ContractId { get; set; }
    public int? SourceOpportunityId { get; set; }
    public int? SourceQuoteId { get; set; }
    public int? SourceTenderId { get; set; }

    [Range(1, int.MaxValue)]
    public int DepartmentRoleGroupId { get; set; }

    [Range(1, int.MaxValue)]
    public int LeadUserId { get; set; }

    /// <summary>
    /// Additional participating departments for internal initiatives. The
    /// primary DepartmentRoleGroupId is always included by the server.
    /// Forbidden for project packages.
    /// </summary>
    public List<int> ParticipantDepartmentRoleGroupIds { get; set; } = new();

    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
}

public sealed class UpdateDepartmentWorkPackageRequest : IConcurrencyRequest
{
    [Required, StringLength(300, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? ContractId { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Planned";

    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class UpsertDepartmentWorkTaskRequest : IConcurrencyRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string WbsCode { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? Resources { get; set; }

    [Range(1, int.MaxValue)]
    public int AssigneeUserId { get; set; }

    /// <summary>Required receiving department for internal cross-department tasks.</summary>
    public int? DepartmentRoleGroupId { get; set; }

    public int? ApproverUserId { get; set; }
    public int? KpiDefinitionId { get; set; }

    [Required]
    public DateTime PlannedStart { get; set; }

    [Required]
    public DateTime PlannedEnd { get; set; }

    public List<int> CcUserIds { get; set; } = new();
    public List<int> PredecessorTaskIds { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class UpdateDepartmentWorkTaskProgressRequest : IConcurrencyRequest
{
    [Range(0, 100)]
    public int ProgressPercent { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "InProgress";

    public string? RowVersion { get; set; }
}

public sealed class VerifyDepartmentWorkTaskRequest : IConcurrencyRequest
{
    [Required]
    public bool Confirm { get; set; }

    [StringLength(2000)]
    public string? Reason { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class DepartmentWorkPackageQuery
{
    public string? Kind { get; set; }
    public int? OperationalProjectId { get; set; }
    public int? ContractId { get; set; }
    public int? SourceOpportunityId { get; set; }
    public int? SourceQuoteId { get; set; }
    public int? SourceTenderId { get; set; }
    public string? Purpose { get; set; }
    public int? DepartmentRoleGroupId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class InputPriceLineRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string ItemCode { get; set; } = string.Empty;

    [Required, StringLength(500, MinimumLength = 2)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 1)]
    public string Unit { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.0001", "999999999999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal UnitPrice { get; set; }

    [Range(typeof(decimal), "0", "100")]
    public decimal VatPercent { get; set; }

    [Range(0, 100000)]
    public int SortOrder { get; set; }
}

public sealed class UpsertInputPriceOfferRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)]
    public int VendorId { get; set; }

    [Required, StringLength(120, MinimumLength = 1)]
    public string SupplierReference { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; set; } = "VND";

    [Required]
    public DateTime ValidUntil { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    [MinLength(1)]
    public List<InputPriceLineRequest> Lines { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class WorkPackageApprovalRecipientsRequest
{
    [StringLength(150)]
    public string? ToEmail { get; set; }

    [MaxLength(20)]
    public List<string> CcEmails { get; set; } = new();
}

public sealed class SubmitBusinessRecordRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }
    [StringLength(32768)]
    public string? PreviewToken { get; set; }
}

public sealed class DecideBusinessRecordRequest : IConcurrencyRequest
{
    public bool Approved { get; set; }

    [StringLength(2000)]
    public string? Reason { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class UpsertFinanceReviewRequest : IConcurrencyRequest
{
    public bool ContractConditionsReviewed { get; set; }
    public bool GuaranteesReviewed { get; set; }
    public bool PaymentTermsReviewed { get; set; }
    public bool FinancialDossierReviewed { get; set; }

    [StringLength(4000)]
    public string? Findings { get; set; }

    public string? RowVersion { get; set; }
}
