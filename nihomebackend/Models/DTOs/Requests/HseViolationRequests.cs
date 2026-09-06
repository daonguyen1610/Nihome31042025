using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class HseViolationListParams
{
    public string? Status { get; set; }
    public string? Severity { get; set; }
    [MaxLength(200)]
    public string? Search { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 200)]
    public int PageSize { get; set; } = 50;
}

public abstract class HseViolationFieldsRequest
{
    [Required, MaxLength(100)]
    public string OfflineClientId { get; set; } = string.Empty;
    [Required]
    public DateTime? OccurredAt { get; set; }
    [Required, MaxLength(300)]
    public string Location { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;
    [Required, MaxLength(30)]
    public string Severity { get; set; } = "Low";
    [Required, MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? RegulatoryReference { get; set; }
    [MaxLength(20)]
    public List<string> EvidenceDocuments { get; set; } = [];
    [Range(1, int.MaxValue)]
    public int ResponsibleSiteUserId { get; set; }
    [Range(1, int.MaxValue)]
    public int? RemediationOwnerUserId { get; set; }
    public DateTime? RemediationDeadline { get; set; }
    [MaxLength(4000)]
    public string? RemediationNote { get; set; }
    [MaxLength(200)]
    public string? PenaltyReference { get; set; }
    [Range(typeof(decimal), "0", "999999999999999.99")]
    public decimal? PenaltyAmount { get; set; }
}

public sealed class CreateHseViolationRequest : HseViolationFieldsRequest;

public sealed class UpdateHseViolationRequest : HseViolationFieldsRequest
{
    public string? RowVersion { get; set; }
}

public sealed class TransitionHseViolationRequest
{
    [MaxLength(2000)]
    public string? Reason { get; set; }
    [MaxLength(4000)]
    public string? RemediationNote { get; set; }
    [Range(1, int.MaxValue)]
    public int? RemediationOwnerUserId { get; set; }
    public DateTime? RemediationDeadline { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class CorrectHseViolationRequest : HseViolationFieldsRequest
{
    [Required, MinLength(3), MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}