using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertActivityCategoryRequest
{
    [Required]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
