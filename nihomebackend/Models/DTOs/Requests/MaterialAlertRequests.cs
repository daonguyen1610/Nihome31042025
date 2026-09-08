using System.ComponentModel.DataAnnotations;
using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class MaterialAlertListParams
{
    [MaxLength(200)]
    public string? Search { get; set; }
    public MaterialAlertType? Type { get; set; }
    public MaterialAlertStatus? Status { get; set; }
    public MaterialAlertSeverity? Severity { get; set; }
    [Range(1, int.MaxValue)]
    public int? AssignedToUserId { get; set; }
    [RegularExpression("^(detectedAt|updatedAt|severity|variance|itemCode)$")]
    public string SortBy { get; set; } = "detectedAt";
    [RegularExpression("^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class AcknowledgeMaterialAlertRequest : IConcurrencyRequest
{
    [Required, MinLength(3), MaxLength(2000)]
    public string Note { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}
