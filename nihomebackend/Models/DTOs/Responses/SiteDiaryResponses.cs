namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Wire shape for a single <c>SiteDiary</c>.</summary>
public class SiteDiaryResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string? DesignProjectName { get; set; }

    public DateOnly DiaryDate { get; set; }
    public string WeatherCode { get; set; } = string.Empty;
    public string? WeatherLabel { get; set; }
    public string? WeatherNote { get; set; }

    public int HeadcountLabor { get; set; }
    public int HeadcountEngineers { get; set; }
    public int HeadcountSupervisors { get; set; }
    public int HeadcountSubcontractors { get; set; }
    /// <summary>Derived server-side so the list header stays trivial.</summary>
    public int HeadcountTotal { get; set; }

    public string? MachinesSummary { get; set; }
    public string? MaterialsReceived { get; set; }
    public string WorkPerformed { get; set; } = string.Empty;
    public string? Incidents { get; set; }
    public string? Note { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public string? ConfirmedByName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SiteDiaryListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SiteDiaryResponse> Items { get; set; } = new();
    /// <summary>Per-status roll-up over the current filter scope.</summary>
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

public class SiteDiaryBulkDeleteResponse
{
    public int Requested { get; set; }
    public int Deleted { get; set; }
    public List<SiteDiaryBulkDeleteFailure> Failures { get; set; } = new();
}

public class SiteDiaryBulkDeleteFailure
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}
