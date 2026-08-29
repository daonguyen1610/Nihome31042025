using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertAsBuiltDocumentCategoryRequest : IValidatableObject, ILegacyLocalizedCategoryRequest
{
    /// <summary>Internal code for programmatic references (e.g. "Drawing", "AcceptanceMinute").</summary>
    [Required(ErrorMessage = "Mã danh mục là bắt buộc. Ví dụ: Drawing.")]
    [StringLength(50, ErrorMessage = "Mã danh mục không được vượt quá 50 ký tự. Ví dụ: Drawing.")]
    public string Code { get; set; } = "";

    public string? Name { get; set; }

    /// <summary>Legacy alias for the Vietnamese source name.</summary>
    public string? NameVi { get; set; }

    /// <summary>Legacy translation fields retained for rolling-deployment compatibility.</summary>
    public string? NameEn { get; set; }
    public string? NameZh { get; set; }
    public string? NameJa { get; set; }

    /// <summary>Whether this category is required for handover completeness.</summary>
    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "Thứ tự hiển thị phải lớn hơn hoặc bằng 0. Ví dụ: 1.")]
    public int SortOrder { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var sourceName = !string.IsNullOrWhiteSpace(Name) ? Name : NameVi;
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            yield return new ValidationResult(
                "Tên danh mục là bắt buộc. Ví dụ: Bản vẽ hoàn công.",
                [nameof(Name)]);
        }
        else if (sourceName.Trim().Length > 200)
        {
            yield return new ValidationResult(
                "Tên danh mục không được vượt quá 200 ký tự.",
                [nameof(Name)]);
        }

        foreach (var (value, fieldName, label, example) in new[]
        {
            (NameEn, nameof(NameEn), "tiếng Anh", "As-built drawings"),
            (NameZh, nameof(NameZh), "tiếng Trung", "竣工图纸"),
            (NameJa, nameof(NameJa), "tiếng Nhật", "竣工図面"),
        })
        {
            if (value == null)
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                yield return new ValidationResult(
                    $"Tên {label} không được để trống. Ví dụ: {example}.",
                    [fieldName]);
            }
            else if (value.Trim().Length > 200)
            {
                yield return new ValidationResult(
                    $"Tên {label} không được vượt quá 200 ký tự.",
                    [fieldName]);
            }
        }
    }
}
