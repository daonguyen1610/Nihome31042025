using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class UpsertOperationalProjectMemberRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required, StringLength(150, MinimumLength = 2)]
    public string Position { get; set; } = string.Empty;

    public int? ReportsToMemberId { get; set; }

    [Required]
    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    [Required, MinLength(1)]
    public List<ProjectMemberRoleRequest> Roles { get; set; } = new();

    public string? RowVersion { get; set; }
}

public sealed class ProjectMemberRoleRequest
{
    [Required, StringLength(50)]
    public string RoleCode { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Scope { get; set; } = "Project";

    [StringLength(80)]
    public string? ScopeValue { get; set; }
}

public sealed class UpsertOperationalProjectAssignmentRequest : IConcurrencyRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string WorkKey { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 2)]
    public string Module { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Discipline { get; set; }

    [StringLength(80)]
    public string? ParallelGroup { get; set; }

    [Range(1, int.MaxValue)]
    public int AssigneeMemberId { get; set; }

    public int? ManagerMemberId { get; set; }

    [Required, StringLength(30)]
    public string Status { get; set; } = "Planned";

    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    public string? RowVersion { get; set; }
}
