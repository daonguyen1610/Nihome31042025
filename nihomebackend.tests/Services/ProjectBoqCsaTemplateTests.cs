using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// CSA WORK: three bills; the customer's file came without quantities or prices, so each item's quantity
// adds up its measured rows (No × L × W × H × HS) and the bills price material and labour & equipment.
public sealed class ProjectBoqCsaTemplateTests
{
    private const string General = "Bill 1- Premilinary & General", Building = "Bill 2-Main building works", External = "Bill 3-External works";
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("CSA-WORK")!;

    private static byte[] Workbook(Action<Dictionary<(string Sheet, string Cell), double>> change)
    {
        var values = new Dictionary<(string Sheet, string Cell), double>();
        change(values);
        return BoqWorkbookPatch.Apply(Template, values);
    }

    private static void Set(Dictionary<(string Sheet, string Cell), double> book, string sheet, string cell, double value) => book[(sheet, cell)] = value;

    private static void Priced(Dictionary<(string Sheet, string Cell), double> book)
    {
        Set(book, General, "D8", 1); // Lump sums come without a quantity.
        Set(book, General, "E8", 50000000);
        Set(book, General, "F8", 50000000);
        Set(book, General, "F5", 50000000);
        // Excavation: 3,634.75515 m³ from the M1-M9 and DK rows, at 100,000 material + 50,000 labour & equipment.
        Set(book, Building, "L15", 100000);
        Set(book, Building, "N15", 50000);
        Set(book, Building, "Q15", 545213272.5);
        Set(book, Building, "Q6", 545213272.5);
        Set(book, External, "L69", 80000); // Fence excavation, 1,016.94 m³.
        Set(book, External, "N69", 40000);
        Set(book, External, "Q69", 122032800);
        Set(book, External, "Q6", 122032800);
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Sheet}!{issue.Cell}").ToArray();

    [Fact]
    public void Parse_UntouchedTemplate_ReadsTheMeasuredQuantitiesWithoutPrices()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(135, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Null(line.UnitPrice));
        Assert.Equal([(General, 0, 51), (Building, 116, 114), (External, 19, 50)],
            result.Sheets.Select(sheet => (sheet.Name, sheet.ItemCount, sheet.SkippedCount)));
        var excavation = result.Lines.First();
        Assert.Equal((Building, 15, "1", "EARTH WORK/ CÔNG TÁC ĐẤT", "m3", 3634.75515m),
            (excavation.SheetName, excavation.SourceRow, excavation.Stt, excavation.GroupName, excavation.Unit, excavation.Quantity));
        Assert.DoesNotContain(result.Lines, line => line.SheetName == Building && line.SourceRow is > 15 and < 28);
    }

    [Fact]
    public void Parse_PricedWorkbook_AddsMaterialAndLabourIntoOneUnitPrice()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(Priced), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        var priced = result.Lines.Where(line => line.UnitPrice.HasValue).ToList();
        Assert.Equal(["S01-R0008", "S02-R0015", "S03-R0069"], priced.Select(line => line.ItemCode));
        Assert.Equal(("1.1", "Groundbreaking", "item", 1m, 50000000m), (priced[0].Stt, priced[0].Description, priced[0].Unit, priced[0].Quantity, priced[0].Amount));
        Assert.Equal((150000m, 545213272.5m), (priced[1].UnitPrice!.Value, priced[1].Amount));
        Assert.Equal((120000m, 122032800m), (priced[2].UnitPrice!.Value, priced[2].Amount));
        Assert.Equal(50000000m + 545213272.5m + 122032800m, result.Lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Parse_AmountThatLeavesOutTheLabourPrice_IsReported()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Building, "Q15", 363475515); // Material only.
            Set(book, Building, "Q6", 363475515);
        }), Template);

        Assert.Equal([$"amountMismatch@{Building}!Q15"], Issues(result));
    }

    [Fact]
    public void Parse_PriceOnAMeasuredRow_AsksForAnItemNumber()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Building, "L16", 1000); // Footing M1, part of the excavation above.
        }), Template);

        Assert.Equal([$"sttRequired@{Building}!A16"], Issues(result));
    }

    [Fact]
    public void Outline_KeepsCoverAndSummariesOut_AndHidesMeasuredRows()
    {
        var outline = ProjectBoqTemplateCatalog.Outline(Template);

        Assert.Equal(["01.BÌA", "Total Amount ", " COST SUMMARY"], outline.Where(sheet => !sheet.IsData).Select(sheet => sheet.Name));
        Assert.Equal([51, 230, 69], outline.Where(sheet => sheet.IsData).Select(sheet => sheet.ItemCount));
        var building = outline.Single(sheet => sheet.Name == Building);
        Assert.Contains(new BoqOutlineRow(15, "1", "Excavation of  Soil/ Công tác đào đất", "m3", "item"), building.Rows);
        Assert.DoesNotContain(building.Rows, row => row.Row is > 15 and < 28);
        // Item numbers 1-3 were formatted as dates in the customer's file.
        Assert.Equal(["1", "2", "3"], outline.Single(sheet => sheet.Name == External).Rows.Where(row => row.Row is >= 38 and <= 40).Select(row => row.Stt));
    }
}
