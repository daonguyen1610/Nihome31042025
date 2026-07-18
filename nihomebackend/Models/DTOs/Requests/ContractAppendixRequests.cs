using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create or update a Variation Order draft. Once submitted the
/// only allowed edits go through the approve/reject endpoints.</summary>
public class UpsertContractAppendixRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Positive = increase, negative = discount. Zero rejected.</summary>
    [Range(-1_000_000_000_000d, 1_000_000_000_000d)]
    public decimal ValueDelta { get; set; }

    /// <summary>Optional attachment metadata; upload first via
    /// <c>POST /api/contracts/{id}/appendices/upload</c>.</summary>
    [StringLength(500)]
    public string? FilePath { get; set; }
    [StringLength(300)]
    public string? OriginalFileName { get; set; }
    public long? FileSize { get; set; }
    [StringLength(150)]
    public string? ContentType { get; set; }
}

/// <summary>Reviewer's rejection / approval note. Approve accepts an empty
/// body; Reject requires a reason.</summary>
public class ContractAppendixDecisionRequest
{
    [StringLength(1000)]
    public string? Note { get; set; }
}
