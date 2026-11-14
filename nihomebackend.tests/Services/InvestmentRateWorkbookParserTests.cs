using ClosedXML.Excel;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// The generic investment-rate forms: section A converts actual areas with a factor, section B prices
// each work group per m² of converted area. The parser reads the input cells and computes every total.
public sealed class InvestmentRateWorkbookParserTests
{
    private const string Sheet = "Suat dau tu";
    private static readonly InvestmentRateTemplate Factory = InvestmentRateTemplateCatalog.Find("SDT-NHA-XUONG")!;

    // Factory form layout: areas on rows 12-15 (total 16), work groups on rows 20-31 (total 32).
    private static byte[] Filled(InvestmentRateTemplate template, Action<IXLWorksheet> change)
    {
        using var workbook = new XLWorkbook(new MemoryStream(InvestmentRateTemplateCatalog.ReadFile(template)));
        change(workbook.Worksheet(Sheet));
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static string[] Issues(InvestmentRateParseResult result) =>
        result.Report.Issues.Select(issue => $"{issue.Code}@{issue.Cell}").ToArray();

    [Theory]
    [InlineData("SDT-NHA-XUONG", 4, 12)]
    [InlineData("SDT-VAN-PHONG", 8, 12)]
    [InlineData("SDT-NOI-THAT", 4, 11)]
    public void EveryBlankForm_ReadsItsRowsWithZeroTotals(string code, int areas, int groups)
    {
        var template = InvestmentRateTemplateCatalog.Find(code)!;

        var result = InvestmentRateWorkbookParser.Parse(InvestmentRateTemplateCatalog.ReadFile(template), template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(areas, result.Areas.Count);
        Assert.Equal(groups, result.Groups.Count);
        Assert.Equal(0m, result.ConvertedArea);
        Assert.Equal(0m, result.RatePerSqm);
        Assert.Equal(groups, result.Groups.Select(group => group.Code).Distinct().Count());
    }

    [Fact]
    public void FilledForm_ConvertsAreasAndAddsMaterialLabourAndEquipment()
    {
        var bytes = Filled(Factory, sheet =>
        {
            sheet.Cell("E12").Value = 5000; // Factory floor, 100%.
            sheet.Cell("E14").Value = 600;  // Canopy, 50% → 300 m².
            sheet.Cell("E20").Value = 150000;
            sheet.Cell("F20").Value = 50000;
            sheet.Cell("E22").Value = 1200000;
            sheet.Cell("F22").Value = 300000;
            sheet.Cell("G22").Value = 100000;
        });

        var result = InvestmentRateWorkbookParser.Parse(bytes, Factory);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(5300m, result.ConvertedArea);
        Assert.Equal(300m, result.Areas.Single(area => area.SourceRow == 14).ConvertedArea);
        Assert.Equal(1800000m, result.RatePerSqm);
        Assert.Equal(1600000m, result.Groups.Single(group => group.Code == "NX-03").RatePerSqm);
        Assert.Equal(9540000000m, result.Total);
    }

    [Fact]
    public void RowInsertedInsideATable_IsRead()
    {
        var bytes = Filled(Factory, sheet =>
        {
            sheet.Row(31).InsertRowsBelow(1);
            sheet.Cell("B32").Value = "Cổng, hàng rào tạm";
            sheet.Cell("D32").Value = "NX-13";
            sheet.Cell("E32").Value = 25000;
            sheet.Cell("E12").Value = 1000;
        });

        var result = InvestmentRateWorkbookParser.Parse(bytes, Factory);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(13, result.Groups.Count);
        Assert.Equal(25000m, result.RatePerSqm);
    }

    [Fact]
    public void BadCells_AreReportedByCellAndNothingIsTrusted()
    {
        var bytes = Filled(Factory, sheet =>
        {
            sheet.Cell("E12").Value = "năm nghìn";
            sheet.Cell("F13").Value = 3.5; // 350% is above the 300% ceiling.
            sheet.Cell("E20").Value = -1;
            sheet.Cell("D21").Value = "NX-01";
            sheet.Cell("D22").Value = "";
        });

        var result = InvestmentRateWorkbookParser.Parse(bytes, Factory);

        Assert.False(result.IsValid);
        Assert.Equal(
            ["number@E12", "factorRange@F13", "negative@E20", "duplicateCode@D21", "codeRequired@D22"],
            Issues(result));
    }

    [Fact]
    public void FormOfAnotherBuildingType_IsRejected()
    {
        var office = InvestmentRateTemplateCatalog.Find("SDT-VAN-PHONG")!;

        var error = Assert.Throws<BoqWorkbookException>(() =>
            InvestmentRateWorkbookParser.Parse(InvestmentRateTemplateCatalog.ReadFile(office), Factory));

        Assert.Equal("wrongInvestmentRateTemplate", error.Code);
    }

    [Fact]
    public void WorkbookWithoutTheNiconMarker_IsRejected()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add(Sheet).Cell("A1").Value = "Suất đầu tư";
        using var output = new MemoryStream();
        workbook.SaveAs(output);

        var error = Assert.Throws<BoqWorkbookException>(() => InvestmentRateWorkbookParser.Parse(output.ToArray(), Factory));

        Assert.Equal("notInvestmentRateForm", error.Code);
    }

    [Fact]
    public void DeletedTotalLine_IsReported()
    {
        // Excel turns the name of a deleted total line into #REF!, which no longer resolves to a row.
        using var workbook = new XLWorkbook(new MemoryStream(InvestmentRateTemplateCatalog.ReadFile(Factory)));
        workbook.DefinedNames.Delete("SDT_RATE_TOTAL");
        using var output = new MemoryStream();
        workbook.SaveAs(output);

        var result = InvestmentRateWorkbookParser.Parse(output.ToArray(), Factory);

        Assert.Contains("totalRowMissing@", Issues(result));
        Assert.Empty(result.Groups);
    }
}
