using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IBoqCatalogWorkbookService
{
    Task<List<BoqImportTemplateResponse>> ListTemplatesAsync(CancellationToken ct = default);
    Task<BoqImportTemplateResponse> CreateTemplateAsync(CreateBoqImportTemplateRequest request, int userId, CancellationToken ct = default);
    Task<BoqWorkbookPreviewResponse> PreviewAsync(IFormFile? file, CancellationToken ct = default);
    Task<MaterialRateRevisionResponse?> ImportAsync(int catalogId, int revisionId, IFormFile? file, string sourceHash,
        IReadOnlyList<BoqSheetMappingRequest> mappings, int userId, CancellationToken ct = default);
}

public sealed class BoqCatalogWorkbookService(AppDbContext db, IMaterialRateService materialRates) : IBoqCatalogWorkbookService
{
    private const long MaxBytes = 20 * 1024 * 1024;
    private static readonly string[] DescriptionAliases = ["description", "item description", "work description", "noi dung", "mo ta", "ten cong viec", "ten hang muc", "hang muc", "dien giai"];
    private static readonly string[] UnitAliases = ["unit", "don vi", "dvt"];
    private static readonly string[] QuantityAliases = ["quantity", "qty", "q ty", "khoi luong", "k luong", "kl tong", "kl", "sl"];
    private static readonly string[] PriceAliases = ["unit price", "total price", "don gia tong", "don gia", "rate"];
    private static readonly string[] CodeAliases = ["item code", "code", "ma hieu", "ma hang muc", "stt", "no"];

    public async Task<List<BoqImportTemplateResponse>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var result = new List<BoqImportTemplateResponse>
        {
            new() { Id = 0, Name = "BOQ chuẩn NICON", IsSystem = true, HeaderRow = 4, ItemCodeColumn = 1, DescriptionColumn = 2, UnitColumn = 3, QuantityColumn = 4, UnitPriceColumn = 5 },
        };
        result.AddRange(await db.BoqImportTemplates.AsNoTracking().OrderBy(item => item.Name).Select(item => new BoqImportTemplateResponse
        {
            Id = item.Id,
            Name = item.Name,
            HeaderRow = item.HeaderRow,
            ItemCodeColumn = item.ItemCodeColumn,
            DescriptionColumn = item.DescriptionColumn,
            UnitColumn = item.UnitColumn,
            QuantityColumn = item.QuantityColumn,
            UnitPriceColumn = item.UnitPriceColumn,
        }).ToListAsync(ct));
        return result;
    }

    public async Task<BoqImportTemplateResponse> CreateTemplateAsync(CreateBoqImportTemplateRequest request, int userId, CancellationToken ct = default)
    {
        ValidateColumns(request.ItemCodeColumn, request.DescriptionColumn, request.UnitColumn, request.QuantityColumn, request.UnitPriceColumn);
        var name = request.Name.Trim();
        if (await db.BoqImportTemplates.AnyAsync(item => item.Name == name, ct))
            throw new MaterialRateOperationException("Tên mẫu BOQ đã tồn tại.", "materialRates.boqTemplate.duplicate");
        var entity = new BoqImportTemplate
        {
            Name = name,
            HeaderRow = request.HeaderRow,
            ItemCodeColumn = request.ItemCodeColumn,
            DescriptionColumn = request.DescriptionColumn,
            UnitColumn = request.UnitColumn,
            QuantityColumn = request.QuantityColumn,
            UnitPriceColumn = request.UnitPriceColumn,
            CreatedByUserId = userId,
        };
        db.BoqImportTemplates.Add(entity);
        await db.SaveChangesAsync(ct);
        return new BoqImportTemplateResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            HeaderRow = entity.HeaderRow,
            ItemCodeColumn = entity.ItemCodeColumn,
            DescriptionColumn = entity.DescriptionColumn,
            UnitColumn = entity.UnitColumn,
            QuantityColumn = entity.QuantityColumn,
            UnitPriceColumn = entity.UnitPriceColumn,
        };
    }

    public async Task<BoqWorkbookPreviewResponse> PreviewAsync(IFormFile? file, CancellationToken ct = default)
    {
        var source = await ReadAsync(file, ct);
        using var workbook = Open(source.Bytes);
        if (workbook.Worksheets.Count > 100) throw Error("Workbook vượt quá 100 sheet.");
        var response = new BoqWorkbookPreviewResponse { SourceFileName = source.Name, SourceHash = source.Hash };
        var order = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            var detected = Detect(sheet, ++order);
            response.Sheets.Add(detected);
        }
        return response;
    }

    public async Task<MaterialRateRevisionResponse?> ImportAsync(int catalogId, int revisionId, IFormFile? file, string sourceHash,
        IReadOnlyList<BoqSheetMappingRequest> mappings, int userId, CancellationToken ct = default)
    {
        var source = await ReadAsync(file, ct);
        if (!string.Equals(source.Hash, sourceHash?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw Error("File đã thay đổi sau khi xem trước. Vui lòng xem trước lại.", "materialRates.boqWorkbook.changed");
        if (mappings.Count == 0 || mappings.All(item => !item.Include))
            throw Error("Chọn ít nhất một sheet chi tiết để nhập.", "materialRates.boqWorkbook.sheetRequired");
        using var workbook = Open(source.Bytes);
        var lines = new List<UpsertBoqMaterialRateLineRequest>();
        var generatedCode = 0;
        foreach (var mapping in mappings.Where(item => item.Include))
        {
            ValidateColumns(mapping.ItemCodeColumn, mapping.DescriptionColumn, mapping.UnitColumn, mapping.QuantityColumn, mapping.UnitPriceColumn);
            if (!workbook.Worksheets.TryGetWorksheet(mapping.SheetName, out var sheet)) throw Error($"Không tìm thấy sheet '{mapping.SheetName}'.");
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? mapping.HeaderRow;
            for (var row = mapping.HeaderRow + 1; row <= lastRow; row++)
            {
                var parsed = ParseRow(sheet, row, mapping.DescriptionColumn, mapping.UnitColumn, mapping.QuantityColumn, mapping.UnitPriceColumn);
                if (parsed.Kind is BoqRowKind.Empty or BoqRowKind.Structural) continue;
                if (parsed.Kind == BoqRowKind.Invalid)
                    throw Error($"{sheet.Name}!{row}: tên công việc, đơn vị, khối lượng hoặc đơn giá không hợp lệ.", "materialRates.boqWorkbook.invalidRow");
                var code = mapping.ItemCodeColumn.HasValue ? sheet.Cell(row, mapping.ItemCodeColumn.Value).GetString().Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(code)) code = $"BOQ-{++generatedCode:00000}";
                lines.Add(new UpsertBoqMaterialRateLineRequest { ItemCode = code, ItemName = parsed.Name, Unit = parsed.Unit, Quantity = parsed.Quantity, UnitPrice = parsed.Price });
            }
        }
        return await materialRates.ReplaceBoqLinesAsync(catalogId, revisionId, lines, source.Name, source.Hash,
            JsonSerializer.Serialize(mappings), userId, ct);
    }

    private static BoqWorkbookSheetPreviewResponse Detect(IXLWorksheet sheet, int order)
    {
        int? header = null, code = null, description = null, unit = null, quantity = null, price = null;
        var best = 0;
        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 100);
        var lastColumn = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, 100);
        for (var row = 1; row <= lastRow; row++)
        {
            int? rowCode = null, rowDescription = null, rowUnit = null, rowQuantity = null, rowPrice = null;
            for (var column = 1; column <= lastColumn; column++)
            {
                var value = Normalize(sheet.Cell(row, column).GetFormattedString());
                var isCode = Matches(value, CodeAliases);
                var isPrice = Matches(value, PriceAliases);
                if (isCode) rowCode ??= column;
                if (!isCode && Matches(value, DescriptionAliases)) rowDescription ??= column;
                if (!isPrice && Matches(value, UnitAliases)) rowUnit ??= column;
                if (Matches(value, QuantityAliases)) rowQuantity ??= column;
                if (isPrice) rowPrice ??= column;
            }
            var score = new[] { rowDescription, rowUnit, rowQuantity, rowPrice }.Count(value => value.HasValue);
            if (score <= best) continue;
            best = score; header = row; code = rowCode; description = rowDescription; unit = rowUnit; quantity = rowQuantity; price = rowPrice;
        }
        var warnings = new List<string>();
        if (sheet.Visibility != XLWorksheetVisibility.Visible) warnings.Add("Sheet đang ẩn.");
        if (best < 4) warnings.Add("Chưa nhận diện đủ cột; hãy chọn mẫu hoặc chỉnh mapping.");
        var candidates = 0;
        var invalidDetails = 0;
        if (header.HasValue && description.HasValue && unit.HasValue && quantity.HasValue && price.HasValue)
        {
            foreach (var row in Enumerable.Range(header.Value + 1, Math.Max(0, (sheet.LastRowUsed()?.RowNumber() ?? header.Value) - header.Value)))
            {
                var kind = ParseRow(sheet, row, description.Value, unit.Value, quantity.Value, price.Value).Kind;
                if (kind == BoqRowKind.Valid) candidates++;
                else if (kind == BoqRowKind.Invalid) invalidDetails++;
            }
        }
        if (invalidDetails > 0) warnings.Add($"Có {invalidDetails} dòng chi tiết thiếu hoặc sai đơn vị, khối lượng, đơn giá.");
        var columns = header.HasValue
            ? Enumerable.Range(1, lastColumn).Select(column => new BoqWorkbookColumnPreviewResponse
            {
                Number = column,
                Label = string.IsNullOrWhiteSpace(sheet.Cell(header.Value, column).GetFormattedString())
                    ? $"Cột {XLHelper.GetColumnLetterFromNumber(column)}"
                    : $"{XLHelper.GetColumnLetterFromNumber(column)} — {sheet.Cell(header.Value, column).GetFormattedString().Trim()}"
            }).ToList()
            : [];
        var previewRows = header.HasValue
            ? Enumerable.Range(header.Value + 1, Math.Max(0, Math.Min(sheet.LastRowUsed()?.RowNumber() ?? header.Value, header.Value + 30) - header.Value))
                .Select(row => new BoqWorkbookRowPreviewResponse
                {
                    RowNumber = row,
                    Cells = Enumerable.Range(1, lastColumn)
                        .Select(column => (column, value: sheet.Cell(row, column).GetFormattedString().Trim()))
                        .Where(cell => !string.IsNullOrWhiteSpace(cell.value))
                        .ToDictionary(cell => cell.column, cell => cell.value)
                })
                .Where(row => row.Cells.Count > 0)
                .Take(8)
                .ToList()
            : [];
        return new BoqWorkbookSheetPreviewResponse
        {
            Name = sheet.Name,
            Order = order,
            IsHidden = sheet.Visibility != XLWorksheetVisibility.Visible,
            HeaderRow = header,
            ItemCodeColumn = code,
            DescriptionColumn = description,
            UnitColumn = unit,
            QuantityColumn = quantity,
            UnitPriceColumn = price,
            CandidateCount = candidates,
            Warnings = warnings,
            Columns = columns,
            PreviewRows = previewRows
        };
    }

    private static void ValidateColumns(int? code, params int[] required)
    {
        var columns = required.Cast<int?>().Append(code).Where(value => value.HasValue).Select(value => value!.Value).ToList();
        if (required.Any(value => value is < 1 or > 500) || code is < 1 or > 500 || columns.Distinct().Count() != columns.Count)
            throw Error("Mapping cột phải từ 1 đến 500 và không được trùng nhau.", "materialRates.boqWorkbook.invalidMapping");
    }

    private static bool TryDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue(out value)) return true;
        return decimal.TryParse(cell.GetFormattedString().Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static ParsedBoqRow ParseRow(IXLWorksheet sheet, int row, int descriptionColumn, int unitColumn, int quantityColumn, int priceColumn)
    {
        var name = sheet.Cell(row, descriptionColumn).GetString().Trim();
        var unit = sheet.Cell(row, unitColumn).GetString().Trim();
        var quantityText = sheet.Cell(row, quantityColumn).GetFormattedString().Trim();
        var priceText = sheet.Cell(row, priceColumn).GetFormattedString().Trim();
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(unit) && string.IsNullOrWhiteSpace(quantityText) && string.IsNullOrWhiteSpace(priceText))
            return new(BoqRowKind.Empty, name, unit, 0, 0);

        // Customer workbooks contain section labels and subtotal rows between
        // detail lines. With no unit and quantity, they are structure, not rates.
        if (string.IsNullOrWhiteSpace(unit) && string.IsNullOrWhiteSpace(quantityText))
            return new(BoqRowKind.Structural, name, unit, 0, 0);

        // Some sheets repeat their column header between work sections.
        if (Matches(Normalize(name), DescriptionAliases) && Matches(Normalize(unit), UnitAliases))
            return new(BoqRowKind.Structural, name, unit, 0, 0);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit) ||
            !TryDecimal(sheet.Cell(row, quantityColumn), out var quantity) ||
            !TryDecimal(sheet.Cell(row, priceColumn), out var price) || quantity <= 0 || price < 0)
            return new(BoqRowKind.Invalid, name, unit, 0, 0);

        return new(BoqRowKind.Valid, name, unit, quantity, price);
    }

    private enum BoqRowKind { Empty, Structural, Invalid, Valid }
    private readonly record struct ParsedBoqRow(BoqRowKind Kind, string Name, string Unit, decimal Quantity, decimal Price);

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(withoutMarks.Replace('đ', 'd').Replace('Đ', 'D'), "[^a-zA-Z0-9]+", " ").Trim().ToLowerInvariant();
    }
    private static bool Matches(string value, IEnumerable<string> aliases) => aliases.Any(alias => value == alias || value.Contains(alias));
    private static XLWorkbook Open(byte[] bytes) { try { return new XLWorkbook(new MemoryStream(bytes)); } catch { throw Error("Không thể đọc workbook .xlsx hợp lệ.", "materialRates.boqWorkbook.invalidFile"); } }
    private static async Task<(byte[] Bytes, string Name, string Hash)> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw Error("Vui lòng chọn file .xlsx có dữ liệu.");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) throw Error("Chỉ chấp nhận file Excel .xlsx.");
        if (file.Length > MaxBytes) throw Error("File BOQ vượt quá 20 MB.");
        await using var input = file.OpenReadStream(); using var output = new MemoryStream(); await input.CopyToAsync(output, ct);
        var bytes = output.ToArray();
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            if (archive.Entries.Count > 500 || archive.Entries.Sum(entry => entry.Length) > 200L * 1024 * 1024)
                throw Error("Nội dung workbook vượt giới hạn an toàn.");

            var unsafePart = archive.Entries.Any(entry =>
                entry.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("xl/connections.xml", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase));
            if (unsafePart)
                throw Error("Workbook chứa liên kết ngoài, kết nối dữ liệu hoặc macro không được hỗ trợ.", "materialRates.boqWorkbook.unsafeFile");
        }
        return (bytes, Path.GetFileName(file.FileName), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }
    private static MaterialRateOperationException Error(string message, string? key = null) => new(message, key);
}
