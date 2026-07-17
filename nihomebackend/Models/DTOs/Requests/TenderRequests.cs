using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateTenderRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int CustomerId { get; set; }

    public DateTime? OpeningDate { get; set; }

    /// <summary>Submission deadline — must be strictly &gt; now on create.</summary>
    [Required]
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }

    [StringLength(200)]
    public string? InfoSource { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>
/// Rules per NIH-96: only Deadline / Preparer / Note may change while
/// Status = Preparing; other statuses accept Note only. The service
/// enforces those two lanes based on the tender's current status.
/// </summary>
public class UpdateTenderRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public DateTime? OpeningDate { get; set; }

    [Required]
    public DateTime SubmissionDeadline { get; set; }

    public int? PreparerUserId { get; set; }

    [StringLength(200)]
    public string? InfoSource { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class TenderListParams
{
    /// <summary>Multi-select status filter — comma-separated names, e.g. "Preparing,Submitted".</summary>
    public string? Status { get; set; }
    public int? CustomerId { get; set; }
    public int? PreparerUserId { get; set; }

    /// <summary>Filter by month of <c>OpeningDate</c> — 1-12.</summary>
    public int? OpeningMonth { get; set; }
    public int? OpeningYear { get; set; }

    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
