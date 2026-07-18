namespace NihomeBackend.Models.DTOs.Responses;

public class SurveyResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public string? ConstructionTypeCode { get; set; }
    /// <summary>Resolved label for <see cref="ConstructionTypeCode"/> from master data.</summary>
    public string? ConstructionTypeLabel { get; set; }

    public DateTime SurveyDate { get; set; }

    public int? SurveyorUserId { get; set; }
    public string? SurveyorName { get; set; }

    public int? LinkedProjectId { get; set; }
    public string? LinkedProjectName { get; set; }

    public int? LinkedOpportunityId { get; set; }
    public string? LinkedOpportunityName { get; set; }

    public string? Note { get; set; }

    public string DriveSyncStatus { get; set; } = string.Empty;
    public string? DriveSyncError { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Slimmed-down row used by the NIH-99 list view.</summary>
public class SurveyListItemResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public string? ConstructionTypeCode { get; set; }
    public string? ConstructionTypeLabel { get; set; }

    public DateTime SurveyDate { get; set; }

    public int? SurveyorUserId { get; set; }
    public string? SurveyorName { get; set; }

    public int? LinkedProjectId { get; set; }
    public string? LinkedProjectName { get; set; }

    public int? LinkedOpportunityId { get; set; }
    public string? LinkedOpportunityName { get; set; }

    public string DriveSyncStatus { get; set; } = string.Empty;
    public string? DriveSyncError { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class SurveyListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SurveyListItemResponse> Items { get; set; } = new();
}

/// <summary>
/// One row of the NIH-101 detail-page History tab. Sourced from the audit
/// log stream filtered by <c>ResourceType == EntityTypes.Survey</c> and the
/// row id.
/// </summary>
public class SurveyTimelineEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
}
