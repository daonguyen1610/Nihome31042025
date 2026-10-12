namespace NihomeBackend.Models.DTOs.Responses;

public class TenderAssigneeOptionResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

public class TenderChecklistItemResponse
{
    public int Id { get; set; }
    public string? TemplateCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? NotApplicableReason { get; set; }
    public string DocumentKind { get; set; } = string.Empty;
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? InternalDeadline { get; set; }
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public int? CapabilityDocumentId { get; set; }
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedByUserId { get; set; }
    public int? WorkTaskId { get; set; }
    public int? CurrentEvidenceId { get; set; }
    public List<TenderChecklistEvidenceResponse> EvidenceRevisions { get; set; } = new();
}

public class TenderChecklistEvidenceResponse
{
    public int Id { get; set; }
    public int Revision { get; set; }
    public string Source { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public int? CapabilityDocumentId { get; set; }
    public int? CapabilityDocumentVersion { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenderSubmissionResponse
{
    public int Id { get; set; }
    public int Sequence { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }
    public int? ApprovedEstimateRevisionId { get; set; }
    public int? FinanceReviewWorkPackageId { get; set; }
    public string SubmissionChannel { get; set; } = string.Empty;
    public string? ReceiptReference { get; set; }
    public bool LegacyUnknown { get; set; }
    public int ItemCount { get; set; }
    public List<TenderSubmissionItemResponse> Items { get; set; } = new();
}

public class TenderSubmissionItemResponse
{
    public int ChecklistItemId { get; set; }
    public string? TemplateCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public int? CapabilityDocumentId { get; set; }
    public int? EvidenceId { get; set; }
    public int? CapabilityDocumentVersion { get; set; }
    public string? Sha256 { get; set; }
    public string? NotApplicableReason { get; set; }
}

public class TenderResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? OperationalProjectId { get; set; }
    public string? OperationalProjectCode { get; set; }
    public string? OperationalProjectName { get; set; }

    public DateTime? OpeningDate { get; set; }
    public DateTime? InvitationReceivedAt { get; set; }
    public string? InvitationReference { get; set; }
    public string InvitationSource { get; set; } = string.Empty;
    public int InvitationReceivedByUserId { get; set; }
    public string? InvitationReceivedByName { get; set; }
    public string? InvitationPortalUrl { get; set; }
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }
    public string? PreparerName { get; set; }

    public string? InfoSource { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }

    public int? WonOpportunityId { get; set; }
    /// <summary>Resolved name of the linked opportunity, so the FE Result tab can render "Cơ hội đã gán" without an extra lookup.</summary>
    public string? WonOpportunityName { get; set; }
    public string? LostReasonCode { get; set; }
    /// <summary>Localised label for <see cref="LostReasonCode"/> from master-data category <c>opportunity_lost_reason</c>.</summary>
    public string? LostReasonLabel { get; set; }
    public string? LostNote { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;

    public List<TenderChecklistItemResponse> ChecklistItems { get; set; } = new();
    public List<TenderSubmissionResponse> Submissions { get; set; } = new();
    public List<TenderInvitationDocumentResponse> InvitationDocuments { get; set; } = new();

    /// <summary>Percent of checklist items in a completed state (Done/Submitted).</summary>
    public int ChecklistCompletionPercent { get; set; }

    /// <summary>True when SubmissionDeadline - now &lt;= 3 days and status is non-terminal.</summary>
    public bool IsDeadlineImminent { get; set; }
    public List<TenderMilestoneResponse> Milestones { get; set; } = new();
}

public class TenderInvitationDocumentResponse
{
    public int Id { get; set; }
    public string? OriginalFileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public string? ExternalUrl { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenderMilestoneResponse
{
    public string Kind { get; set; } = string.Empty;
    public DateTime AchievedAt { get; set; }
    public int AchievedByUserId { get; set; }
}

public class TenderReadinessResponse
{
    public int TenderId { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public bool CanSubmit { get; set; }
    public List<TenderReadinessBlockerResponse> Blockers { get; set; } = new();
    public List<TenderMilestoneResponse> Milestones { get; set; } = new();
}

public class TenderReadinessBlockerResponse
{
    public string Code { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActionPath { get; set; } = string.Empty;
}

public class TenderListItemResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public DateTime? OpeningDate { get; set; }
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }
    public string? PreparerName { get; set; }

    public string Status { get; set; } = string.Empty;
    public int ChecklistCompletionPercent { get; set; }
    public bool IsDeadlineImminent { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class TenderListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<TenderListItemResponse> Items { get; set; } = new();
}

/// <summary>
/// Timeline event for the Tender detail page (NIH-97 History tab). Sourced
/// from the audit log stream filtered by <c>resourceType == "Tender"</c> and
/// the row id.
/// </summary>
public class TenderTimelineEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
}
