using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ProjectReportQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int? ProjectId { get; set; }

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult(
                "From must be on or before To.",
                [nameof(From), nameof(To)]);
        }
    }
}

public sealed class ProjectReportExportQuery : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int? ProjectId { get; set; }

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Required]
    [RegularExpression("^(xlsx|pdf)$", ErrorMessage = "Format must be xlsx or pdf.")]
    public string Format { get; set; } = "xlsx";

    [Required]
    [RegularExpression("^(vi|en|zh|ja)$", ErrorMessage = "Language must be vi, en, zh, or ja.")]
    public string Language { get; set; } = "vi";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult(
                "From must be on or before To.",
                [nameof(From), nameof(To)]);
        }
    }

    public ProjectReportQuery ToReportQuery() => new()
    {
        ProjectId = ProjectId,
        From = From,
        To = To,
    };
}
