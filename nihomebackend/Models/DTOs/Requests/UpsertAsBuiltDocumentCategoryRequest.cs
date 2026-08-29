using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertAsBuiltDocumentCategoryRequest
{
    /// <summary>Internal code for programmatic references (e.g. "Drawing", "AcceptanceMinute").</summary>
    [Required(ErrorMessage = "Mã danh mục là bắt buộc. Ví dụ: Drawing.")]
    [StringLength(50, ErrorMessage = "Mã danh mục không được vượt quá 50 ký tự. Ví dụ: Drawing.")]
    public string Code { get; set; } = "";

    [Required(ErrorMessage = "Tên tiếng Việt là bắt buộc. Ví dụ: Bản vẽ hoàn công.")]
    [StringLength(200, ErrorMessage = "Tên tiếng Việt không được vượt quá 200 ký tự.")]
    public string NameVi { get; set; } = "";

    [Required(ErrorMessage = "Tên tiếng Anh là bắt buộc. Ví dụ: As-built drawings.")]
    [StringLength(200, ErrorMessage = "Tên tiếng Anh không được vượt quá 200 ký tự.")]
    public string NameEn { get; set; } = "";

    [Required(ErrorMessage = "Tên tiếng Trung là bắt buộc. Ví dụ: 竣工图纸.")]
    [StringLength(200, ErrorMessage = "Tên tiếng Trung không được vượt quá 200 ký tự.")]
    public string NameZh { get; set; } = "";

    [Required(ErrorMessage = "Tên tiếng Nhật là bắt buộc. Ví dụ: 竣工図面.")]
    [StringLength(200, ErrorMessage = "Tên tiếng Nhật không được vượt quá 200 ký tự.")]
    public string NameJa { get; set; } = "";

    /// <summary>Whether this category is required for handover completeness.</summary>
    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị phải lớn hơn hoặc bằng 0. Ví dụ: 1.")]
    public int SortOrder { get; set; }
}
