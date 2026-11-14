using ClosedXML.Excel;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectBoqDesignPermitTemplateTests
{
    private const string SheetName = "Bao gia";
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("THIET-KE-XIN-PHEP")!;

    private static byte[] Workbook(Action<IXLWorksheet>? change = null)
    {
        using var book = new XLWorkbook(new MemoryStream(ProjectBoqTemplateCatalog.ReadFile(Template)));
        change?.Invoke(book.Worksheet(SheetName));
        book.RecalculateAllFormulas();
        using var output = new MemoryStream();
        book.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = true });
        return output.ToArray();
    }

    private static void Priced(IXLWorksheet sheet)
    {
        sheet.Cell("G31").Value = 0; // Concept design is free; its amount cell shows "-".
        sheet.Cell("F32").Value = 2000;
        sheet.Cell("G32").Value = 50000;
        sheet.Cell("F33").Value = 2000;
        sheet.Cell("G33").Value = 80000;
        foreach (var row in new[] { 38, 39, 40, 41, 43, 44 }) sheet.Cell($"G{row}").Value = row * 1000000;
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Cell}").ToArray();

    [Fact]
    public void Parse_PricedTemplate_ReadsBothFeeTablesUpToTheTotalBeforeVat()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(Priced), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(
            ["S01-R0031", "S01-R0032", "S01-R0033", "S01-R0038", "S01-R0039", "S01-R0040", "S01-R0041", "S01-R0043", "S01-R0044"],
            result.Lines.Select(line => line.ItemCode));
        var concept = result.Lines[0];
        Assert.Equal(("Thiết kế sơ bộ", "set", 1m, 0m, 0m, "Công tác thiết kế"),
            (concept.Description, concept.Unit, concept.Quantity, concept.UnitPrice!.Value, concept.Amount, concept.GroupName));
        Assert.Equal(("Thiết kế cơ sở", "m2", 2000m, 100000000m), (result.Lines[1].Description, result.Lines[1].Unit, result.Lines[1].Quantity, result.Lines[1].Amount));
        Assert.Equal("Các thủ tục xin phép xây dựng", result.Lines[3].GroupName);
        Assert.Equal(("4", "Các thủ tục sau khi hoàn thành xây dựng"), (result.Lines[8].Stt, result.Lines[8].GroupName));
        var fees = 2000m * 50000 + 2000m * 80000 + (38 + 39 + 40 + 41 + 43 + 44) * 1000000m;
        Assert.Equal(new BoqWorkbookSheetSummary(SheetName, 9, 0, 0, 0, fees), Assert.Single(result.Sheets));
    }

    [Fact]
    public void Parse_UntouchedTemplate_ListsFixedSetItemsAsMissingPrices()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(7, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Null(line.UnitPrice));
        Assert.Equal(new BoqWorkbookSheetSummary(SheetName, 7, 2, 7, 0, 0), Assert.Single(result.Sheets));
    }

    [Fact]
    public void Parse_DashInAnAmountCellOfAPricedItem_StillHasToMatchThePrice()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(sheet =>
        {
            Priced(sheet);
            sheet.Cell("G31").Value = 5000000;
        }), Template);

        Assert.Contains("amountMismatch@H31", Issues(result));
    }

    [Fact]
    public void Template_DownloadMatchesItsProfile()
    {
        using var book = new XLWorkbook(new MemoryStream(ProjectBoqTemplateCatalog.ReadFile(Template)));

        Assert.Equal([SheetName], book.Worksheets.Select(sheet => sheet.Name));
        foreach (var header in Template.Sheets[0].Headers)
            Assert.Equal(header.Value, book.Worksheet(SheetName).Cell(header.Key).GetString().Trim());
    }
}
