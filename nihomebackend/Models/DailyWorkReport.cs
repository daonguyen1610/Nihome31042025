using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Models;

/// <summary>
/// A staff member's end-of-day report (QLNS-M05 / CV-02): the work done with its
/// completion, proposals and tomorrow's plan. Their department head reviews it.
/// </summary>
public class DailyWorkReport : IConcurrencyTracked
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    /// <summary>The author's department when they submitted, so its head keeps sight of it.</summary>
    public int DepartmentRoleGroupId { get; set; }
    public RoleGroup DepartmentRoleGroup { get; set; } = null!;
    public DateOnly ReportDate { get; set; }
    public string? Proposals { get; set; }
    public string TomorrowPlan { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ReviewComment { get; set; }
    public int? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<DailyWorkReportItem> Items { get; set; } = new();
}

/// <summary>
/// One line of a daily report. It copies the work's title and completion at the
/// time of reporting, so later changes to the work do not rewrite the report.
/// </summary>
public class DailyWorkReportItem
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public DailyWorkReport Report { get; set; } = null!;
    /// <summary>The kind of work it came from (see <c>MyWorkSources</c>); null for a line typed in.</summary>
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}
