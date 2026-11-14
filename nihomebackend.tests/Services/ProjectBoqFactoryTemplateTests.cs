using ClosedXML.Excel;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// GNF factory BOQ: 20 priced sheets, totals at the top, headings numbered and items often not.
public sealed class ProjectBoqFactoryTemplateTests
{
    private const string Factory = "2.Nhà Xưởng & Văn Phòng", Maintenance = "3.Nhà Bảo Trì, Rang Đậu, BB",
        Civil = "B.Hạ Tầng Kỹ Thuật", Fire = "C3.Hệ Thống PCCC ", Electric = "C4.Hệ Thống Điện & Điện Nhẹ ";
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("NHA-XUONG")!;

    private static byte[] Workbook(Action<XLWorkbook> change)
    {
        using var book = new XLWorkbook(new MemoryStream(ProjectBoqTemplateCatalog.ReadFile(Template)));
        change(book);
        book.RecalculateAllFormulas();
        using var output = new MemoryStream();
        book.SaveAs(output, new SaveOptions { EvaluateFormulasBeforeSaving = true });
        return output.ToArray();
    }

    private static void Priced(XLWorkbook book)
    {
        void Set(string sheet, string cell, double value) => book.Worksheet(sheet).Cell(cell).Value = value;
        Set(Factory, "E30", 7470.6892); // Excavation; the sheet rounds each amount to whole dong.
        Set(Factory, "F30", 12345);
        Set(Factory, "G30", 1000);
        Set(Factory, "E93", 1); // Scaffolding, which the customer's file left without formulas.
        Set(Factory, "G93", 2000000);
        Set(Maintenance, "E119", 150.8333);
        Set(Maintenance, "F119", 40000);
        Set(Civil, "E55", 41); // Manhole count on a heading: not a priced line.
        Set(Civil, "E56", 229.369088);
        Set(Civil, "G56", 85000);
        Set(Fire, "E24", 35); // Fire protection keeps unrounded amounts.
        Set(Fire, "F24", 333333.33);
        Set(Electric, "E19", 1); // Electrical items have no STT; the sheet total rounds to 100,000.
        Set(Electric, "F19", 123456789);
        Set(Electric, "E39", 1); // An item the customer gave no unit.
        Set(Electric, "F39", 10000000);
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Sheet}!{issue.Cell}").ToArray();

    [Fact]
    public void Parse_UntouchedTemplate_MatchesEveryHeaderAndTotalButHasNoQuantities()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);

        Assert.Equal(["noRows@!"], Issues(result));
        Assert.Equal(Template.Sheets.Select(sheet => sheet.Name), result.Sheets.Select(sheet => sheet.Name));
        Assert.Equal(1845, result.Sheets.Sum(sheet => sheet.SkippedCount));
        Assert.All(result.Sheets, sheet => Assert.Equal(0, sheet.Total));
    }

    [Fact]
    public void Parse_PricedWorkbook_ReadsUnnumberedItemsAndAddsUpToEachSheetTotal()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(Priced), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(
            ["S02-R0030", "S02-R0093", "S03-R0119", "S16-R0056", "S19-R0024", "S20-R0019", "S20-R0039"],
            result.Lines.Select(line => line.ItemCode));
        var excavation = result.Lines[0];
        Assert.Equal(("1", "EARTH WORK / CÔNG TÁC ĐẤT", "Excavation (by Equipment)\nĐào đất bằng máy", "m3", 7470.6892m, 13345m, 99696347m),
            (excavation.Stt, excavation.GroupName, excavation.Description, excavation.Unit, excavation.Quantity, excavation.UnitPrice!.Value, excavation.Amount));
        Assert.Equal((2000000m, "BRICK WORK / CÔNG TÁC XÂY"), (result.Lines[1].Amount, result.Lines[1].GroupName));
        Assert.Equal(("Downspout elbow/Đai kẹp ống", 6033332m), (result.Lines[2].Description, result.Lines[2].Amount));
        Assert.Equal(("Manhole type MH-1/ Hố ga loại MH-1", 19496372m), (result.Lines[3].GroupName, result.Lines[3].Amount));
        Assert.Equal(("Nhà xưởng + văn phòng tầng 1", 11666666.55m), (result.Lines[4].GroupName, result.Lines[4].Amount));
        var recloser = result.Lines[5];
        Assert.Equal(("", "POWER INCOMING WORK/ ĐIỆN ĐẦU VÀO", "Recloser 3P-24kV-630A", "Bộ\nPcs"),
            (recloser.Stt, recloser.GroupName, recloser.Description, recloser.Unit));
        Assert.Equal(("Tu trung thế", ""), (result.Lines[6].Description, result.Lines[6].Unit));

        var sheets = result.Sheets.ToDictionary(sheet => sheet.Name);
        Assert.Equal(new BoqWorkbookSheetSummary(Factory, 2, 169, 0, 0, 101696347m), sheets[Factory]);
        Assert.Equal(new BoqWorkbookSheetSummary(Electric, 2, 336, 0, 1, 133456789m), sheets[Electric]);
        Assert.Equal(99696347m + 2000000 + 6033332 + 19496372 + 11666666.55m + 133456789, result.Lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Parse_LongDecimalQuantityAtAHighPrice_MatchesTheAmountExcelComputed()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            book.Worksheet(Civil).Cell("E22").Value = 126.40802308; // Rebar in tons.
            book.Worksheet(Civil).Cell("F22").Value = 20000000;
        }), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        var rebar = Assert.Single(result.Lines);
        Assert.Equal((126.408023m, 2528160462m), (rebar.Quantity, rebar.Amount));
    }

    [Fact]
    public void Parse_SheetTotalThatDoesNotAddUp_IsReportedAtItsTotalCell()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            book.Worksheet(Factory).Cell("J6").Value = 99696347;
        }), Template);

        Assert.Equal([$"totalMismatch@{Factory}!J6"], Issues(result));
    }

    [Fact]
    public void Parse_AmountThatDoesNotMatchQuantityTimesPrice_IsReported()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            book.Worksheet(Factory).Cell("J30").Value = 99000000;
        }), Template);

        Assert.Contains($"amountMismatch@{Factory}!J30", Issues(result));
    }

    [Fact]
    public void Outline_KeepsCoverAndSummaryAsPlainSheets_AndMarksHeadingsSubtotalsAndItems()
    {
        var outline = ProjectBoqTemplateCatalog.Outline(Template);

        Assert.Equal(["Cover", "Cover 1", "00.Tổng Hợp"], outline.Where(sheet => !sheet.IsData).Select(sheet => sheet.Name));
        Assert.Equal(20, outline.Count(sheet => sheet.IsData));
        var electric = outline.Single(sheet => sheet.Name == Electric);
        Assert.Equal(new BoqOutlineRow(17, "E.1", "POWER INCOMING WORK/ ĐIỆN ĐẦU VÀO", null, "group"), electric.Rows[0]);
        Assert.Equal(new BoqOutlineRow(18, null, "[ Sub - total ]", null, "subtotal"), electric.Rows[1]);
        Assert.Equal(new BoqOutlineRow(19, null, "Recloser 3P-24kV-630A", "Bộ\nPcs", "item"), electric.Rows[2]);
        var civil = outline.Single(sheet => sheet.Name == Civil);
        Assert.Contains(new BoqOutlineRow(55, "I", "Manhole type MH-1/ Hố ga loại MH-1", null, "group"), civil.Rows);
        Assert.Equal(164, civil.ItemCount);
    }
}
