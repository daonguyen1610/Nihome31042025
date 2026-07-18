namespace NihomeBackend.Models;

/// <summary>
/// M3 Permitting checklist item (NIH-137). One row per <see cref="DesignProject"/>
/// × permit type (GPXD, PCCC, electricity, water, sidewalk, safety, environment,
/// completion — see master data category <c>permit_type</c>). The set is auto-
/// generated from the master-data template on <see cref="DesignProject"/>
/// creation and can later be edited by Legal Officer / PM.
/// </summary>
public class PermitChecklistItem
{
    public int Id { get; set; }

    /// <summary>Owning design project — hard FK.</summary>
    public int DesignProjectId { get; set; }
    public DesignProject DesignProject { get; set; } = null!;

    /// <summary>Master-data code from category <c>permit_type</c>.</summary>
    public string PermitTypeCode { get; set; } = string.Empty;

    /// <summary>Issuing authority (free text — Sở Xây dựng, Cảnh sát PCCC …).</summary>
    public string? IssuingAgency { get; set; }

    /// <summary>Legal officer / PM in charge of pushing this item.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Internal target deadline for having the permit issued.</summary>
    public DateTime? TargetDeadline { get; set; }

    /// <summary>Date the submission package was handed to the authority.</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Date the permit was issued.</summary>
    public DateTime? IssuedAt { get; set; }

    /// <summary>Expiry date on permits that have one (PCCC, safety, …).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Host-relative path to the submitted package (uploaded via NIH-137 slice 2).</summary>
    public string? SubmittedFilePath { get; set; }

    /// <summary>Host-relative path to the issued permit scan (uploaded via NIH-137 slice 2).</summary>
    public string? IssuedFilePath { get; set; }

    public PermitStatus Status { get; set; } = PermitStatus.NotStarted;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

/// <summary>
/// Lifecycle of a <see cref="PermitChecklistItem"/>. Order mirrors the
/// master-data <c>permit_status</c> catalogue so the FE can render a
/// consistent status badge across surfaces.
/// </summary>
public enum PermitStatus
{
    NotStarted = 0,
    Preparing = 1,
    Submitted = 2,
    UnderReview = 3,
    NeedMoreDocs = 4,
    Issued = 5,
    Rejected = 6,
    Expired = 7,
}
