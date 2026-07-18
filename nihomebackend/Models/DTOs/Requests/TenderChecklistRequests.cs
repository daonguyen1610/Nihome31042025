using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Inline-edit payload for a single checklist row (status, owner, deadline).
/// Fields are optional — only supplied fields are applied so the FE can send
/// one at a time (dropdown, avatar picker, date field). Everything null leaves
/// the row unchanged.
/// </summary>
public class UpdateTenderChecklistItemRequest
{
    /// <summary>New status name: NotStarted / Preparing / Done / Submitted.</summary>
    public string? Status { get; set; }

    /// <summary>New owner user id, or <c>null</c> to unassign (only when explicitly cleared).</summary>
    public int? OwnerUserId { get; set; }

    /// <summary>Whether <see cref="OwnerUserId"/> was explicitly supplied by the caller (nullable → clear).</summary>
    public bool ClearOwner { get; set; }

    /// <summary>New internal deadline, or <c>null</c> when <see cref="ClearInternalDeadline"/> is set.</summary>
    public DateTime? InternalDeadline { get; set; }

    public bool ClearInternalDeadline { get; set; }
}

/// <summary>
/// Bulk-attach payload: copies file metadata from one or more
/// <c>capability_documents</c> rows into the target checklist items so the
/// tender picks up the library's current file without duplicating the
/// physical binary. Each entry pairs the checklist row with the source
/// library document.
/// </summary>
public class AttachTenderChecklistFromLibraryRequest
{
    [Required]
    public List<AttachTenderChecklistFromLibraryItem> Items { get; set; } = new();
}

public class AttachTenderChecklistFromLibraryItem
{
    [Required]
    public int ChecklistItemId { get; set; }

    [Required]
    public int CapabilityDocumentId { get; set; }
}
