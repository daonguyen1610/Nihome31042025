using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>
/// Payload used by both create and update of a
/// <see cref="MasterDataOption"/>. The category is taken from the route,
/// not from the body, so it cannot be silently rewritten by clients.
/// </summary>
public class UpsertMasterDataOptionRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? LabelKey { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
