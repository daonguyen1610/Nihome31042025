using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ActivateContractWithLegalRequest : IConcurrencyRequest
{
    public string? WorkPackageRowVersion { get; set; }
    [Range(1, int.MaxValue)] public int? TemplateId { get; set; }
    public string? TemplateRowVersion { get; set; }
    [Range(1, int.MaxValue)]
    public int LegalLeadUserId { get; set; }

    [Required]
    public DateTime PlannedStart { get; set; }

    [Required]
    public DateTime PlannedEnd { get; set; }

    public List<LegalPermitSetupItemRequest> Permits { get; set; } = new();

    [Required]
    public string? RowVersion { get; set; }
}

public sealed class SetupProjectLegalRequest
{
    [Range(1, int.MaxValue)] public int? TemplateId { get; set; }
    public string? TemplateRowVersion { get; set; }
    public string? WorkPackageRowVersion { get; set; }
    [Range(1, int.MaxValue)]
    public int LegalLeadUserId { get; set; }

    [Required]
    public DateTime PlannedStart { get; set; }

    [Required]
    public DateTime PlannedEnd { get; set; }

    public List<LegalPermitSetupItemRequest> Permits { get; set; } = new();
}

public sealed class LegalPermitSetupItemRequest
{
    [Required, StringLength(60)]
    public string PermitTypeCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string? IssuingAgency { get; set; }

    [Range(1, int.MaxValue)]
    public int AssigneeUserId { get; set; }

    [Range(1, int.MaxValue)]
    public int ApproverUserId { get; set; }

    [Required]
    public DateTime PlannedStart { get; set; }

    [Required]
    public DateTime PlannedEnd { get; set; }

    public List<string> PredecessorPermitTypeCodes { get; set; } = new();
}
