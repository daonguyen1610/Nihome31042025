using ClosedXML.Excel;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IQuoteSpreadsheetService
{
    Task<byte[]> CreateAsync(QuoteResponse quote, string languageCode, CancellationToken ct = default);
}

public sealed class QuoteSpreadsheetService(TranslationService translations) : IQuoteSpreadsheetService
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const int ItemsHeaderRow = 16;
    private static readonly HashSet<string> SupportedLanguages = ["vi", "en", "zh", "ja"];
    private static readonly XLColor BrandRed = XLColor.FromHtml("#EF3340");
    private static readonly XLColor Navy = XLColor.FromHtml("#1F2937");
    private static readonly XLColor LightGray = XLColor.FromHtml("#F3F4F6");

    public async Task<byte[]> CreateAsync(
        QuoteResponse quote,
        string languageCode,
        CancellationToken ct = default)
    {
        var language = languageCode?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedLanguages.Contains(language))
        {
            throw new QuoteOperationException(
                "Ngôn ngữ xuất Excel không hợp lệ. Chỉ chấp nhận vi, en, zh hoặc ja.");
        }

        var text = await translations.GetTranslationMapAsync(language);
        ct.ThrowIfCancellationRequested();
        string T(string key, string fallback) => text.GetValueOrDefault(key, fallback);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeWorksheetName(T("quotes.excel.sheetName", "Báo giá")));
        sheet.ShowGridLines = false;
        sheet.SheetView.FreezeRows(ItemsHeaderRow);

        BuildHeader(sheet, quote, T);
        BuildPricingBasis(sheet, quote, T);
        var lastItemRow = BuildItems(sheet, quote, T);
        BuildTotals(sheet, quote, lastItemRow + 2, T);
        ApplyPageLayout(sheet, lastItemRow + 8);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static void BuildPricingBasis(
        IXLWorksheet sheet,
        QuoteResponse quote,
        Func<string, string, string> text)
    {
        sheet.Range("A9:F9").Merge().Value = text("quotes.excel.pricing", "CƠ SỞ ĐƠN GIÁ");
        sheet.Range("A9:F9").Style
            .Font.SetBold().Font.SetFontColor(Navy).Font.SetFontName("Arial")
            .Fill.SetBackgroundColor(LightGray)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

        var catalog = string.Join(" - ", new[] { quote.MaterialRateCatalogCode, quote.MaterialRateCatalogName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        SetLabelValue(sheet, 10, 1, text("quotes.excel.area", "Diện tích (m²)"), quote.AreaSqm);
        SetLabelValue(sheet, 10, 4, text("quotes.excel.catalog", "Danh mục"),
            string.IsNullOrWhiteSpace(catalog) ? null : catalog);
        SetLabelValue(sheet, 11, 1, text("quotes.excel.revision", "Phiên bản đơn giá"),
            quote.MaterialRateRevisionVersion);
        SetLabelValue(sheet, 11, 4, text("quotes.excel.effectiveDate", "Ngày áp dụng"),
            quote.PricingEffectiveDate);
        SetLabelValue(sheet, 12, 1, text("quotes.excel.catalogRate", "Đơn giá danh mục/m²"),
            quote.CatalogUnitPricePerSqm);
        SetLabelValue(sheet, 12, 4, text("quotes.excel.appliedRate", "Đơn giá áp dụng/m²"),
            quote.UnitPricePerSqm);
        SetLabelValue(sheet, 13, 1, text("quotes.excel.rateSource", "Nguồn đơn giá"),
            text($"quotes.rateSource.{quote.RateSource}", quote.RateSource));
        SetLabelValue(sheet, 14, 1, text("quotes.excel.overrideReason", "Lý do điều chỉnh"),
            quote.RateOverrideReason);
        sheet.Range("A10:F14").Style.Font.SetFontName("Arial").Font.SetFontSize(10);
        sheet.Range("A9:F14").Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
    }

    private static void BuildHeader(
        IXLWorksheet sheet,
        QuoteResponse quote,
        Func<string, string, string> text)
    {
        var preliminary = quote.Status is "Draft" or "PendingApproval";
        sheet.Range("A1:F1").Merge().Value = text("quotes.excel.title", "BÁO GIÁ");
        sheet.Range("A1:F1").Style
            .Font.SetBold().Font.SetFontSize(18).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(BrandRed)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 34;

        if (preliminary)
        {
            sheet.Range("A2:F2").Merge().Value = text("quotes.excel.preliminaryWatermark", "SƠ BỘ");
            sheet.Range("A2:F2").Style
                .Font.SetBold().Font.SetFontColor(BrandRed)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#FDE8EA"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        SetLabelValue(sheet, 3, 1, text("quotes.excel.code", "Mã báo giá"), quote.Code);
        SetLabelValue(sheet, 3, 4, text("quotes.excel.version", "Phiên bản"), $"V{quote.Version}");
        SetLabelValue(sheet, 4, 1, text("quotes.excel.customer", "Khách hàng"), quote.CustomerName ?? "-");
        SetLabelValue(sheet, 5, 1, text("quotes.excel.opportunity", "Cơ hội"), quote.OpportunityName ?? "-");
        SetLabelValue(sheet, 6, 1, text("quotes.excel.validUntil", "Hiệu lực đến"), quote.ValidUntil);
        SetLabelValue(sheet, 6, 4, text("quotes.field.method", "Phương thức"),
            text($"quotes.method.{quote.Method}", quote.Method));
        SetLabelValue(sheet, 7, 1, text("quotes.field.owner", "Sales phụ trách"), quote.OwnerName ?? "-");
        SetLabelValue(sheet, 7, 4, text("quotes.field.status", "Trạng thái"),
            text($"quotes.status.{quote.Status}", quote.Status));

        sheet.Range("A3:F7").Style.Font.SetFontName("Arial").Font.SetFontSize(10);
        sheet.Cell(6, 2).Style.DateFormat.Format = "dd/mm/yyyy";
    }

    private static int BuildItems(
        IXLWorksheet sheet,
        QuoteResponse quote,
        Func<string, string, string> text)
    {
        var headers = new[]
        {
            text("quotes.excel.header.code", "Mã"),
            text("quotes.item.name", "Hạng mục"),
            text("quotes.item.unit", "Đơn vị"),
            text("quotes.item.quantity", "Khối lượng"),
            text("quotes.item.unitPrice", "Đơn giá"),
            text("quotes.item.amount", "Thành tiền"),
        };
        for (var column = 1; column <= headers.Length; column++)
        {
            sheet.Cell(ItemsHeaderRow, column).Value = headers[column - 1];
        }
        sheet.Range(ItemsHeaderRow, 1, ItemsHeaderRow, 6).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White).Font.SetFontName("Arial")
            .Fill.SetBackgroundColor(Navy)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        sheet.Row(ItemsHeaderRow).Height = 26;

        var items = quote.Method == "Boq"
            ? quote.Items.OrderBy(item => item.SortOrder).Select(item => new QuoteSpreadsheetLine(
                item.ItemCode, item.Name, item.Unit, item.Quantity, item.UnitPrice, item.Amount)).ToList()
            :
            [new QuoteSpreadsheetLine(
                null,
                quote.PackageDescription ?? text("quotes.method.UnitCost", "Suất đầu tư"),
                "m²",
                quote.AreaSqm ?? 0,
                quote.UnitPricePerSqm ?? 0,
                quote.Subtotal)];

        var row = ItemsHeaderRow + 1;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = SafeText(item.Code);
            sheet.Cell(row, 2).Value = SafeText(item.Name);
            sheet.Cell(row, 3).Value = SafeText(item.Unit);
            sheet.Cell(row, 4).Value = item.Quantity;
            sheet.Cell(row, 5).Value = item.UnitPrice;
            sheet.Cell(row, 6).Value = item.Amount;
            row++;
        }

        var firstItemRow = ItemsHeaderRow + 1;
        var lastRow = Math.Max(firstItemRow, row - 1);
        var detailRange = sheet.Range(firstItemRow, 1, lastRow, 6);
        detailRange.Style
            .Font.SetFontName("Arial").Font.SetFontSize(10)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Hair);
        sheet.Range(firstItemRow, 4, lastRow, 4).Style.NumberFormat.Format = "#,##0.####";
        sheet.Range(firstItemRow, 5, lastRow, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Range(firstItemRow, 4, lastRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        sheet.Range(firstItemRow, 1, lastRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
        return lastRow;
    }

    private static void BuildTotals(
        IXLWorksheet sheet,
        QuoteResponse quote,
        int startRow,
        Func<string, string, string> text)
    {
        var discountAmount = decimal.Round(quote.Subtotal * quote.DiscountPercent / 100m, 2);
        var vatAmount = decimal.Round((quote.Subtotal - discountAmount) * quote.VatPercent / 100m, 2);
        var rows = new[]
        {
            (text("quotes.excel.subtotal", "Tạm tính"), quote.Subtotal),
            ($"{text("quotes.excel.discount", "Chiết khấu")} ({quote.DiscountPercent:0.##}%)", -discountAmount),
            ($"{text("quotes.excel.vat", "VAT")} ({quote.VatPercent:0.##}%)", vatAmount),
            (text("quotes.excel.grandTotal", "TỔNG CỘNG"), quote.GrandTotal),
        };

        for (var index = 0; index < rows.Length; index++)
        {
            var row = startRow + index;
            sheet.Range(row, 1, row, 5).Merge().Value = rows[index].Item1;
            sheet.Cell(row, 6).Value = rows[index].Item2;
            sheet.Range(row, 1, row, 6).Style
                .Font.SetFontName("Arial").Font.SetFontSize(10)
                .Border.SetBottomBorder(XLBorderStyleValues.Hair);
            sheet.Range(row, 1, row, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }

        var grandTotalRow = startRow + rows.Length - 1;
        sheet.Range(grandTotalRow, 1, grandTotalRow, 6).Style
            .Font.SetBold().Font.SetFontColor(BrandRed).Font.SetFontSize(12)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#FFF1F2"))
            .Border.SetTopBorder(XLBorderStyleValues.Medium)
            .Border.SetBottomBorder(XLBorderStyleValues.Double);

        var wordsRow = grandTotalRow + 1;
        sheet.Range(wordsRow, 1, wordsRow, 6).Merge().Value =
            $"{text("quotes.field.grandTotalInWords", "Bằng chữ")}: {SafeText(quote.GrandTotalInWords)}";
        sheet.Range(wordsRow, 1, wordsRow, 6).Style
            .Font.SetItalic().Font.SetFontName("Arial").Font.SetFontSize(10)
            .Alignment.SetWrapText();

        if (!string.IsNullOrWhiteSpace(quote.Note))
        {
            var noteRow = wordsRow + 2;
            sheet.Range(noteRow, 1, noteRow, 6).Merge().Value =
                $"{text("quotes.field.note", "Ghi chú")}: {SafeText(quote.Note)}";
            sheet.Range(noteRow, 1, noteRow, 6).Style
                .Fill.SetBackgroundColor(LightGray)
                .Font.SetFontName("Arial").Font.SetFontSize(10)
                .Alignment.SetWrapText();
        }
    }

    private static void ApplyPageLayout(IXLWorksheet sheet, int lastRow)
    {
        sheet.Column(1).Width = 16;
        sheet.Column(2).Width = 44;
        sheet.Column(3).Width = 12;
        sheet.Column(4).Width = 16;
        sheet.Column(5).Width = 19;
        sheet.Column(6).Width = 21;
        sheet.Range(1, 1, lastRow, 6).Style.Font.SetFontName("Arial");
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.Margins.SetLeft(0.3).SetRight(0.3).SetTop(0.4).SetBottom(0.4);
        sheet.PageSetup.PrintAreas.Add($"A1:F{lastRow}");
    }

    private static void SetLabelValue(IXLWorksheet sheet, int row, int column, string label, object? value)
    {
        sheet.Cell(row, column).Value = label;
        sheet.Cell(row, column).Style.Font.SetBold().Font.SetFontColor(XLColor.FromHtml("#6B7280"));
        var valueCell = sheet.Cell(row, column + 1);
        if (value is DateTime dateTime)
        {
            valueCell.Value = dateTime;
            valueCell.Style.DateFormat.Format = "dd/mm/yyyy";
        }
        else if (value is DateOnly date)
        {
            valueCell.Value = date.ToDateTime(TimeOnly.MinValue);
            valueCell.Style.DateFormat.Format = "dd/mm/yyyy";
        }
        else if (value is decimal decimalValue)
        {
            valueCell.Value = decimalValue;
            valueCell.Style.NumberFormat.Format = "#,##0.00";
        }
        else if (value is int intValue)
        {
            valueCell.Value = intValue;
        }
        else
        {
            valueCell.Value = value is null ? "-" : SafeText(value.ToString());
        }
        if (column == 1) sheet.Range(row, 2, row, 3).Merge();
        if (column == 4) sheet.Range(row, 5, row, 6).Merge();
    }

    private static string SafeText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? $"'{text}" : text;
    }

    private static string SafeWorksheetName(string? value)
    {
        var sanitized = new string((value ?? string.Empty)
            .Where(character => character is not ':' and not '\\' and not '/' and not '?' and not '*'
                and not '[' and not ']')
            .ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Quotation";
        return sanitized[..Math.Min(31, sanitized.Length)];
    }

    private sealed record QuoteSpreadsheetLine(
        string? Code,
        string Name,
        string Unit,
        decimal Quantity,
        decimal UnitPrice,
        decimal Amount);
}
