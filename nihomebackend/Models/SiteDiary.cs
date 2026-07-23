namespace NihomeBackend.Models;

/// <summary>
/// M4 Construction & Acceptance — Site Diary (Nhật ký công trình / NIH-142).
///
/// One row per <see cref="DesignProject"/> per calendar day. Captures
/// the site-engineer's daily entry: weather, headcount by role, work
/// performed, materials received, machines on site, incidents raised.
///
/// Slice 1 (NIH-142): three-step lifecycle Draft → Submitted → Confirmed
/// with the strict "confirm" permission gate. Edits are only allowed in
/// Draft. Photos + attachments land in slice 2 (they hang off the same
/// row when the Digital Assets module is ready).
/// </summary>
public class SiteDiary
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>The site date — unique together with the project id.</summary>
    public DateOnly DiaryDate { get; set; }

    /// <summary>Master-data code from <c>diary_weather</c> (Sunny/Cloudy/Rain…).</summary>
    public string WeatherCode { get; set; } = string.Empty;

    /// <summary>Free-text weather note (temperature, wind, etc.).</summary>
    public string? WeatherNote { get; set; }

    // Headcount rolls up into the daily crew count on the report. Kept
    // as separate columns instead of a nested list so slice-1 stays
    // trivially reportable.
    public int HeadcountLabor { get; set; }
    public int HeadcountEngineers { get; set; }
    public int HeadcountSupervisors { get; set; }
    public int HeadcountSubcontractors { get; set; }

    /// <summary>Free text — "1 tower crane, 2 excavators…".</summary>
    public string? MachinesSummary { get; set; }

    /// <summary>Free text — "8 T rebar #16, 45 m³ ready-mix C30…".</summary>
    public string? MaterialsReceived { get; set; }

    /// <summary>Main narrative — what was done today.</summary>
    public string WorkPerformed { get; set; } = string.Empty;

    /// <summary>Incidents / near-misses / safety notes.</summary>
    public string? Incidents { get; set; }

    public SiteDiaryStatus Status { get; set; } = SiteDiaryStatus.Draft;

    /// <summary>Working note — visible only inside the diary detail.</summary>
    public string? Note { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public ApplicationUser? ConfirmedBy { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Lifecycle of a <see cref="SiteDiary"/>.</summary>
public enum SiteDiaryStatus
{
    /// <summary>Being drafted by the site engineer — freely editable.</summary>
    Draft = 0,
    /// <summary>Submitted for PM confirmation — read-only until reopened.</summary>
    Submitted = 1,
    /// <summary>PM has confirmed the entry — becomes the source of truth
    /// for weekly / monthly progress reports.</summary>
    Confirmed = 2,
}
