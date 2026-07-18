using System.ComponentModel.DataAnnotations;

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
