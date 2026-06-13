using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertRecruitmentDropdownOptionRequest
{
    [Required]
    public string Type { get; set; } = "";

    [Required]
    public string Code { get; set; } = "";

    [Required]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
