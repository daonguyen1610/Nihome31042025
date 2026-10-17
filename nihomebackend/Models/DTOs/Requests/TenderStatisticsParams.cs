using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class TenderStatisticsParams : IValidatableObject
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    [Range(1, int.MaxValue)]
    public int? CustomerId { get; set; }
    [Range(1, int.MaxValue)]
    public int? PreparerUserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate.HasValue != ToDate.HasValue)
            yield return new ValidationResult("Provide both FromDate and ToDate, for example 2026-01-01 and 2026-12-31.", [nameof(FromDate), nameof(ToDate)]);
        if (FromDate > ToDate)
            yield return new ValidationResult("ToDate must be on or after FromDate.", [nameof(ToDate)]);
        if (FromDate == DateOnly.MinValue)
            yield return new ValidationResult("FromDate must be after 0001-01-01.", [nameof(FromDate)]);
    }

    // Date boundaries are inclusive Vietnam calendar dates; UTC end is exclusive.
    public DateTime? StartUtc => FromDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
    public DateTime? EndUtc => ToDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(17);
}
