namespace NihomeBackend.Models;

public sealed class SurveySiteCondition
{
    public long Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    public SurveySiteConditionCategory Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public SurveySiteConditionStatus Status { get; set; }
    public decimal? NumericValue { get; set; }
    public string? UnitCode { get; set; }
    public string? ReferenceCode { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public enum SurveySiteConditionCategory
{
    RightOfWay = 0,
    Elevation = 1,
    Infrastructure = 2,
}

public enum SurveySiteConditionStatus
{
    Unknown = 0,
    Available = 1,
    Unavailable = 2,
    NeedsInvestigation = 3,
}