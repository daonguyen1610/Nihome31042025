using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertRecruitmentMetadataItemRequest
{
    [Required]
    public string GroupKey { get; set; } = "";

    [Required]
    public string Value { get; set; } = "";

    [Required]
    public string Label { get; set; } = "";

    public string? TranslationKey { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
