using System.ComponentModel.DataAnnotations;
namespace NihomeBackend.Models.DTOs.Requests;

public sealed class CmScheduleDraftRequest
{
    [Required, StringLength(2000, MinimumLength = 3)] public string ChangeReason { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SiteManagerUserId { get; set; }
    [Required, MinLength(1), MaxLength(2000)] public List<CmSchedulePlanRequest> Items { get; set; } = [];
}
public sealed class CmSchedulePlanRequest
{
    [Range(1, int.MaxValue)] public int TaskId { get; set; }
    public DateOnly PlannedStart { get; set; }
    public DateOnly PlannedEnd { get; set; }
    [Range(typeof(decimal), "0.0001", "1000000000")] public decimal Weight { get; set; }
}
public sealed class CmScheduleDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}
public sealed class CmRecoveryRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(2000, MinimumLength = 3)] public string Explanation { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 3)] public string RecoveryPlan { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
}

public sealed class CmActualUpdateRequest
{
    [Required] public DateTime? ExpectedUpdatedAt { get; set; }
    [Required, Range(0, 100)] public int? ProgressPercent { get; set; }
}

public sealed class CmScheduleSubmitRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required] public string PreviewToken { get; set; } = string.Empty;
}
