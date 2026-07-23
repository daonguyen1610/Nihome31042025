namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// List/filter parameters for <c>GET /api/site-diaries</c> — matches the
/// NIH-142 slice-1 DoD (project + date range + weather + status +
/// text search + pagination).
/// </summary>
public class SiteDiaryListParams
{
    public int? DesignProjectId { get; set; }
    public string? WeatherCode { get; set; }
    /// <summary>Comma-separated status names (Draft,Submitted,Confirmed).</summary>
    public string? Status { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    /// <summary>Matches work-performed / incidents / materials text.</summary>
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class CreateSiteDiaryRequest
{
    public int DesignProjectId { get; set; }
    public DateOnly DiaryDate { get; set; }
    public string WeatherCode { get; set; } = string.Empty;
    public string? WeatherNote { get; set; }
    public int HeadcountLabor { get; set; }
    public int HeadcountEngineers { get; set; }
    public int HeadcountSupervisors { get; set; }
    public int HeadcountSubcontractors { get; set; }
    public string? MachinesSummary { get; set; }
    public string? MaterialsReceived { get; set; }
    public string WorkPerformed { get; set; } = string.Empty;
    public string? Incidents { get; set; }
    public string? Note { get; set; }
}

public class UpdateSiteDiaryRequest
{
    public DateOnly DiaryDate { get; set; }
    public string WeatherCode { get; set; } = string.Empty;
    public string? WeatherNote { get; set; }
    public int HeadcountLabor { get; set; }
    public int HeadcountEngineers { get; set; }
    public int HeadcountSupervisors { get; set; }
    public int HeadcountSubcontractors { get; set; }
    public string? MachinesSummary { get; set; }
    public string? MaterialsReceived { get; set; }
    public string WorkPerformed { get; set; } = string.Empty;
    public string? Incidents { get; set; }
    public string? Note { get; set; }
}

public class BulkDeleteSiteDiariesRequest
{
    public List<int> Ids { get; set; } = new();
}
