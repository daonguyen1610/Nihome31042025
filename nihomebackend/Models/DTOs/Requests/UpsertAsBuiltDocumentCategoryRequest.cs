using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertAsBuiltDocumentCategoryRequest
{
    /// <summary>Internal code for programmatic references (e.g. "Drawing", "AcceptanceMinute").</summary>
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = "";

    [Required]
    public string Name { get; set; } = "";

    public string? NameVi { get; set; }
    public string? NameEn { get; set; }
    public string? NameZh { get; set; }
    public string? NameJa { get; set; }

    /// <summary>Whether this category is required for handover completeness.</summary>
    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
