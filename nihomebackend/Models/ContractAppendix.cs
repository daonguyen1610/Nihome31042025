namespace NihomeBackend.Models;

/// <summary>
/// Variation Order (Phụ lục hợp đồng) — a signed amendment that adjusts the
/// parent contract's value up or down with a business reason.
///
/// Workflow (NIH-104): <c>Draft → Submitted → Approved | Rejected</c>.
/// Only <see cref="ContractAppendixStatus.Approved"/> rows count toward the
/// contract's <c>CurrentValue</c>.
/// </summary>
public class ContractAppendix
{
    public int Id { get; set; }

    /// <summary>Owning contract. Cascade-delete when the parent is removed.</summary>
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    /// <summary>Sequential VO number within the contract (1-based, unique).</summary>
    public int VoNumber { get; set; }

    /// <summary>Short label shown in tables (e.g. "Thay đổi vật liệu ốp lát").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Business reason for the amendment. Free-text, required.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Value delta in VND. Positive = increase, negative = discount. Applied
    /// to the parent contract's <c>Value</c> only when <see cref="Status"/>
    /// is <see cref="ContractAppendixStatus.Approved"/>.
    /// </summary>
    public decimal ValueDelta { get; set; }

    /// <summary>Optional attached document (uploaded file, host-relative path).</summary>
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }

    public ContractAppendixStatus Status { get; set; } = ContractAppendixStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }

    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public ApplicationUser? DecidedBy { get; set; }

    /// <summary>Rejection reason recorded by the reviewer.</summary>
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public enum ContractAppendixStatus
{
    /// <summary>Editable draft. Not yet counted toward CurrentValue.</summary>
    Draft = 0,
    /// <summary>Submitted for review. Read-only for the submitter.</summary>
    Submitted = 1,
    /// <summary>Approved by a manager. Counted toward CurrentValue.</summary>
    Approved = 2,
    /// <summary>Rejected. Not counted toward CurrentValue.</summary>
    Rejected = 3,
}
