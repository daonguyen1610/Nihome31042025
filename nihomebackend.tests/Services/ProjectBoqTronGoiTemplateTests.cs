using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// Lump-sum quotation: 18 priced sheets in five layouts, material + labour + machine prices, sheet totals
// above the rows, unnumbered items, and quantities measured in unnumbered rows.
public sealed class ProjectBoqTronGoiTemplateTests
{
    private const string Design = "Design and permit", Factory = "01 Nhà Xưởng 01+02", Office = " 02 Văn Phòng 01+02",
        Guard = "03 NHÀ BẢO VỆ 01 &02", Drainage = "10 THOÁT NƯỚC MƯA", Fire = "Fire Fighting ", Electric = "Electric X1", Water = "CTN ";
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("TRON-GOI")!;

    private static byte[] Workbook(Action<Dictionary<(string Sheet, string Cell), double>> change)
    {
        var values = new Dictionary<(string Sheet, string Cell), double>();
        change(values);
        return BoqWorkbookPatch.Apply(Template, values);
    }

    private static void Set(Dictionary<(string Sheet, string Cell), double> book, string sheet, string cell, double value) => book[(sheet, cell)] = value;

    private static void Priced(Dictionary<(string Sheet, string Cell), double> book)
    {
        Set(book, Design, "G12", 50000000); // Item numbered 2.1.
        Set(book, Design, "H12", 50000000);
        Set(book, Design, "H35", 50000000);
        // Test piles: 132 (sum of the measured rows below) at 1,000 material + 200 labour + 50 machines.
        Set(book, Factory, "L24", 1000);
        Set(book, Factory, "M24", 200);
        Set(book, Factory, "N24", 50);
        Set(book, Factory, "P24", 165000);
        Set(book, Factory, "P8", 165000); // Sheet total above the rows.
        Set(book, Drainage, "G15", 10000); // 717.6 m³.
        Set(book, Drainage, "H15", 5000);
        Set(book, Drainage, "I15", 1000);
        Set(book, Drainage, "K15", 11481600);
        Set(book, Drainage, "K8", 11481600);
        Set(book, Electric, "G10", 300000000); // Unnumbered item.
        Set(book, Electric, "H10", 20000000);
        Set(book, Electric, "J10", 320000000);
        Set(book, Electric, "J245", 320000000);
        Set(book, Water, "I7", 100000); // 12 m; material and labour amounts in their own columns.
        Set(book, Water, "J7", 50000);
        Set(book, Water, "M7", 1800000);
        Set(book, Water, "M62", 1800000);
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Sheet}!{issue.Cell}").ToArray();

    [Fact]
    public void Parse_UntouchedTemplate_ReadsTheQuantitiesWithoutPrices()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(Template.Sheets.Select(sheet => sheet.Name), result.Sheets.Select(sheet => sheet.Name));
        Assert.Equal(898, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Null(line.UnitPrice));
        Assert.Equal(201, result.Sheets.Single(sheet => sheet.Name == Electric).ItemCount);
        // Measured rows ("Nhà xưởng 01" × 2 × 33 m) build the quantity above them and are not lines.
        Assert.DoesNotContain(result.Lines, line => line.SheetName == Factory && line.SourceRow is 25 or 28);
        Assert.Equal(132m, result.Lines.Single(line => line.SheetName == Factory && line.SourceRow == 24).Quantity);
        // "Tầng 3" ends a measured block without sizes; it is not a heading for the stair granite.
        Assert.StartsWith("CÔNG TÁC ỐP LÁT", result.Lines.Single(line => line.SheetName == Office && line.SourceRow == 409).GroupName);
        // The guard house is quoted as one lump sum on its title row.
        var guard = result.Lines.Single(line => line.SheetName == Guard);
        Assert.Equal((8, "m2", 33.6m), (guard.SourceRow, guard.Unit, guard.Quantity));
        Assert.Equal("Ống kẽm D140 x 3.96mm", result.Lines.First(line => line.SheetName == Fire).Description);
    }

    [Fact]
    public void Parse_PricedWorkbook_AddsEveryPriceColumnIntoOneUnitPrice()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(Priced), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        var priced = result.Lines.Where(line => line.UnitPrice.HasValue).ToList();
        Assert.Equal(["S01-R0012", "S05-R0024", "S13-R0015", "S15-R0010", "S18-R0007"], priced.Select(line => line.ItemCode));
        Assert.Equal(("2.1", "Công tác khảo sát địa chất\nGeological survey work", 50000000m), (priced[0].Stt, priced[0].GroupName, priced[0].Amount));
        Assert.Equal(("CÔNG TÁC XỬ LÝ ĐẦU CỌC\nPILING WORKS", "tim", 132m, 1250m, 165000m),
            (priced[1].GroupName, priced[1].Unit, priced[1].Quantity, priced[1].UnitPrice!.Value, priced[1].Amount));
        Assert.Equal((717.6m, 16000m, 11481600m), (priced[2].Quantity, priced[2].UnitPrice!.Value, priced[2].Amount));
        Assert.Equal(("", "Bộ / Set", 320000000m), (priced[3].Stt, priced[3].Unit, priced[3].UnitPrice));
        Assert.Equal((150000m, 1800000m), (priced[4].UnitPrice!.Value, priced[4].Amount));
        Assert.Equal(50000000m + 165000 + 11481600 + 320000000 + 1800000, result.Lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Parse_AmountThatLeavesOutTheMachinePrice_IsReported()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Factory, "P24", 158400); // 132 × (1,000 + 200).
            Set(book, Factory, "P8", 158400);
        }), Template);

        Assert.Equal([$"amountMismatch@{Factory}!P24"], Issues(result));
    }

    [Fact]
    public void Parse_PriceOnAMeasuredRow_AsksForAnItemNumber()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Factory, "L25", 1000); // "Nhà xưởng 01", 2 piles × 33 m.
        }), Template);

        Assert.Equal([$"sttRequired@{Factory}!A25"], Issues(result));
    }

    [Fact]
    public void Outline_KeepsCoverAndSummariesOut_AndHidesMeasuredRows()
    {
        var outline = ProjectBoqTemplateCatalog.Outline(Template);

        Assert.Equal(21, outline.Count);
        Assert.Equal(["Cover ", "Total ", "Summary "], outline.Where(sheet => !sheet.IsData).Select(sheet => sheet.Name));
        var office = outline.Single(sheet => sheet.Name == Office);
        // Nested labels over measured rows ("Nhà Văn Phòng 02", then "Tầng 1") and an empty "Tầng 3".
        Assert.DoesNotContain(office.Rows, row => row.Row is 73 or 74 or 408 or 421);
        Assert.Equal(new BoqOutlineRow(245, null, "TỔNG CỘNG ( Chưa bao gồm thuế VAT  )\nTOTAL (Excluding VAT)", null, "total"),
            outline.Single(sheet => sheet.Name == Electric).Rows[^1]);
        Assert.Contains(new BoqOutlineRow(10, null, "Máy biến áp 3P-1000kVA\n3P-1000kVA Transformer", "Bộ / Set", "item"),
            outline.Single(sheet => sheet.Name == Electric).Rows);
    }
}
