using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertProjectCategoryRequest
{
    [Required] public string Name { get; set; } = "";
    public string? NameVi { get; set; }
    public string? NameEn { get; set; }
    public string? NameZh { get; set; }
    public string? NameJa { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
