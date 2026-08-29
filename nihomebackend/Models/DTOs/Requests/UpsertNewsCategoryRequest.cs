using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertNewsCategoryRequest : ILegacyLocalizedCategoryRequest
{
    [Required(ErrorMessage = "Tên danh mục là bắt buộc. Ví dụ: Tin công ty.")]
    [StringLength(200, ErrorMessage = "Tên danh mục không được vượt quá 200 ký tự.")]
    public string Name { get; set; } = "";
    [StringLength(200, ErrorMessage = "Tên tiếng Việt không được vượt quá 200 ký tự.")]
    public string? NameVi { get; set; }
    [StringLength(200, ErrorMessage = "Tên tiếng Anh không được vượt quá 200 ký tự.")]
    public string? NameEn { get; set; }
    [StringLength(200, ErrorMessage = "Tên tiếng Trung không được vượt quá 200 ký tự.")]
    public string? NameZh { get; set; }
    [StringLength(200, ErrorMessage = "Tên tiếng Nhật không được vượt quá 200 ký tự.")]
    public string? NameJa { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị phải lớn hơn hoặc bằng 0. Ví dụ: 1.")]
    public int SortOrder { get; set; }
}
