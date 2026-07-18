using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Patch payload for a permit checklist item. All fields are optional so the
/// FE can send a minimal delta (e.g. "just move to Submitted"). Explicit
/// null-clearing uses the sibling <c>Clear*</c> flags to keep the "no change"
/// semantics safe: missing + null both mean "leave as is" without the flag.
/// </summary>
public class UpdatePermitChecklistItemRequest
{
    /// <summary>Enum name — NotStarted / Preparing / Submitted / UnderReview / NeedMoreDocs / Issued / Rejected / Expired.</summary>
    public string? Status { get; set; }

    [StringLength(200)]
    public string? IssuingAgency { get; set; }
    public bool ClearIssuingAgency { get; set; }

    public int? OwnerUserId { get; set; }
    public bool ClearOwner { get; set; }

    public DateTime? TargetDeadline { get; set; }
    public bool ClearTargetDeadline { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public bool ClearSubmittedAt { get; set; }

    public DateTime? IssuedAt { get; set; }
    public bool ClearIssuedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public bool ClearExpiresAt { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
    public bool ClearNote { get; set; }
}

/// <summary>
/// Query parameters for the company-wide list view. Legal / BGD use these
/// to zero in on their "rủi ro pháp lý" surface — overdue + expiring rows.
/// </summary>
public class PermitChecklistListParams
{
    public int? DesignProjectId { get; set; }
    public int? OwnerUserId { get; set; }

    /// <summary>Comma-separated <c>PermitStatus</c> values.</summary>
    public string? Status { get; set; }

    /// <summary>Comma-separated master-data codes from <c>permit_type</c>.</summary>
    public string? PermitTypeCode { get; set; }

    /// <summary>Convenience flag — <c>TargetDeadline &lt;= now + 7d</c> and status != Issued.</summary>
    public bool? DueSoon { get; set; }

    /// <summary>Convenience flag — <c>ExpiresAt &lt;= now + 30d</c> for Issued permits.</summary>
    public bool? ExpiringSoon { get; set; }

    /// <summary>Convenience flag — <c>TargetDeadline &lt; now</c> and status != Issued.</summary>
    public bool? Overdue { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
