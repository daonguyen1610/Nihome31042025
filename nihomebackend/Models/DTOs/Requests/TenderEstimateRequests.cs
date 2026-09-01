using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class DecideTenderEstimateRequest
{
    [StringLength(2000)]
    public string? Note { get; set; }
}

public class TransitionTenderRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public int? OpportunityId { get; set; }

    [StringLength(80)]
    public string? ReasonCode { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}
