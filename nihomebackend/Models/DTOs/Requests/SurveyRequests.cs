using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateSurveyRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Location { get; set; } = string.Empty;

    [StringLength(80)]
    public string? ConstructionTypeCode { get; set; }

    [Required]
    public DateTime SurveyDate { get; set; }

    public int? SurveyorUserId { get; set; }

    public int? LinkedProjectId { get; set; }
    public int? LinkedOpportunityId { get; set; }
    [Required(ErrorMessage = "Vui lòng chọn Dự án vận hành.")]
    [Range(1, int.MaxValue, ErrorMessage = "Dự án vận hành không hợp lệ. Hãy chọn một dự án có mã số lớn hơn 0, ví dụ 1.")]
    public int? OperationalProjectId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>
/// Update payload — same shape as create. NIH-100 will layer edit rules
/// on top; NIH-99 only reads.
/// </summary>
public class UpdateSurveyRequest : CreateSurveyRequest
{
}

public class SurveyListParams
{
    /// <summary>Master-data code from <c>construction_type</c>.</summary>
    public string? ConstructionTypeCode { get; set; }

    public int? SurveyorUserId { get; set; }
    public int? LinkedProjectId { get; set; }

    /// <summary>Multi-select filter — comma-separated enum names (NotSynced / Syncing / Synced / Failed).</summary>
    public string? DriveSyncStatus { get; set; }

    /// <summary>Inclusive lower bound of <c>SurveyDate</c>.</summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>Inclusive upper bound of <c>SurveyDate</c>.</summary>
    public DateTime? DateTo { get; set; }

    /// <summary>Substring match against <c>Location</c> + <c>Code</c>.</summary>
    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SurveyMediaUploadRequest
{
    [Required]
    public IFormFile? File { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    public DateTime? CapturedAt { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}

public class UpdateSurveyChecklistResultRequest
{
    [Required]
    public SurveyChecklistStatus? Status { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }

    [Range(0, 10000)]
    public int SortOrder { get; set; }
}

public sealed class ReplaceSurveySiteConditionsRequest
{
    [Required]
    [MinLength(1)]
    public List<SurveySiteConditionRequest> Conditions { get; set; } = [];
}

public sealed class SurveySiteConditionRequest
{
    [Required]
    [StringLength(40)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string StatusCode { get; set; } = string.Empty;

    public decimal? NumericValue { get; set; }

    [StringLength(20)]
    public string? UnitCode { get; set; }

    [StringLength(80)]
    public string? ReferenceCode { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}
