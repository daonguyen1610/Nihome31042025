using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpdateKpiDefinitionRequest : IConcurrencyRequest
{
    [Range(typeof(decimal), "0.01", "1")]
    public decimal Weight { get; set; }
    public decimal? TargetValue { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal? MinimumAcceptableScore { get; set; }
    public KpiTargetDirection TargetDirection { get; set; }
    public bool IsActive { get; set; }
    public string? RowVersion { get; set; }
}

public class CalculateKpiPeriodRequest
{
    [Range(2020, 2100)]
    public int Year { get; set; }
    [Range(1, 12)]
    public int Month { get; set; }
    public int? UserId { get; set; }
}

public class LockKpiPeriodRequest : IConcurrencyRequest
{
    public int? UserId { get; set; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string Note { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}
