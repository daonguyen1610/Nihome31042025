namespace NihomeBackend.Models;

public class KpiDefinition : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinimumAcceptableScore { get; set; }
    public KpiTargetDirection TargetDirection { get; set; } = KpiTargetDirection.HigherIsBetter;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<KpiScoreSnapshot> ScoreSnapshots { get; set; } = new();
}

public class KpiPeriod : IConcurrencyTracked
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public KpiPeriodStatus Status { get; set; } = KpiPeriodStatus.Open;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime? LockedAt { get; set; }
    public int? LockedByUserId { get; set; }
    public ApplicationUser? LockedByUser { get; set; }
    public string? LockNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<KpiScoreSnapshot> ScoreSnapshots { get; set; } = new();
}

public class KpiScoreSnapshot
{
    public long Id { get; set; }
    public int KpiPeriodId { get; set; }
    public KpiPeriod KpiPeriod { get; set; } = null!;
    public int KpiDefinitionId { get; set; }
    public KpiDefinition KpiDefinition { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public int DefinitionVersion { get; set; }
    public string DefinitionCode { get; set; } = string.Empty;
    public string DefinitionNameKey { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public decimal DefinitionWeight { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinimumAcceptableScore { get; set; }
    public KpiTargetDirection TargetDirection { get; set; }
    public decimal? RawValue { get; set; }
    public decimal? Numerator { get; set; }
    public decimal? Denominator { get; set; }
    public decimal? Score { get; set; }
    public decimal? WeightedScore { get; set; }
    public KpiScoreStatus Status { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public enum KpiTargetDirection
{
    HigherIsBetter = 0,
    LowerIsBetter = 1,
}

public enum KpiPeriodStatus
{
    Open = 0,
    Locked = 1,
}

public enum KpiScoreStatus
{
    Available = 0,
    MissingData = 1,
    MissingConfiguration = 2,
}
