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

    public bool IsMilestone { get; set; }

    /// <summary>Original plan dates, as imported from MS Project; both or neither.</summary>
    public DateOnly? BaselineStart { get; set; }
    public DateOnly? BaselineEnd { get; set; }

    [MaxLength(50)]
    public List<SaveProjectScheduleLinkRequest> Predecessors { get; set; } = new();
}

/// <summary>A link from another item of the same revision: FS, SS, FF or SF, with lag in days.</summary>
public sealed class SaveProjectScheduleLinkRequest
{
    public Guid PredecessorNodeId { get; set; }

    [Required, RegularExpression("^(FS|SS|FF|SF)$")]
    public string Type { get; set; } = "FS";

    [Range(-3650, 3650)]
    public int LagDays { get; set; }
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
