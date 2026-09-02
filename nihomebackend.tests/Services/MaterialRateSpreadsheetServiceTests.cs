using System.IO.Compression;
using ClosedXML.Excel;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class MaterialRateSpreadsheetServiceTests
{
    private readonly MaterialRateSpreadsheetService _sut = new();

    [Fact]
    public void CreateTemplate_SeparatesBlankEntryFormFromExamples()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>
        {
            ["materialRates.excel.entrySheet"] = "Nhập liệu",
            ["materialRates.excel.guideSheet"] = "Hướng dẫn & ví dụ",
            ["materialRates.excel.title"] = "BIỂU MẪU KHÁCH HÀNG",
        });

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var entry = workbook.Worksheet("Nhập liệu");

        Assert.Equal("BIỂU MẪU KHÁCH HÀNG", entry.Cell("A1").GetString());
        Assert.True(entry.Cell("A5").IsEmpty());
        Assert.True(entry.Cell("G5").HasFormula);
        Assert.Equal(XLWorksheetVisibility.VeryHidden, workbook.Worksheet("_NICON").Visibility);
        Assert.Contains("VL-XM-PC40", workbook.Worksheet("Hướng dẫn & ví dụ").CellsUsed().Select(cell => cell.GetString()));
    }

    [Fact]
    public void Parse_ReadsOnlyCustomerEntryRows()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>());
        using var source = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(source);
        var entry = workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible);
        entry.Cell("A5").Value = "VL-01";
        entry.Cell("B5").Value = "Xi măng";
        entry.Cell("C5").Value = "kg";
        entry.Cell("D5").Value = 2.5;
        entry.Cell("E5").Value = 15000;
        entry.Cell("F5").Value = 5;
        using var completed = new MemoryStream();
        workbook.SaveAs(completed);
        completed.Position = 0;

        var result = _sut.Parse(completed);

        Assert.True(result.IsValid);
        var row = Assert.Single(result.Rows);
        Assert.Equal(5, Assert.Single(result.SourceRowNumbers));
        Assert.Equal("VL-01", row["MaterialCode"]);
        Assert.Equal("2.5", row["NormPerSqm"]);
        Assert.Equal("15000", row["UnitRate"]);
    }

    [Fact]
    public void Parse_RejectsUnrelatedWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sheet1");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = _sut.Parse(stream);

        Assert.Equal("materialRates.excel.invalidTemplate", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsChangedColumnStructure()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>());
        using var source = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(source);
        workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible)
            .Cell("D4").Value = "Đơn giá";
        using var changed = new MemoryStream();
        workbook.SaveAs(changed);
        changed.Position = 0;

        var result = _sut.Parse(changed);

        Assert.Equal("materialRates.excel.invalidStructure", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsInvestmentRateWorkbookForBoqCatalog()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>(), MaterialRateCatalogType.InvestmentRate);
        using var stream = new MemoryStream(bytes);

        var result = _sut.Parse(stream, MaterialRateCatalogType.Boq);

        Assert.False(result.IsValid);
        Assert.Equal("materialRates.excel.wrongCatalogType", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsReorderedColumns()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>());
        using var source = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(source);
        var entry = workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible);
        var normHeader = entry.Cell("D4").Value;
        entry.Cell("D4").Value = entry.Cell("E4").Value;
        entry.Cell("E4").Value = normHeader;
        using var changed = new MemoryStream();
        workbook.SaveAs(changed);
        changed.Position = 0;

        var result = _sut.Parse(changed);

        Assert.Equal("materialRates.excel.invalidStructure", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsFirstRowBeyondTwoThousandEntries()
    {
        var bytes = _sut.CreateTemplate(new Dictionary<string, string>());
        using var source = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(source);
        workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible)
            .Cell("A2005").Value = "VL-OVERFLOW";
        using var overflow = new MemoryStream();
        workbook.SaveAs(overflow);
        overflow.Position = 0;

        var result = _sut.Parse(overflow);

        var error = Assert.Single(result.Errors);
        Assert.Equal("materialRates.excel.maxRows", error.MessageKey);
        Assert.Equal(2005, error.Row);
    }

    [Fact]
    public void Parse_RejectsCorruptExcelFile()
    {
        using var stream = new MemoryStream("not an Excel workbook"u8.ToArray());

        var result = _sut.Parse(stream);

        Assert.Equal("materialRates.excel.invalidFile", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsWorkbookWithTooManyArchiveEntries()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index < 101; index++)
            {
                archive.CreateEntry($"entry-{index}.xml");
            }
        }
        stream.Position = 0;

        var result = _sut.Parse(stream);

        Assert.Equal("materialRates.excel.invalidFile", Assert.Single(result.Errors).MessageKey);
    }

    [Fact]
    public void Parse_RejectsWorkbookWithExcessiveExpandedSize()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("large.xml", CompressionLevel.Optimal);
            using var content = entry.Open();
            var chunk = new byte[1024 * 1024];
            for (var index = 0; index < 26; index++)
            {
                content.Write(chunk);
            }
        }
        stream.Position = 0;

        var result = _sut.Parse(stream);

        Assert.Equal("materialRates.excel.invalidFile", Assert.Single(result.Errors).MessageKey);
    }
}