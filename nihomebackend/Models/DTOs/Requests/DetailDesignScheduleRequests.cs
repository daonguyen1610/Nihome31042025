using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class InitializeDesignScheduleRequest
{
    [Required, MinLength(3), MaxLength(3)]
    public List<InitializeDesignSchedulePhaseRequest> Phases { get; set; } = new();
}

public sealed class InitializeDesignSchedulePhaseRequest
{
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Weight { get; set; }
}

public sealed class UpsertDesignSchedulePhaseRequest
{
    [Required]
    public DateOnly? PlannedStart { get; set; }

    [Required]
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }

    [Required, MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    [Range(0, 100)]
    public int ProgressPercent { get; set; }

    [Range(1, 100)]
    public int Weight { get; set; }

    public string? RowVersion { get; set; }
}

public sealed class UpsertDesignScheduleTaskRequest
{
    [Required, MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string DepartmentCode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int AssigneeMemberId { get; set; }

    public bool IsMilestone { get; set; }

    [Required]
    public DateOnly? PlannedStart { get; set; }

    [Required]
    public DateOnly? PlannedEnd { get; set; }
    public DateOnly? ActualStart { get; set; }
    public DateOnly? ActualEnd { get; set; }

    [Required, MaxLength(40)]
    public string Status { get; set; } = string.Empty;

    [Range(0, 100)]
    public int ProgressPercent { get; set; }

    [Range(1, 100)]
    public int Weight { get; set; }

    [Required, MaxLength(100)]
    public List<int>? PredecessorTaskIds { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class DesignScheduleQuery
{
    public string? Phase { get; set; }
    public int? AssigneeMemberId { get; set; }
    public string? DepartmentCode { get; set; }
    public string? Status { get; set; }
    public DateOnly? PlannedFrom { get; set; }
    public DateOnly? PlannedTo { get; set; }
    public bool OverdueOnly { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}