namespace NihomeBackend.Models.DTOs.Responses;

public sealed class MyWorkResponse
{
    /// <summary>Today in the configured display time zone; the groups are computed against it.</summary>
    public DateOnly Today { get; set; }
    public int DueSoonDays { get; set; }
    public List<MyWorkItemResponse> Items { get; set; } = new();
}

public sealed class MyWorkItemResponse
{
    /// <summary>WorkTask, DesignTask or PunchItem (see <c>MyWorkSources</c>).</summary>
    public string Source { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>The work package, design dossier or project the work belongs to.</summary>
    public string? ContainerCode { get; set; }
    public string? ContainerName { get; set; }
    public bool IsInternal { get; set; }
    /// <summary>Assigned (I do it) or Created (I gave it to someone else).</summary>
    public string Relation { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public int ProgressPercent { get; set; }
    public DateOnly? PlannedStart { get; set; }
    public DateOnly? PlannedEnd { get; set; }
    public bool IsToday { get; set; }
    public bool IsDueSoon { get; set; }
    public bool IsOverdue { get; set; }
    public int? OverdueDays { get; set; }
    public bool IsPriority { get; set; }
    public string LinkUrl { get; set; } = string.Empty;
}

public sealed class DailyWorkReportResponse
{
    /// <summary>0 for a draft that has not been submitted yet.</summary>
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateOnly ReportDate { get; set; }
    public string? Proposals { get; set; }
    public string TomorrowPlan { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ReviewComment { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanReview { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<DailyWorkReportItemResponse> Items { get; set; } = new();
}

public sealed class DailyWorkReportItemResponse
{
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string? Note { get; set; }
}

public sealed class DepartmentDailyReportsResponse
{
    public DateOnly ReportDate { get; set; }
    public int DepartmentRoleGroupId { get; set; }
    public string DepartmentLabelKey { get; set; } = string.Empty;
    public List<DailyWorkReportResponse> Reports { get; set; } = new();
    /// <summary>Department members who have not reported for the day.</summary>
    public List<DailyReportMemberResponse> MissingMembers { get; set; } = new();
}

public sealed class DailyReportMemberResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}
