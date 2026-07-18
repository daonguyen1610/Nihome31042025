namespace NihomeBackend.Models;

/// <summary>
/// CRM Survey (Phiếu khảo sát) — a field visit capturing site information
/// against a project or opportunity. Media (photos, sketches, docs) live on
/// Google Drive; the row keeps the sync-status so the FE can surface a
/// per-row badge. Sits alongside <see cref="Tender"/> in the M1 CRM funnel.
/// </summary>
public class Survey
{
    public int Id { get; set; }

    /// <summary>Human-friendly code (e.g. SV-2026-0001), unique, auto-generated on create.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Free-text address / site description.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Master-data code from category <c>construction_type</c>.</summary>
    public string? ConstructionTypeCode { get; set; }

    /// <summary>Date the site visit is scheduled / took place.</summary>
    public DateTime SurveyDate { get; set; }

    /// <summary>User who performs / performed the survey. Nullable while unassigned.</summary>
    public int? SurveyorUserId { get; set; }
    public ApplicationUser? Surveyor { get; set; }

    /// <summary>Linked <see cref="Project"/> (nullable — survey may pre-date the project row).</summary>
    public int? LinkedProjectId { get; set; }
    public Project? LinkedProject { get; set; }

    /// <summary>Linked <see cref="Opportunity"/> (nullable — some surveys are pre-lead).</summary>
    public int? LinkedOpportunityId { get; set; }
    public Opportunity? LinkedOpportunity { get; set; }

    /// <summary>Free-text working note.</summary>
    public string? Note { get; set; }

    // --- Drive sync (per NIH-99 AC) ---

    public SurveyDriveSyncStatus DriveSyncStatus { get; set; } = SurveyDriveSyncStatus.NotSynced;

    /// <summary>Failure reason when <see cref="DriveSyncStatus"/> is Failed — shown as tooltip on the FE badge.</summary>
    public string? DriveSyncError { get; set; }

    /// <summary>Timestamp of the last successful/failed sync attempt.</summary>
    public DateTime? LastSyncedAt { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Drive-sync lifecycle of a <see cref="Survey"/>. The FE renders the value
/// as a coloured badge on the list (green / orange / red / neutral).
/// </summary>
public enum SurveyDriveSyncStatus
{
    NotSynced = 0,
    Syncing = 1,
    Synced = 2,
    Failed = 3,
}
