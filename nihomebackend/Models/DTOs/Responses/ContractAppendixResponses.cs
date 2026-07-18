using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public class ContractAppendixResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int VoNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal ValueDelta { get; set; }

    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }

    public ContractAppendixStatus Status { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public string? SubmittedByName { get; set; }

    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ContractAttachmentResponse
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public ContractAttachmentKind Kind { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
}

/// <summary>
/// Timeline event for the Contract detail page. Sourced from the audit log
/// stream filtered by <c>resourceType == "Contract"</c> and the row id.
/// </summary>
public class ContractTimelineEvent
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
}
