using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/material-rate-catalogs")]
[Route("api/v1/material-rate-catalogs")]
[Authorize]
public sealed class MaterialRateCatalogsController(
    IMaterialRateService service,
    IMaterialRateSpreadsheetService spreadsheetService,
    TranslationService translations,
    IAuditLogger audit) : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["vi", "en", "zh", "ja"];

    [HttpGet]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<List<MaterialRateCatalogResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] MaterialRateCatalogType? catalogType = null,
        CancellationToken ct = default) => Ok(await service.ListCatalogsAsync(search, includeInactive, catalogType, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Get(int id, CancellationToken ct)
    {
        var result = await service.GetCatalogAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Create(
        [FromBody] UpsertMaterialRateCatalogRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateCatalogResponse>(async () =>
        {
            var result = await service.CreateCatalogAsync(request, userId.Value, ct);
            Audit("material-rate-catalog.create", result.Id, result);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        });
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Update(
        int id,
        [FromBody] UpsertMaterialRateCatalogRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateCatalogResponse>(async () =>
        {
            var result = await service.UpdateCatalogAsync(id, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-catalog.update", id, result);
            return Ok(result);
        });
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateCatalogResponse>> Delete(int id, CancellationToken ct)
    {
        try
        {
            var result = await service.DeleteCatalogAsync(id, ct);
            if (result is null) return NotFound();
            Audit("material-rate-catalog.delete", id, result);
            return NoContent();
        }
        catch (MaterialRateOperationException exception)
        {
            return Conflict(new { message = exception.Message, messageKey = exception.MessageKey });
        }
    }

    [HttpGet("csv-template")]
    [RequirePermission("crm.material-rates", "view")]
    public IActionResult DownloadTemplate([FromQuery] MaterialRateCatalogType catalogType = MaterialRateCatalogType.InvestmentRate)
    {
        var body = CreateTemplateCsv(catalogType);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(body)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", catalogType == MaterialRateCatalogType.Boq
            ? "boq-rate-template.csv"
            : "material-rate-template.csv");
    }

    [HttpGet("excel-template")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<IActionResult> DownloadExcelTemplate(
        [FromQuery] string language = "vi",
        [FromQuery] MaterialRateCatalogType catalogType = MaterialRateCatalogType.InvestmentRate)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        if (normalizedLanguage is null) return InvalidLanguage();

        var text = await translations.GetTranslationMapAsync(normalizedLanguage);
        return File(
            spreadsheetService.CreateTemplate(text, catalogType),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            GetExcelFileName(normalizedLanguage, catalogType));
    }

    [HttpGet("template-package")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<IActionResult> DownloadTemplatePackage(
        [FromQuery] string language = "vi",
        [FromQuery] MaterialRateCatalogType catalogType = MaterialRateCatalogType.InvestmentRate,
        CancellationToken ct = default)
    {
        _ = ct;
        var normalizedLanguage = NormalizeLanguage(language);
        if (normalizedLanguage is null) return InvalidLanguage();

        var text = await translations.GetTranslationMapAsync(normalizedLanguage);
        string T(string key, string fallback) => text.GetValueOrDefault(key, fallback);
        var guide = catalogType == MaterialRateCatalogType.Boq
            ? string.Join("\r\n",
            [
                T("materialRates.package.boq.title", "HƯỚNG DẪN NHẬP ĐƠN GIÁ BOQ"),
                new string('=', 68),
                "",
                T("materialRates.package.boq.purpose", "Mục đích: tạo danh mục hạng mục, khối lượng và đơn giá để áp dụng vào Bảng khối lượng Báo giá."),
                "",
                T("materialRates.package.stepsTitle", "CÁC BƯỚC THỰC HIỆN"),
                T("materialRates.package.step1", "1. Mở biểu mẫu bằng Excel, Numbers hoặc Google Sheets."),
                T("materialRates.package.boq.step2", "2. Nhập mỗi hạng mục trên một dòng; không sửa tên hoặc thứ tự cột."),
                T("materialRates.package.step3", "3. Dùng số không có dấu phân cách hàng nghìn; cột Thành tiền được tự động tính."),
                T("materialRates.package.step4", "4. Lưu nguyên định dạng Excel và gửi lại tệp cho NICON để nhập dữ liệu."),
                "",
                T("materialRates.package.columnsTitle", "GIẢI THÍCH CÁC CỘT"),
                T("materialRates.package.boq.columnCode", "ItemCode: mã hạng mục duy nhất trong file, tối đa 60 ký tự."),
                T("materialRates.package.boq.columnName", "ItemName: tên hoặc mô tả hạng mục, tối đa 300 ký tự."),
                T("materialRates.package.columnUnit", "Unit: đơn vị tính, tối đa 30 ký tự. Ví dụ: kg, m3, m2."),
                T("materialRates.package.boq.columnQuantity", "Quantity: khối lượng của hạng mục; phải lớn hơn 0, tối đa 4 số lẻ."),
                T("materialRates.package.boq.columnPrice", "UnitPrice: đơn giá của một đơn vị; không âm, tối đa 2 số lẻ."),
                "",
                T("materialRates.package.formulaTitle", "CÔNG THỨC"),
                T("materialRates.package.boq.formula", "Thành tiền = Khối lượng × Đơn giá. Tổng giá trị BOQ là tổng của tất cả các dòng."),
                "",
                T("materialRates.package.importantTitle", "LƯU Ý QUAN TRỌNG"),
                T("materialRates.package.important1", "- File nhập sẽ thay thế toàn bộ các dòng đang có trong phiên bản Nháp được chọn."),
                T("materialRates.package.important2", "- Nếu bất kỳ dòng nào sai, hệ thống không lưu dòng nào và trả về vị trí cần sửa."),
                T("materialRates.package.boq.important3", "- Chỉ phiên bản đã duyệt, còn hiệu lực và thuộc danh mục đang hoạt động mới có thể áp dụng vào Báo giá BOQ."),
                T("materialRates.package.support", "Cần hỗ trợ: gửi lại file gốc cùng ảnh chụp lỗi cho người phụ trách NICON."),
                "",
            ])
            : string.Join("\r\n",
        [
            T("materialRates.package.title", "HƯỚNG DẪN NHẬP ĐỊNH MỨC VÀ ĐƠN GIÁ VẬT LIỆU"),
            new string('=', 68),
            "",
            T("materialRates.package.purpose", "Mục đích: điền biểu mẫu Excel để NICON tính đơn giá xây dựng trên mỗi m²."),
            "",
            T("materialRates.package.stepsTitle", "CÁC BƯỚC THỰC HIỆN"),
            T("materialRates.package.step1", "1. Mở biểu mẫu bằng Excel, Numbers hoặc Google Sheets."),
            T("materialRates.package.step2", "2. Nhập mỗi vật liệu trên một dòng tại trang Nhập liệu; không sửa tên hoặc thứ tự cột."),
            T("materialRates.package.step3", "3. Dùng số không có dấu phân cách hàng nghìn; cột Thành tiền được tự động tính."),
            T("materialRates.package.step4", "4. Lưu nguyên định dạng Excel và gửi lại tệp cho NICON để nhập dữ liệu."),
            "",
            T("materialRates.package.columnsTitle", "GIẢI THÍCH CÁC CỘT"),
            T("materialRates.package.columnCode", "MaterialCode: mã vật liệu duy nhất trong file, tối đa 50 ký tự. Ví dụ: VL-XM-PC40."),
            T("materialRates.package.columnName", "MaterialName: tên vật liệu, tối đa 200 ký tự."),
            T("materialRates.package.columnUnit", "Unit: đơn vị tính, tối đa 30 ký tự. Ví dụ: kg, m3, m2."),
            T("materialRates.package.columnNorm", "NormPerSqm: lượng vật liệu cần cho 1 m² xây dựng; phải lớn hơn 0, tối đa 6 số lẻ."),
            T("materialRates.package.columnRate", "UnitRate: giá của 1 đơn vị vật liệu; không âm, tối đa 4 số lẻ."),
            T("materialRates.package.columnWaste", "WastePercent: tỷ lệ hao hụt từ 0 đến 100; tối đa 4 số lẻ."),
            "",
            T("materialRates.package.formulaTitle", "CÔNG THỨC"),
            T("materialRates.package.formula", "Thành tiền/m² = Định mức/m² × Đơn giá × (1 + Hao hụt/100). Tổng đơn giá/m² là tổng của tất cả các dòng."),
            "",
            T("materialRates.package.importantTitle", "LƯU Ý QUAN TRỌNG"),
            T("materialRates.package.important1", "- File nhập sẽ thay thế toàn bộ các dòng đang có trong phiên bản Nháp được chọn."),
            T("materialRates.package.important2", "- Nếu bất kỳ dòng nào sai, hệ thống không lưu dòng nào và trả về vị trí cần sửa."),
            T("materialRates.package.important3", "- Chỉ phiên bản đã được phê duyệt, còn hiệu lực và thuộc danh mục đang hoạt động mới xuất hiện trong Báo giá suất đầu tư."),
            T("materialRates.package.support", "Cần hỗ trợ: gửi lại file gốc cùng ảnh chụp lỗi cho người phụ trách NICON."),
            "",
        ]);

        await using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var workbookEntry = archive.CreateEntry(GetExcelFileName(normalizedLanguage, catalogType), CompressionLevel.Optimal);
            using (var workbookStream = workbookEntry.Open())
            {
                workbookStream.Write(spreadsheetService.CreateTemplate(text, catalogType));
            }
            WriteUtf8Entry(archive, "README.txt", guide, includeBom: true);
        }

        return File(buffer.ToArray(), "application/zip", catalogType == MaterialRateCatalogType.Boq
            ? "nicon-boq-rate-form.zip"
            : "nicon-material-rate-form.zip");
    }

    [HttpGet("{catalogId:int}/revisions")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<List<MaterialRateRevisionResponse>>> ListRevisions(int catalogId, CancellationToken ct)
    {
        var result = await service.ListRevisionsAsync(catalogId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{catalogId:int}/revisions/{revisionId:int}")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> GetRevision(
        int catalogId,
        int revisionId,
        CancellationToken ct)
    {
        var result = await service.GetRevisionAsync(catalogId, revisionId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{catalogId:int}/revisions")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> CreateRevision(
        int catalogId,
        [FromBody] CreateMaterialRateRevisionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = await service.CreateRevisionAsync(catalogId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-revision.create", result.Id, result);
            return CreatedAtAction(nameof(GetRevision), new { catalogId, revisionId = result.Id }, result);
        });
    }

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/import")]
    [Consumes("multipart/form-data")]
    [RequirePermission("crm.material-rates", "manage")]
    public async Task<ActionResult<MaterialRateImportResponse>> Import(
        int catalogId,
        int revisionId,
        IFormFile? file,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (file is null || file.Length == 0)
        {
            return BadRequest(new MaterialRateImportResponse
            {
                Errors = [new CsvImportError
                {
                    Message = "Vui lòng chọn tệp CSV UTF-8 có dữ liệu để nhập.",
                    MessageKey = "materialRates.validation.csvEmpty",
                }],
            });
        }
        var extension = Path.GetExtension(file.FileName);
        if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new MaterialRateImportResponse
            {
                Errors = [new CsvImportError
                {
                    Message = "Chỉ chấp nhận biểu mẫu Excel (.xlsx) hoặc tệp CSV UTF-8 (.csv).",
                    MessageKey = "materialRates.validation.fileType",
                }],
            });
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(new MaterialRateImportResponse
            {
                Errors = [new CsvImportError
                {
                    Message = "Tệp vượt quá dung lượng tối đa 5 MB.",
                    MessageKey = "materialRates.validation.maxBytes",
                    MessageArgs = new() { ["max"] = 5 },
                }],
            });
        }

        return await ExecuteAsync<MaterialRateImportResponse>(async () =>
        {
            await using var stream = file.OpenReadStream();
            var result = await service.ImportAsync(catalogId, revisionId, stream, userId.Value, file.FileName, ct);
            if (result is null) return NotFound();
            if (result.Errors.Count > 0) return BadRequest(result);
            Audit("material-rate-revision.import", revisionId, result);
            return Ok(result);
        });
    }

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/approve")]
    [RequirePermission("crm.material-rates", "approve")]
    public Task<ActionResult<MaterialRateRevisionResponse>> Approve(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct) => Decide(catalogId, revisionId, request?.Note, true, ct);

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/reject")]
    [RequirePermission("crm.material-rates", "approve")]
    public Task<ActionResult<MaterialRateRevisionResponse>> Reject(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct) => Decide(catalogId, revisionId, request?.Note, false, ct);

    [HttpPost("{catalogId:int}/revisions/{revisionId:int}/retire")]
    [RequirePermission("crm.material-rates", "approve")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> Retire(
        int catalogId,
        int revisionId,
        [FromBody] DecideMaterialRateRevisionRequest? request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = await service.RetireAsync(catalogId, revisionId, request?.Note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("material-rate-revision.retire", revisionId, result);
            return Ok(result);
        });
    }

    [HttpGet("{catalogId:int}/effective")]
    [RequirePermission("crm.material-rates", "view")]
    public async Task<ActionResult<MaterialRateRevisionResponse>> GetEffective(
        int catalogId,
        [FromQuery] DateOnly? onDate,
        CancellationToken ct)
    {
        var result = await service.GetEffectiveAsync(catalogId, onDate ?? DateOnly.FromDateTime(DateTime.UtcNow), ct);
        return result is null ? NotFound() : Ok(result);
    }

    private async Task<ActionResult<MaterialRateRevisionResponse>> Decide(
        int catalogId,
        int revisionId,
        string? note,
        bool approve,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        return await ExecuteAsync<MaterialRateRevisionResponse>(async () =>
        {
            var result = approve
                ? await service.ApproveAsync(catalogId, revisionId, note, userId.Value, ct)
                : await service.RejectAsync(catalogId, revisionId, note, userId.Value, ct);
            if (result is null) return NotFound();
            Audit(approve ? "material-rate-revision.approve" : "material-rate-revision.reject", revisionId, result);
            return Ok(result);
        });
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (MaterialRateOperationException exception)
        {
            return BadRequest(new { message = exception.Message, messageKey = exception.MessageKey });
        }
    }

    private void Audit(string action, int id, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = action.StartsWith("material-rate-catalog", StringComparison.Ordinal)
            ? EntityTypes.MaterialRateCatalog
            : EntityTypes.MaterialRateRevision,
        ResourceId = id.ToString(),
        Message = $"{action} #{id}.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("uid");
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static string? NormalizeLanguage(string language)
    {
        var normalized = language.Trim().ToLowerInvariant();
        return SupportedLanguages.Contains(normalized, StringComparer.Ordinal) ? normalized : null;
    }

    private BadRequestObjectResult InvalidLanguage() =>
        BadRequest(new { message = "Ngôn ngữ không hợp lệ. Chỉ chấp nhận vi, en, zh hoặc ja." });

    private static string GetExcelFileName(string language, MaterialRateCatalogType catalogType)
    {
        if (catalogType == MaterialRateCatalogType.Boq)
        {
            return language switch
            {
                "en" => "NICON-BOQ-Rate-Form.xlsx",
                "zh" => "NICON-BOQ单价表.xlsx",
                "ja" => "NICON-BOQ単価表.xlsx",
                _ => "NICON-Bieu-mau-don-gia-BOQ.xlsx",
            };
        }

        return language switch
        {
            "en" => "NICON-Material-Rate-Form.xlsx",
            "zh" => "NICON-材料定额单价表.xlsx",
            "ja" => "NICON-材料基準単価表.xlsx",
            _ => "NICON-Bieu-mau-dinh-muc-don-gia.xlsx",
        };
    }

    private static string CreateTemplateCsv(MaterialRateCatalogType catalogType) =>
        string.Join(',', catalogType == MaterialRateCatalogType.Boq
            ? MaterialRateService.BoqCsvHeaders
            : MaterialRateService.CsvHeaders) + "\r\n";

    private static void WriteUtf8Entry(ZipArchive archive, string name, string content, bool includeBom)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        if (includeBom)
        {
            stream.Write(Encoding.UTF8.GetPreamble());
        }
        stream.Write(Encoding.UTF8.GetBytes(content));
    }
}
