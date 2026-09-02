using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class UpsertMaterialRateCatalogRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại danh mục đơn giá.")]
    [EnumDataType(typeof(MaterialRateCatalogType), ErrorMessage = "Loại danh mục đơn giá không hợp lệ. Chỉ chấp nhận InvestmentRate hoặc Boq.")]
    public MaterialRateCatalogType CatalogType { get; set; } = MaterialRateCatalogType.InvestmentRate;

    [Required(ErrorMessage = "Vui lòng nhập mã danh mục đơn giá.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Mã danh mục đơn giá phải có từ 1 đến 50 ký tự.")]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]*$", ErrorMessage = "Mã danh mục đơn giá chỉ gồm chữ, số, dấu chấm, gạch ngang hoặc gạch dưới, ví dụ: NOI-THAT-2026.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục đơn giá.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Tên danh mục đơn giá phải có từ 1 đến 200 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Mô tả danh mục đơn giá không được vượt quá 1000 ký tự.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã tiền tệ, ví dụ: VND.")]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "Mã tiền tệ phải gồm đúng 3 chữ in hoa, ví dụ: VND.")]
    public string Currency { get; set; } = "VND";

    public bool IsActive { get; set; } = true;
}

public sealed class CreateMaterialRateRevisionRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú phiên bản không được vượt quá 1000 ký tự.")]
    public string? Note { get; set; }
}

public sealed class DecideMaterialRateRevisionRequest
{
    [StringLength(1000, ErrorMessage = "Ghi chú quyết định không được vượt quá 1000 ký tự.")]
    public string? Note { get; set; }
}

public sealed class UpsertBoqMaterialRateLineRequest
{
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? Unit { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Quantity { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? UnitPrice { get; set; }
}
