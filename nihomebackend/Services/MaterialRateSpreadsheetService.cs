using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IMaterialRateSpreadsheetService
{
    byte[] CreateTemplate(IReadOnlyDictionary<string, string> text);
    CsvImportResult Parse(Stream stream);
}

public sealed class MaterialRateSpreadsheetService : IMaterialRateSpreadsheetService
{
    private const string TemplateMarker = "NICON_MATERIAL_RATE_TEMPLATE";
    private const int HeaderRow = 4;
    private const int FirstDataRow = 5;
    private const int LastDataRow = 2004;
    private const int MaximumArchiveEntries = 100;
    private const long MaximumExpandedBytes = 25 * 1024 * 1024;

    public byte[] CreateTemplate(IReadOnlyDictionary<string, string> text)
    {
        string T(string key, string fallback) => text.GetValueOrDefault(key, fallback);
        using var workbook = new XLWorkbook();
        var entrySheet = workbook.Worksheets.Add(T("materialRates.excel.entrySheet", "Nhập liệu"));
        var guideSheet = workbook.Worksheets.Add(T("materialRates.excel.guideSheet", "Hướng dẫn & ví dụ"));
        var metadataSheet = workbook.Worksheets.Add("_NICON");

        BuildEntrySheet(entrySheet, T);
        BuildGuideSheet(guideSheet, T);
        metadataSheet.Cell("A1").Value = TemplateMarker;
        metadataSheet.Cell("A2").Value = "1";
        metadataSheet.Cell("A3").Value = entrySheet.Name;
        for (var index = 0; index < MaterialRateService.CsvHeaders.Count; index++)
        {
            metadataSheet.Cell(index + 1, 2).Value = MaterialRateService.CsvHeaders[index];
            metadataSheet.Cell(index + 1, 3).Value = entrySheet.Cell(HeaderRow, index + 1).GetString();
        }
        metadataSheet.Visibility = XLWorksheetVisibility.VeryHidden;

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    public CsvImportResult Parse(Stream stream)
    {
        try
        {
            using var input = CopyAndValidateArchive(stream);
            using var workbook = new XLWorkbook(input);
            var metadata = workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == "_NICON");
            if (metadata?.Cell("A1").GetString() != TemplateMarker || metadata.Cell("A2").GetString() != "1")
            {
                return Invalid("materialRates.excel.invalidTemplate", "Tệp Excel không phải biểu mẫu đơn giá NICON hợp lệ.");
            }

            var entrySheetName = metadata.Cell("A3").GetString();
            var entrySheet = workbook.Worksheets.FirstOrDefault(sheet => sheet.Name == entrySheetName);
            if (entrySheet is null || !HasExpectedColumns(entrySheet, metadata))
            {
                return Invalid("materialRates.excel.invalidStructure", "Cấu trúc cột của biểu mẫu Excel đã bị thay đổi.");
            }

            var overflowRow = entrySheet.RowsUsed(row => row.RowNumber() > LastDataRow)
                .FirstOrDefault(row => Enumerable.Range(1, 6).Any(column => !row.Cell(column).IsEmpty()));
            if (overflowRow is not null)
            {
                return new CsvImportResult
                {
                    Errors = [new CsvImportError
                    {
                        Row = overflowRow.RowNumber(),
                        Message = "Biểu mẫu chỉ được chứa tối đa 2.000 dòng dữ liệu.",
                        MessageKey = "materialRates.excel.maxRows",
                        MessageArgs = new() { ["max"] = 2000 },
                    }],
                };
            }

            var result = new CsvImportResult();
            result.Headers.AddRange(MaterialRateService.CsvHeaders);
            for (var rowNumber = FirstDataRow; rowNumber <= LastDataRow; rowNumber++)
            {
                var values = Enumerable.Range(1, 6)
                    .Select(column => ReadCell(entrySheet.Cell(rowNumber, column)))
                    .ToArray();
                if (values.All(string.IsNullOrWhiteSpace)) continue;

                result.Rows.Add(MaterialRateService.CsvHeaders
                    .Select((header, index) => (header, value: values[index]))
                    .ToDictionary(item => item.header, item => item.value, StringComparer.Ordinal));
                result.SourceRowNumbers.Add(rowNumber);
            }
            return result;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Invalid("materialRates.excel.invalidFile", "Không thể đọc tệp Excel. Hãy tải lại biểu mẫu NICON và không thay đổi cấu trúc tệp.");
        }
    }

    private static void BuildEntrySheet(IXLWorksheet sheet, Func<string, string, string> text)
    {
        sheet.ShowGridLines = false;
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.Range("A1:G1").Merge().Value = text("materialRates.excel.title", "BIỂU MẪU ĐỊNH MỨC VÀ ĐƠN GIÁ VẬT LIỆU");
        sheet.Range("A2:G2").Merge().Value = text("materialRates.excel.entryHint", "Nhập mỗi vật liệu trên một dòng. Không thêm, xóa hoặc đổi thứ tự cột.");
        sheet.Range("A1:G1").Style
            .Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#17365D"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Row(1).Height = 30;
        sheet.Range("A2:G2").Style
            .Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#44546A"))
            .Fill.SetBackgroundColor(XLColor.FromHtml("#D9EAF7"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        sheet.Row(2).Height = 24;

        var headers = new[]
        {
            text("materialRates.excel.header.code", "Mã vật liệu *"),
            text("materialRates.excel.header.name", "Tên vật liệu *"),
            text("materialRates.excel.header.unit", "Đơn vị *"),
            text("materialRates.excel.header.norm", "Định mức / m² *"),
            text("materialRates.excel.header.rate", "Đơn giá *"),
            text("materialRates.excel.header.waste", "Hao hụt (%) *"),
            text("materialRates.excel.header.amount", "Thành tiền / m²"),
        };
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(HeaderRow, index + 1).Value = headers[index];
        }
        sheet.Range(HeaderRow, 1, HeaderRow, 7).Style
            .Font.SetBold().Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#2F75B5"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        sheet.Row(HeaderRow).Height = 32;

        var inputRange = sheet.Range(FirstDataRow, 1, LastDataRow, 6);
        inputRange.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFFDEB"));
        inputRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Hair);
        inputRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Hair);
        sheet.Range(FirstDataRow, 7, LastDataRow, 7).Style
            .Fill.SetBackgroundColor(XLColor.FromHtml("#E2F0D9"))
            .Border.SetOutsideBorder(XLBorderStyleValues.Hair)
            .Border.SetInsideBorder(XLBorderStyleValues.Hair);
        sheet.Range(FirstDataRow, 4, LastDataRow, 4).Style.NumberFormat.Format = "0.######";
        sheet.Range(FirstDataRow, 5, LastDataRow, 5).Style.NumberFormat.Format = "#,##0.####";
        sheet.Range(FirstDataRow, 6, LastDataRow, 6).Style.NumberFormat.Format = "0.####";
        sheet.Range(FirstDataRow, 7, LastDataRow, 7).Style.NumberFormat.Format = "#,##0.####";
        for (var row = FirstDataRow; row <= LastDataRow; row++)
        {
            sheet.Cell(row, 7).FormulaA1 = $"=IF(COUNTA(A{row}:F{row})=0,\"\",D{row}*E{row}*(1+F{row}/100))";
        }

        sheet.Column(1).Width = 20;
        sheet.Column(2).Width = 36;
        sheet.Column(3).Width = 14;
        sheet.Column(4).Width = 19;
        sheet.Column(5).Width = 18;
        sheet.Column(6).Width = 18;
        sheet.Column(7).Width = 21;
        sheet.Range(HeaderRow, 1, LastDataRow, 7).SetAutoFilter();
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.PrintAreas.Add($"A1:G{LastDataRow}");
    }

    private static void BuildGuideSheet(IXLWorksheet sheet, Func<string, string, string> text)
    {
        sheet.ShowGridLines = false;
        sheet.Range("A1:G1").Merge().Value = text("materialRates.excel.guideTitle", "HƯỚNG DẪN SỬ DỤNG BIỂU MẪU");
        sheet.Range("A1:G1").Style
            .Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml("#17365D"));
        var instructions = new[]
        {
            text("materialRates.package.step1", "1. Mở biểu mẫu bằng Excel, Numbers hoặc Google Sheets."),
            text("materialRates.package.step2", "2. Nhập mỗi vật liệu trên một dòng tại trang Nhập liệu; không sửa tên hoặc thứ tự cột."),
            text("materialRates.package.step3", "3. Dùng số không có dấu phân cách hàng nghìn; cột Thành tiền được tự động tính."),
            text("materialRates.package.step4", "4. Lưu nguyên định dạng Excel và gửi lại tệp cho NICON để nhập dữ liệu."),
            text("materialRates.package.important1", "Tệp hợp lệ sẽ thay thế toàn bộ dữ liệu trong phiên bản Nháp được chọn."),
            text("materialRates.package.important2", "Nếu bất kỳ dòng nào sai, hệ thống không lưu dòng nào và trả về vị trí cần sửa."),
        };
        for (var index = 0; index < instructions.Length; index++)
        {
            sheet.Range(index + 3, 1, index + 3, 7).Merge().Value = instructions[index];
        }
        sheet.Range("A3:G8").Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        sheet.Range("A3:G8").Style.Fill.SetBackgroundColor(XLColor.FromHtml("#D9EAF7"));
        sheet.Row(8).Height = 34;

        sheet.Range("A10:G10").Merge().Value = text("materialRates.excel.exampleTitle", "VÍ DỤ THAM KHẢO — KHÔNG CẦN XÓA");
        sheet.Range("A10:G10").Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#E2F0D9"));
        var headers = new[]
        {
            text("materialRates.excel.header.code", "Mã vật liệu *"),
            text("materialRates.excel.header.name", "Tên vật liệu *"),
            text("materialRates.excel.header.unit", "Đơn vị *"),
            text("materialRates.excel.header.norm", "Định mức / m² *"),
            text("materialRates.excel.header.rate", "Đơn giá *"),
            text("materialRates.excel.header.waste", "Hao hụt (%) *"),
            text("materialRates.excel.header.amount", "Thành tiền / m²"),
        };
        var examples = new object[][]
        {
            ["VL-XM-PC40", text("materialRates.excel.example.cement", "Xi măng Portland PCB40"), "kg", 12.5, 1850, 3, 23818.75],
            ["VL-CAT-01", text("materialRates.excel.example.sand", "Cát xây tô"), "m3", 0.025, 420000, 5, 11025],
            ["VL-GACH-01", text("materialRates.excel.example.brick", "Gạch ống 8x8x18"), text("materialRates.excel.example.piece", "viên"), 68, 1450, 4, 102544],
        };
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(11, column + 1).Value = headers[column];
        }
        for (var row = 0; row < examples.Length; row++)
        {
            for (var column = 0; column < examples[row].Length; column++)
            {
                sheet.Cell(row + 12, column + 1).Value = XLCellValue.FromObject(examples[row][column]);
            }
        }
        sheet.Range("A11:G11").Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2F75B5"));
        sheet.Range("A11:G14").Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Thin);
        sheet.Columns(1, 7).AdjustToContents(1, 14);
    }

    private static string ReadCell(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        if (cell.TryGetValue<decimal>(out var number)) return number.ToString(CultureInfo.InvariantCulture);
        return cell.GetString().Trim();
    }

    private static bool HasExpectedColumns(IXLWorksheet sheet, IXLWorksheet metadata)
    {
        for (var index = 0; index < MaterialRateService.CsvHeaders.Count; index++)
        {
            if (metadata.Cell(index + 1, 2).GetString() != MaterialRateService.CsvHeaders[index] ||
                metadata.Cell(index + 1, 3).GetString() != sheet.Cell(HeaderRow, index + 1).GetString())
            {
                return false;
            }
        }
        return true;
    }

    private static MemoryStream CopyAndValidateArchive(Stream stream)
    {
        var input = new MemoryStream();
        stream.CopyTo(input);
        input.Position = 0;
        using (var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        {
            if (archive.Entries.Count > MaximumArchiveEntries ||
                archive.Entries.Sum(entry => entry.Length) > MaximumExpandedBytes)
            {
                throw new InvalidDataException("Workbook archive exceeds the supported limits.");
            }
        }
        input.Position = 0;
        return input;
    }

    private static CsvImportResult Invalid(string key, string message) => new()
    {
        Errors = [new CsvImportError { Message = message, MessageKey = key }],
    };
}