using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class ConfirmDeletionRequest
{
    [Required(ErrorMessage = "Mã kế hoạch xoá là bắt buộc; ví dụ: mã nhận được từ API xem trước ảnh hưởng xoá.")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "Mã kế hoạch xoá phải có đúng 64 ký tự; ví dụ: mã nhận được từ API xem trước ảnh hưởng xoá.")]
    public string PlanToken { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã xác nhận xoá là bắt buộc; ví dụ: nhập đúng mã tài nguyên hiển thị trong hộp thoại xác nhận.")]
    [StringLength(200, ErrorMessage = "Mã xác nhận xoá không được dài quá 200 ký tự; ví dụ: nhập đúng mã tài nguyên hiển thị trong hộp thoại xác nhận.")]
    public string Confirmation { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Phiên bản dữ liệu không được dài quá 200 ký tự; ví dụ: dùng RowVersion mới nhất từ API chi tiết.")]
    public string? RowVersion { get; set; }
}
