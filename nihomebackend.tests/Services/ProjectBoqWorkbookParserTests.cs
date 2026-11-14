using System.IO.Compression;
using ClosedXML.Excel;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectBoqWorkbookParserTests
{
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("PCCC-CAN-HO")!;

    private static byte[] Save(XLWorkbook book, bool calculate = true)
    {
        // Excel recalculates and stores formula results when it saves; ClosedXML does so only when asked.
        if (calculate) book.RecalculateAllFormulas();
        using var output = new MemoryStream();
        book.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = calculate });
        return output.ToArray();
    }

    private static byte[] Filled(Action<IXLWorksheet>? change = null, bool calculate = true)
    {
        using var book = new XLWorkbook(new MemoryStream(ProjectBoqTemplateCatalog.ReadFile(Template)));
        var sheet = book.Worksheet("ptct");
        sheet.Cell("E16").Value = 1;
        sheet.Cell("F16").Value = 8600000;
        sheet.Cell("E17").Value = 50;
        sheet.Cell("F17").Value = 420000;
        sheet.Cell("E32").Value = 70;
        sheet.Cell("E29").Value = 1;
        sheet.Cell("F29").Value = 24000000;
        sheet.Cell("E95").Value = 2;
        sheet.Cell("F95").Value = 7500000;
        change?.Invoke(sheet);
        return Save(book, calculate);
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Cell}").ToArray();

    [Fact]
    public void Parse_FilledTemplate_ReadsItemsGroupsAndTotalsFromTheCustomerLayout()
    {
        var result = ProjectBoqWorkbookParser.Parse(Filled(), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(["S01-R0016", "S01-R0017", "S01-R0029", "S01-R0032", "S01-R0095"], result.Lines.Select(line => line.ItemCode));
        var first = result.Lines[0];
        Assert.Equal(("ptct", 16, "1", "Trung tâm báo cháy 8 zone", "bộ", 1m, 8600000m), (first.SheetName, first.SourceRow, first.Stt, first.Description, first.Unit, first.Quantity, first.UnitPrice!.Value));
        Assert.Equal("HỆ THỐNG BÁO CHÁY TỰ ĐỘNG", first.GroupName);
        Assert.Equal("HỆ THỐNG CHỮA CHÁY TỰ ĐỘNG", result.Lines[3].GroupName);
        Assert.Equal("CỬA CHỐNG CHÁY", result.Lines[4].GroupName);
        Assert.Null(result.Lines[3].UnitPrice);
        var sheet = Assert.Single(result.Sheets);
        Assert.Equal(new BoqWorkbookSheetSummary("ptct", 5, 73, 1, 1, 8600000 + 21000000 + 24000000 + 15000000), sheet);
    }

    [Theory]
    [InlineData("E16", "1,5", "number@E16")]
    [InlineData("E16", -1, "negative@E16")]
    [InlineData("F16", "liên hệ", "number@F16")]
    public void Parse_BadCellOnItemRow_ReportsThatCell(string cell, object value, string expected)
    {
        var bytes = Filled(sheet =>
        {
            if (value is string text) sheet.Cell(cell).Value = text;
            else sheet.Cell(cell).Value = Convert.ToDouble(value);
        });

        Assert.Contains(expected, Issues(ProjectBoqWorkbookParser.Parse(bytes, Template)));
    }

    [Fact]
    public void Parse_AmountOrTotalNotMatchingTheItems_IsRejected()
    {
        var amount = ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("G16").Value = 1), Template);
        Assert.Contains("amountMismatch@G16", Issues(amount));

        // A row added below the SUM range leaves the sheet total short of the item amounts.
        var total = ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("G30").FormulaA1 = "SUM(G16:G20)"), Template);
        Assert.Equal(["totalMismatch@G101"], Issues(total));
    }

    [Fact]
    public void Parse_FormulaWithoutStoredResultOrWithExcelError_IsRejected()
    {
        var notCalculated = ProjectBoqWorkbookParser.Parse(Filled(calculate: false), Template);
        Assert.Contains("formulaNotCalculated@G16", Issues(notCalculated));

        var excelError = ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("E18").FormulaA1 = "1/0"), Template);
        Assert.Contains("formulaError@E18", Issues(excelError));
    }

    [Fact]
    public void Parse_QuantityOnRowWithoutStt_IsNotDroppedSilently()
    {
        var result = ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("E94").Value = 3), Template);
        Assert.Equal(["sttRequired@A94"], Issues(result));
    }

    [Fact]
    public void Parse_LayoutDifferentFromTheTemplate_IsRejected()
    {
        Assert.Equal(["headerMismatch@E15"], Issues(ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("E15").Value = "SL"), Template)));
        Assert.Equal(["sheetMissing@", "unknownSheet@"], Issues(ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Name = "Khac"), Template)).OrderBy(code => code));
        Assert.Contains("totalRowMissing@", Issues(ProjectBoqWorkbookParser.Parse(Filled(sheet => sheet.Cell("B101").Value = "Tổng"), Template)));
    }

    [Fact]
    public void Parse_TemplateWithoutQuantities_HasNoRows()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);
        Assert.Equal(["noRows@"], Issues(result));
    }

    [Theory]
    [InlineData("xl/externalLinks/externalLink1.xml")]
    [InlineData("xl/connections.xml")]
    [InlineData("xl/vbaProject.bin")]
    public void Parse_WorkbookWithActiveContent_IsRejectedBeforeReading(string part)
    {
        using var output = new MemoryStream();
        output.Write(Filled());
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true)) zip.CreateEntry(part);

        var error = Assert.Throws<BoqWorkbookException>(() => ProjectBoqWorkbookParser.Parse(output.ToArray(), Template));
        Assert.Equal("unsafeFile", error.Code);
    }

    [Fact]
    public void Parse_NotAnExcelFile_IsRejected()
    {
        var error = Assert.Throws<BoqWorkbookException>(() => ProjectBoqWorkbookParser.Parse([1, 2, 3], Template));
        Assert.Equal("file", error.Code);
    }

    [Fact]
    public void Template_DownloadIsTheApprovedFileAndMatchesItsProfile()
    {
        var bytes = ProjectBoqTemplateCatalog.ReadFile(Template);
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["ptct"], book.Worksheets.Select(sheet => sheet.Name));
        foreach (var header in Template.Sheets[0].Headers)
            Assert.Equal(header.Value, book.Worksheet("ptct").Cell(header.Key).GetString().Trim());
    }
}
