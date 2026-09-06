using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public class KpiUserOptionResponse
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
}

public class KpiDefinitionResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool RequiresTarget { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinimumAcceptableScore { get; set; }
    public KpiTargetDirection TargetDirection { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class KpiScoreResponse
{
    public long Id { get; set; }
    public int DefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinimumAcceptableScore { get; set; }
    public decimal? RawValue { get; set; }
    public decimal? Numerator { get; set; }
    public decimal? Denominator { get; set; }
    public decimal? Score { get; set; }
    public decimal? WeightedScore { get; set; }
    public KpiScoreStatus Status { get; set; }
    public string EvidenceJson { get; set; } = "{}";
    public int DefinitionVersion { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class KpiDashboardResponse
{
    public int PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public KpiPeriodStatus PeriodStatus { get; set; }
    public string PeriodRowVersion { get; set; } = string.Empty;
    public DateTime? LockedAt { get; set; }
    public string? LockedByName { get; set; }
    public string? LockNote { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public decimal? TotalScore { get; set; }
    public decimal AvailableWeight { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? LastCalculatedAt { get; set; }
    public List<KpiScoreResponse> Scores { get; set; } = new();
}
