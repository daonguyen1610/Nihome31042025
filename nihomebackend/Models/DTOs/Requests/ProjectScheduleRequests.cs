using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class SaveProjectScheduleRevisionRequest
{
    [MaxLength(2000)]
    public string? ChangeReason { get; set; }

    [Required, MinLength(1), MaxLength(500)]
    public List<SaveProjectScheduleItemRequest> Items { get; set; } = new();

    [StringLength(64)]
    public string? RowVersion { get; set; }
}

public sealed class SaveProjectScheduleItemRequest
{
    public Guid? NodeId { get; set; }
    public Guid? ParentNodeId { get; set; }

    [Required, StringLength(80, MinimumLength = 1)]
    public string WbsCode { get; set; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateOnly? PlannedStart { get; set; }

    [Required]
    public DateOnly? PlannedEnd { get; set; }

    public int SortOrder { get; set; }
}

public sealed class SubmitProjectScheduleRevisionRequest
{
    [Range(1, int.MaxValue)]
    public int ApproverUserId { get; set; }

    [Required]
    public string? RowVersion { get; set; }

    [StringLength(16000)]
    public string? PreviewToken { get; set; }
}

public sealed class ReviewProjectScheduleRevisionRequest
{
    [Required, RegularExpression("^(Approve|Reject)$")]
    public string Decision { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Comment { get; set; }

    [Required]
    public string? RowVersion { get; set; }

    [StringLength(16000)]
    public string? PreviewToken { get; set; }
}

public sealed class ResolveProjectScheduleEmailRequest
{
    [Required, RegularExpression("^(ConfirmReceived|Retry)$")]
    public string Decision { get; set; } = string.Empty;
}
