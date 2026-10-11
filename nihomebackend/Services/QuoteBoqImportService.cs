using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class QuoteBoqImportService(
    AppDbContext db, IQuoteService quoteService, IDataProtectionProvider dataProtection) : IQuoteBoqImportService
{
    public const string ParserVersion = "quote-boq-v1";
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private const int MaxSheets = 100;
    private const int MaxRows = 20_000;
    private const long MaxUncompressedBytes = 200L * 1024 * 1024;
    private static readonly string[] DescriptionAliases = ["description", "item description", "work description", "noi dung", "mo ta", "ten hang muc", "hang muc", "cong tac"];
    private static readonly string[] UnitAliases = ["unit", "don vi", "dvt"];
    private static readonly string[] QuantityAliases = ["quantity", "qty", "khoi luong", "kl"];
    private static readonly string[] UnitPriceAliases = ["unit price", "rate", "don gia"];
    private static readonly string[] CodeAliases = ["item code", "code", "ma hieu", "ma hang muc", "stt", "no"];
    private readonly IDataProtector _previewTokens = dataProtection.CreateProtector("Nihome.QuoteBoqImport.Preview.v1");

    public Task<byte[]> CreateTemplateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var workbook = new XLWorkbook();
        var guide = workbook.Worksheets.Add("Huong dan");
        guide.Cell("A1").Value = "NICON - TEMPLATE IMPORT BOQ BAO GIA";
        guide.Range("A1:F1").Merge().Style.Font.SetBold().Font.SetFontSize(16);
        guide.Cell("A3").Value = "Mỗi sheet chi tiết dùng đúng 6 cột bên dưới. Có thể sao chép sheet Mau hang muc cho từng nhà xưởng, nhà kho, văn phòng, ký túc xá hoặc hạ tầng.";
        guide.Range("A3:F3").Merge().Style.Alignment.SetWrapText();
        guide.Cell("A5").Value = "Quy tắc";
        guide.Cell("B5").Value = "Mã không bắt buộc; mô tả và đơn vị bắt buộc; khối lượng > 0; đơn giá >= 0. Không gộp ô trong vùng dữ liệu.";
        guide.Range("B5:F5").Merge().Style.Alignment.SetWrapText();
        guide.Columns("A:F").AdjustToContents();
        guide.Column("B").Width = 85;

        var detail = workbook.Worksheets.Add("Mau hang muc");
        var headers = new[] { "Mã hạng mục", "Mô tả", "Đơn vị", "Khối lượng", "Đơn giá", "Thành tiền" };
        for (var column = 1; column <= headers.Length; column++) detail.Cell(1, column).Value = headers[column - 1];
        detail.Range("A1:F1").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#1F4E78"));
        detail.Range("A1:F1").Style.Font.SetFontColor(XLColor.White);
        detail.Cell("A2").Value = "CT-001";
        detail.Cell("B2").Value = "Bê tông móng";
        detail.Cell("C2").Value = "m³";
        detail.Cell("D2").Value = 100;
        detail.Cell("E2").Value = 1500000;
        detail.Cell("F2").FormulaA1 = "=D2*E2";
        detail.Range("D2:F1000").Style.NumberFormat.Format = "#,##0.####";
        detail.SheetView.FreezeRows(1);
        detail.Range("A1:F1000").SetAutoFilter();
        detail.Columns("A:F").AdjustToContents();
        detail.Column("B").Width = 45;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    public async Task<QuoteBoqImportPreviewResponse> PreviewAsync(
        int quoteId, int callerUserId, IFormFile? file, CancellationToken ct = default)
    {
        var source = await ReadFileAsync(file, ct);
        using var workbook = OpenWorkbook(source.Bytes);
        ValidateWorkbook(workbook);
        var response = new QuoteBoqImportPreviewResponse
        {
            SourceFileName = source.FileName,
            SourceHash = source.Hash,
            ParserVersion = ParserVersion,
            PreviewToken = _previewTokens.Protect($"{quoteId}|{callerUserId}|{source.Hash}|{DateTime.UtcNow.Ticks}"),
        };
        var order = 0;
        foreach (var sheet in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();
            var mapping = DetectMapping(sheet);
            mapping.Order = ++order;
            response.Sheets.Add(mapping);
            response.SuggestedItemCount += mapping.SuggestedItemCount;
        }
        if (response.Sheets.All(sheet => sheet.SuggestedClassification != "Detail"))
            response.WorkbookWarnings.Add("Không tự nhận diện được sheet chi tiết. Hãy dùng template chuẩn hoặc chọn và map cột thủ công.");
        return response;
    }

    public async Task<QuoteBoqImportResponse?> ConfirmAsync(
        int quoteId, IFormFile? file, ConfirmQuoteBoqImportRequest request, int callerUserId,
        bool canManage, bool canSeeAll, CancellationToken ct = default)
    {
        if (!canManage) throw new QuoteOperationException("Không có quyền import BOQ báo giá.");
        var source = await ReadFileAsync(file, ct);
        ValidatePreviewToken(request.PreviewToken, quoteId, callerUserId, source.Hash);
        if (!string.Equals(source.Hash, request.SourceHash?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new QuoteOperationException("File BOQ đã thay đổi sau khi preview. Vui lòng preview lại trước khi xác nhận.");
        using var workbook = OpenWorkbook(source.Bytes);
        ValidateWorkbook(workbook);
        ValidateMappings(workbook, request.Sheets);
        var parsed = ParseDetails(workbook, request.Sheets);
        if (parsed.Errors.Count > 0)
            throw new QuoteOperationException("BOQ không hợp lệ: " + string.Join("; ", parsed.Errors.Take(20)));
        if (parsed.Items.Count == 0)
            throw new QuoteOperationException("BOQ không có dòng chi tiết hợp lệ để import.");

        var duplicate = await db.Set<QuoteBoqImport>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.QuoteId == quoteId && item.SourceHash == source.Hash && item.ParserVersion == ParserVersion, ct);
        if (duplicate is not null)
            throw new QuoteOperationException($"File này đã được import vào báo giá ở phiên bản {duplicate.QuoteVersion}.");

        var quote = await quoteService.ImportBoqAsync(
            quoteId, request, parsed.Items, source.FileName, source.Hash, ParserVersion,
            JsonSerializer.Serialize(request.Sheets), JsonSerializer.Serialize(parsed.Outcomes),
            callerUserId, canManage, canSeeAll, ct);
        if (quote is null) return null;
        var import = await db.Set<QuoteBoqImport>().AsNoTracking()
            .SingleAsync(item => item.QuoteId == quoteId && item.SourceHash == source.Hash && item.ParserVersion == ParserVersion, ct);
        return new QuoteBoqImportResponse
        {
            ImportId = import.Id,
            QuoteId = quoteId,
            QuoteVersion = quote.Version,
            ImportedItemCount = import.ImportedItemCount,
            SourceHash = import.SourceHash,
            ImportedAt = import.ImportedAt,
            Quote = quote,
        };
    }

    private void ValidatePreviewToken(string token, int quoteId, int callerUserId, string sourceHash)
    {
        try
        {
            var parts = _previewTokens.Unprotect(token ?? string.Empty).Split('|');
            if (parts.Length != 4 || parts[0] != quoteId.ToString(CultureInfo.InvariantCulture) ||
                parts[1] != callerUserId.ToString(CultureInfo.InvariantCulture) ||
                !string.Equals(parts[2], sourceHash, StringComparison.OrdinalIgnoreCase) ||
                !long.TryParse(parts[3], out var ticks) ||
                DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > TimeSpan.FromMinutes(30))
                throw new QuoteOperationException("Preview BOQ đã hết hạn hoặc không khớp. Vui lòng preview lại.");
        }
        catch (Exception ex) when (ex is not QuoteOperationException)
        {
            throw new QuoteOperationException("Preview BOQ không hợp lệ. Vui lòng preview lại.");
        }
    }

    private static XLWorkbook OpenWorkbook(byte[] bytes)
    {
        try
        {
            ValidatePackage(bytes);
            return new XLWorkbook(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is not QuoteOperationException)
        { throw new QuoteOperationException("Không thể đọc file Excel. Chỉ chấp nhận workbook .xlsx hợp lệ, không mã hoá hoặc hỏng."); }
    }

    private static void ValidatePackage(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false);
        long uncompressed = 0;
        foreach (var entry in archive.Entries)
        {
            uncompressed += entry.Length;
            if (uncompressed > MaxUncompressedBytes)
                throw new QuoteOperationException("Nội dung giải nén của workbook vượt quá 200 MB.");
            var name = entry.FullName.Replace('\\', '/');
            if (name.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                throw new QuoteOperationException("Workbook có macro và không được hỗ trợ.");
            if (name.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("xl/connections.xml", StringComparison.OrdinalIgnoreCase))
                throw new QuoteOperationException("Workbook có liên kết dữ liệu bên ngoài và không được hỗ trợ.");
        }
    }

    private static void ValidateWorkbook(XLWorkbook workbook)
    {
        if (workbook.Worksheets.Count > MaxSheets)
            throw new QuoteOperationException($"Workbook có quá nhiều sheet; tối đa {MaxSheets} sheet.");
        var totalRows = workbook.Worksheets.Sum(sheet => sheet.LastRowUsed()?.RowNumber() ?? 0);
        if (totalRows > MaxRows)
            throw new QuoteOperationException($"Workbook có quá nhiều dòng; tối đa {MaxRows:N0} dòng trên toàn bộ sheet.");
    }

    private static QuoteBoqSheetPreviewResponse DetectMapping(IXLWorksheet sheet)
    {
        var result = new QuoteBoqSheetPreviewResponse
        {
            Name = sheet.Name,
            IsHidden = sheet.Visibility != XLWorksheetVisibility.Visible,
        };
        var lastRow = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0, 100);
        var lastColumn = Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0, 100);
        var bestScore = 0;
        for (var row = 1; row <= lastRow; row++)
        {
            var columns = new Dictionary<string, int>();
            for (var column = 1; column <= lastColumn; column++)
            {
                var header = Normalize(sheet.Cell(row, column).GetFormattedString());
                if (Matches(header, DescriptionAliases)) columns.TryAdd("description", column);
                if (Matches(header, UnitAliases)) columns.TryAdd("unit", column);
                if (Matches(header, QuantityAliases)) columns.TryAdd("quantity", column);
                if (Matches(header, UnitPriceAliases)) columns.TryAdd("unitPrice", column);
                if (Matches(header, CodeAliases)) columns.TryAdd("code", column);
            }
            var score = columns.Keys.Count(key => key != "code");
            if (score <= bestScore) continue;
            bestScore = score;
            result.HeaderRow = row;
            result.DescriptionColumn = columns.GetValueOrDefault("description");
            result.UnitColumn = columns.GetValueOrDefault("unit");
            result.QuantityColumn = columns.GetValueOrDefault("quantity");
            result.UnitPriceColumn = columns.GetValueOrDefault("unitPrice");
            result.ItemCodeColumn = columns.GetValueOrDefault("code");
        }
        if (bestScore == 4)
        {
            result.SuggestedClassification = "Detail";
            result.SuggestedItemCount = CountCandidateRows(sheet, result);
        }
        else if (Normalize(sheet.Name).Contains("tong hop") || Normalize(sheet.Name).Contains("summary") || Normalize(sheet.Name).Contains("total"))
            result.SuggestedClassification = "Summary";
        if (result.IsHidden) result.Warnings.Add("Sheet đang ẩn; cần xác nhận rõ nếu dùng làm chi tiết.");
        return result;
    }

    private static int CountCandidateRows(IXLWorksheet sheet, QuoteBoqSheetPreviewResponse mapping)
    {
        if (!mapping.HeaderRow.HasValue || !mapping.DescriptionColumn.HasValue) return 0;
        var last = sheet.LastRowUsed()?.RowNumber() ?? mapping.HeaderRow.Value;
        return Enumerable.Range(mapping.HeaderRow.Value + 1, Math.Max(0, last - mapping.HeaderRow.Value))
            .Count(row => !string.IsNullOrWhiteSpace(sheet.Cell(row, mapping.DescriptionColumn.Value).GetFormattedString()));
    }

    private static ParsedImport ParseDetails(XLWorkbook workbook, IReadOnlyCollection<QuoteBoqSheetMappingInput> mappings)
    {
        var result = new ParsedImport();
        var sortOrder = 0;
        foreach (var mapping in mappings.Where(item => item.Classification == "Detail"))
        {
            var sheet = workbook.Worksheet(mapping.SheetName);
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? mapping.HeaderRow!.Value;
            for (var row = mapping.HeaderRow!.Value + 1; row <= lastRow; row++)
            {
                var location = $"{sheet.Name}!{row}";
                var descriptionCell = sheet.Cell(row, mapping.DescriptionColumn!.Value);
                var unitCell = sheet.Cell(row, mapping.UnitColumn!.Value);
                var quantityCell = sheet.Cell(row, mapping.QuantityColumn!.Value);
                var priceCell = sheet.Cell(row, mapping.UnitPriceColumn!.Value);
                if (string.IsNullOrWhiteSpace(descriptionCell.GetFormattedString()) &&
                    string.IsNullOrWhiteSpace(unitCell.GetFormattedString()) &&
                    string.IsNullOrWhiteSpace(quantityCell.GetFormattedString()) &&
                    string.IsNullOrWhiteSpace(priceCell.GetFormattedString())) continue;
                if (IsAmbiguousMergedCell(descriptionCell) || IsAmbiguousMergedCell(unitCell) ||
                    IsAmbiguousMergedCell(quantityCell) || IsAmbiguousMergedCell(priceCell))
                {
                    result.Errors.Add($"{location}: ô dữ liệu nằm trong vùng merge không rõ nghĩa.");
                    result.Outcomes.Add(new(location, "Rejected", "MergedCell"));
                    continue;
                }
                var description = descriptionCell.GetString().Trim();
                var unit = unitCell.GetString().Trim();
                if (description.Length is < 1 or > 300 || unit.Length is < 1 or > 30 ||
                    !TryDecimal(quantityCell, out var quantity) || !TryDecimal(priceCell, out var unitPrice) ||
                    quantity <= 0 || unitPrice < 0)
                {
                    result.Errors.Add($"{location}: mô tả/đơn vị/khối lượng/đơn giá không hợp lệ.");
                    result.Outcomes.Add(new(location, "Rejected", "InvalidRequiredValue"));
                    continue;
                }
                var code = mapping.ItemCodeColumn.HasValue ? sheet.Cell(row, mapping.ItemCodeColumn.Value).GetString().Trim() : null;
                if (code?.Length > 60)
                {
                    result.Errors.Add($"{location}: mã hạng mục dài quá 60 ký tự.");
                    result.Outcomes.Add(new(location, "Rejected", "CodeTooLong"));
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(code) && !result.ItemCodes.Add(code))
                {
                    result.Errors.Add($"{location}: mã hạng mục '{code}' bị trùng trong các sheet Detail.");
                    result.Outcomes.Add(new(location, "Rejected", "DuplicateItemCode"));
                    continue;
                }
                if (decimal.Round(quantity, 4) != quantity || decimal.Round(unitPrice, 2) != unitPrice)
                {
                    result.Errors.Add($"{location}: khối lượng tối đa 4 số lẻ và đơn giá tối đa 2 số lẻ.");
                    result.Outcomes.Add(new(location, "Rejected", "InvalidPrecision"));
                    continue;
                }
                result.Items.Add(new QuoteItemInput
                {
                    ItemCode = string.IsNullOrWhiteSpace(code) ? null : code,
                    Name = description,
                    Unit = unit,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    SortOrder = ++sortOrder,
                });
                result.Outcomes.Add(new(
                    location,
                    "Imported",
                    null,
                    quantityCell.HasFormula ? quantityCell.FormulaA1 : null,
                    priceCell.HasFormula ? priceCell.FormulaA1 : null));
            }
        }
        return result;
    }

    private static void ValidateMappings(XLWorkbook workbook, IReadOnlyCollection<QuoteBoqSheetMappingInput> mappings)
    {
        if (mappings.Count == 0 || mappings.All(item => item.Classification != "Detail"))
            throw new QuoteOperationException("Chọn ít nhất một sheet Detail để import.");
        if (mappings.Select(item => item.SheetName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != mappings.Count)
            throw new QuoteOperationException("Mỗi sheet chỉ được map một lần.");
        foreach (var mapping in mappings)
        {
            if (!workbook.Worksheets.TryGetWorksheet(mapping.SheetName, out _))
                throw new QuoteOperationException($"Không tìm thấy sheet '{mapping.SheetName}' trong file đã preview.");
            if (mapping.Classification == "Detail" &&
                (!mapping.HeaderRow.HasValue || !mapping.DescriptionColumn.HasValue || !mapping.UnitColumn.HasValue ||
                 !mapping.QuantityColumn.HasValue || !mapping.UnitPriceColumn.HasValue))
                throw new QuoteOperationException($"Sheet '{mapping.SheetName}' thiếu header row hoặc mapping cột bắt buộc.");
        }
    }

    private static bool IsAmbiguousMergedCell(IXLCell cell)
    {
        if (!cell.IsMerged()) return false;
        var range = cell.MergedRange();
        return range is not null && range.FirstCell().Address != cell.Address;
    }

    private static bool TryDecimal(IXLCell cell, out decimal value)
    {
        if (cell.TryGetValue(out value)) return true;
        var text = cell.GetFormattedString().Trim().Replace(" ", string.Empty);
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out value);
    }

    private static bool Matches(string value, IEnumerable<string> aliases) => aliases.Any(alias => value == alias || value.Contains(alias));
    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }

    private static async Task<SourceFile> ReadFileAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new QuoteOperationException("Chọn file Excel .xlsx để import.");
        if (file.Length > MaxFileBytes) throw new QuoteOperationException("File BOQ vượt quá 20 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new QuoteOperationException("Chỉ chấp nhận file Excel .xlsx; file macro .xlsm và định dạng cũ không được hỗ trợ.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        return new SourceFile(Path.GetFileName(file.FileName), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes);
    }

    private sealed record SourceFile(string FileName, string Hash, byte[] Bytes);
    private sealed record RowOutcome(
        string Location,
        string Status,
        string? Reason,
        string? QuantityFormula = null,
        string? UnitPriceFormula = null);
    private sealed class ParsedImport
    {
        public List<QuoteItemInput> Items { get; } = [];
        public List<RowOutcome> Outcomes { get; } = [];
        public List<string> Errors { get; } = [];
        public HashSet<string> ItemCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
