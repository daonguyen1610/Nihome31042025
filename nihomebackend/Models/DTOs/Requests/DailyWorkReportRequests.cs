using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class SubmitDailyWorkReportRequest : IConcurrencyRequest
{
    [MaxLength(50)]
    public List<DailyWorkReportItemRequest> Items { get; set; } = new();

    [StringLength(4000)]
    public string? Proposals { get; set; }

    [Required, StringLength(4000, MinimumLength = 2)]
    public string TomorrowPlan { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}

public sealed class DailyWorkReportItemRequest
{
    [StringLength(40)]
    public string? SourceType { get; set; }

    [Range(1, int.MaxValue)]
    public int? SourceId { get; set; }

    [Required, StringLength(300, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Range(0, 100)]
    public int ProgressPercent { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public sealed class ReviewDailyWorkReportRequest : IConcurrencyRequest
{
    [Required, StringLength(4000, MinimumLength = 2)]
    public string Comment { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}
