namespace NihomeBackend.Models;

public sealed class CmScheduleBaseline : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int Version { get; set; }
    public int CrmRevisionId { get; set; }
    public string Status { get; set; } = "Draft";
    public string ChangeReason { get; set; } = string.Empty;
    public string WeightBasisNote { get; set; } = string.Empty;
    public string WorkingDaysOfWeekJson { get; set; } = "[1,2,3,4,5,6]";
    public string NonWorkingDatesJson { get; set; } = "[]";
    public string ItemsJson { get; set; } = "[]";
    public int SiteManagerUserId { get; set; }
    public int ApproverUserId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewNote { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CmProgressSnapshot : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int BaselineId { get; set; }
    public CmScheduleBaseline Baseline { get; set; } = null!;
    public DateOnly AsOfDate { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public int? CapturedByUserId { get; set; }
    public string InputsJson { get; set; } = "[]";
    public decimal CrmPercent { get; set; }
    public decimal PlanPercent { get; set; }
    public decimal ActualPercent { get; set; }
    public decimal DelayPercent { get; set; }
    public decimal ThresholdPercent { get; set; }
    public bool IsDelayed { get; set; }
    public string? Explanation { get; set; }
    public string? RecoveryPlan { get; set; }
    public DateOnly? RecoveryDueDate { get; set; }
    public int? RecoveryByUserId { get; set; }
    public DateTime? RecoveryAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
