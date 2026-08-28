namespace NihomeBackend.Models;

/// <summary>
/// M4 Construction & Acceptance — as-built dossier document
/// (Hồ sơ Hoàn công / NIH-145).
///
/// One row = one document in the as-built dossier of a
/// <see cref="DesignProject"/>. Handover requires the dossier to
/// have every required <see cref="Category"/> present and Approved,
/// so the list page rolls up completeness per project.
///
/// Lifecycle Draft → Submitted → Approved → Archived (final).
/// Draft rows can be edited/deleted; anything else is locked.
/// </summary>
public class AsBuiltDocument
{
    public int Id { get; set; }

    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Human-friendly code, unique inside a project (e.g. <c>AB-001</c>).</summary>
    public string DocumentCode { get; set; } = string.Empty;

    /// <summary>Display title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Free-text body / description.</summary>
    public string? Description { get; set; }

    /// <summary>Category FK — used to compute dossier completeness.</summary>
    public int CategoryId { get; set; }
    public AsBuiltDocumentCategory Category { get; set; } = null!;

    /// <summary>Optional relative URL of the attached file (PDF / DWG / photo).</summary>
    public string? FileUrl { get; set; }

    public AsBuiltStatus Status { get; set; } = AsBuiltStatus.Draft;

    /// <summary>Free-text note on approval / rejection.</summary>
    public string? Note { get; set; }

    // --- Workflow audit ---
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }

    public DateTime? ArchivedAt { get; set; }

    // --- Standard audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>Lifecycle of an <see cref="AsBuiltDocument"/>.</summary>
public enum AsBuiltStatus
{
    /// <summary>Being drafted — editable + deletable.</summary>
    Draft = 0,
    /// <summary>Submitted for QA / handover approval.</summary>
    Submitted = 1,
    /// <summary>Approved — counts toward dossier completeness.</summary>
    Approved = 2,
    /// <summary>Archived after handover — immutable.</summary>
    Archived = 3,
    /// <summary>Cancelled — soft-removed from the dossier.</summary>
    Cancelled = 4,
}

/// <summary>
/// Well-known category codes for programmatic access.
/// These codes must match the seeded <see cref="AsBuiltDocumentCategory.Code"/> values.
/// </summary>
public static class AsBuiltCategoryCodes
{
    public const string Drawing = "Drawing";
    public const string AcceptanceMinute = "AcceptanceMinute";
    public const string TestReport = "TestReport";
    public const string WarrantyCertificate = "WarrantyCertificate";
    public const string Other = "Other";

    /// <summary>Required category codes for handover completeness.</summary>
    public static readonly string[] Required =
    {
        Drawing,
        AcceptanceMinute,
        TestReport,
        WarrantyCertificate,
    };
}
