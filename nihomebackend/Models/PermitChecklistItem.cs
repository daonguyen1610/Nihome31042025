namespace NihomeBackend.Models;

/// <summary>
/// M3 permitting record. Operational Project is the business owner; the
/// Design Project relationship is retained only as a compatibility bridge.
/// </summary>
public class PermitChecklistItem
{
    public int Id { get; set; }

    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;

    public int? DesignProjectId { get; set; }
    public DesignProject? DesignProject { get; set; }

    public int? WorkTaskId { get; set; }
    public DepartmentWorkTask? WorkTask { get; set; }

    /// <summary>Master-data code from category <c>permit_type</c>.</summary>
    public string PermitTypeCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }

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
    public List<PermitChecklistHistory> History { get; set; } = new();
}

public class PermitChecklistHistory
{
    public long Id { get; set; }
    public int PermitChecklistItemId { get; set; }
    public PermitChecklistItem PermitChecklistItem { get; set; } = null!;
    public PermitStatus? FromStatus { get; set; }
    public PermitStatus ToStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedByUser { get; set; } = null!;
}

/// <summary>
/// Idempotency and audit record for the manual contract activation wizard.
/// A project can have several activating contracts while sharing one legal
/// work package.
/// </summary>
public class ContractLegalActivation
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    public string ConfigurationJson { get; set; } = string.Empty;
    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public int ActivatedByUserId { get; set; }
    public ApplicationUser ActivatedByUser { get; set; } = null!;
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
