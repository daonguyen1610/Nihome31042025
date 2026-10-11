using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class CreateDepartmentWorkPackageRequest
{
    [Required, StringLength(20)]
    public string Kind { get; set; } = "Project";

    [Required, StringLength(300, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OperationalProjectId { get; set; }
    public int? ContractId { get; set; }
    public int? SourceOpportunityId { get; set; }

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

    public string? RowVersion { get; set; }
}

public sealed class DepartmentWorkPackageQuery
{
    public string? Kind { get; set; }
    public int? OperationalProjectId { get; set; }
    public int? ContractId { get; set; }
    public int? SourceOpportunityId { get; set; }
    public int? DepartmentRoleGroupId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
