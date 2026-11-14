using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

// ME base BOQ: 17 priced sheets with material and labour prices, quantities broken down in unnumbered rows.
public sealed class ProjectBoqMeTemplateTests
{
    private const string Factory = "Xưởng", Electric = "Điện nước", Fire = "PCCC", Preparation = "CP chuẩn bị", Tank = "Bể XLNT";
    private static readonly BoqTemplate Template = ProjectBoqTemplateCatalog.Find("XAY-DUNG-ME")!;

    private static byte[] Workbook(Action<Dictionary<(string Sheet, string Cell), double>> change)
    {
        var values = new Dictionary<(string Sheet, string Cell), double>();
        change(values);
        return BoqWorkbookPatch.Apply(Template, values);
    }

    private static void Set(Dictionary<(string Sheet, string Cell), double> book, string sheet, string cell, double value) => book[(sheet, cell)] = value;

    private static void Priced(Dictionary<(string Sheet, string Cell), double> book)
    {
        // Excavation: 1,311.518 m³ (sum of the F1… rows below) at 120,000 material + 80,000 labour.
        Set(book, Factory, "I10", 120000);
        Set(book, Factory, "J10", 80000);
        Set(book, Factory, "K10", 157382160);
        Set(book, Factory, "L10", 104921440);
        Set(book, Factory, "M10", 262303600);
        Set(book, Factory, "M492", 262303600);
        Set(book, Electric, "G12", 5000000);
        Set(book, Electric, "H12", 1500000);
        Set(book, Electric, "I12", 5000000);
        Set(book, Electric, "J12", 1500000);
        Set(book, Electric, "K12", 6500000);
        Set(book, Electric, "K475", 6500000);
        Set(book, Fire, "F8", 250000); // 410 m of pipe, one price column.
        Set(book, Fire, "G8", 102500000);
        Set(book, Fire, "G140", 102500000);
        Set(book, Preparation, "E10", 300000); // Total row labelled in the STT column.
        Set(book, Preparation, "F10", 75000000);
        Set(book, Preparation, "F56", 75000000);
    }

    private static string[] Issues(BoqWorkbookParseResult result) =>
        result.Issues.Select(issue => $"{issue.Code}@{issue.Sheet}!{issue.Cell}").ToArray();

    [Fact]
    public void Parse_UntouchedTemplate_ReadsTheBaseQuantitiesWithoutPrices()
    {
        var result = ProjectBoqWorkbookParser.Parse(ProjectBoqTemplateCatalog.ReadFile(Template), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        Assert.Equal(Template.Sheets.Select(sheet => sheet.Name), result.Sheets.Select(sheet => sheet.Name));
        Assert.Equal(1063, result.Lines.Count);
        Assert.All(result.Lines, line => Assert.Null(line.UnitPrice));
        Assert.Equal(1063, result.Sheets.Sum(sheet => sheet.MissingPriceCount));
        // Negative wall openings and the "Bể XLNT" labels are measurement rows, not items or headings.
        Assert.DoesNotContain(result.Lines, line => line.SheetName == Factory && line.SourceRow is >= 293 and <= 307);
        Assert.Equal("Công tác ván khuôn", result.Lines.Single(line => line.SheetName == Tank && line.SourceRow == 27).GroupName);
    }

    [Fact]
    public void Parse_PricedWorkbook_AddsMaterialAndLabourIntoOneUnitPrice()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(Priced), Template);

        Assert.True(result.IsValid, string.Join(", ", Issues(result)));
        var priced = result.Lines.Where(line => line.UnitPrice.HasValue).ToList();
        Assert.Equal(["S01-R0010", "S04-R0010", "S15-R0012", "S17-R0008"], priced.Select(line => line.ItemCode));
        var excavation = priced[1];
        Assert.Equal(("Công tác đất và xử lí đầu cọc", "Đào đất", "m³", 1311.518m, 200000m, 262303600m),
            (excavation.GroupName, excavation.Description, excavation.Unit, excavation.Quantity, excavation.UnitPrice!.Value, excavation.Amount));
        Assert.Equal(("ĐIỂM ĐẤU NỐI TRUNG THẾ", 6500000m), (priced[2].GroupName, priced[2].UnitPrice));
        // The PCCC sheet keeps the pipe size in its own column; it becomes part of the item name.
        Assert.Equal(("Ống kẽm D168 x 3.96mm", "Mét", 102500000m), (priced[3].Description, priced[3].Unit, priced[3].Amount));
        Assert.Equal(75000000m, result.Sheets.Single(sheet => sheet.Name == Preparation).Total);
        Assert.Equal(262303600m + 6500000 + 102500000 + 75000000, result.Lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Parse_AmountThatDoesNotMatchQuantityTimesBothPrices_IsReported()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Factory, "M10", 157382160); // Material only.
            Set(book, Factory, "M492", 157382160);
        }), Template);

        Assert.Equal([$"amountMismatch@{Factory}!M10"], Issues(result));
    }

    [Fact]
    public void Parse_PriceOnAMeasurementRow_AsksForAnItemNumber()
    {
        var result = ProjectBoqWorkbookParser.Parse(Workbook(book =>
        {
            Priced(book);
            Set(book, Factory, "I11", 120000); // F1 footing, a breakdown of the excavation above.
        }), Template);

        Assert.Equal([$"sttRequired@{Factory}!A11"], Issues(result));
    }

    [Fact]
    public void Outline_SkipsMeasurementRows_AndReadsTheTotalLabelledInTheSttColumn()
    {
        var outline = ProjectBoqTemplateCatalog.Outline(Template);

        Assert.Equal(["TỔNG HỢP"], outline.Where(sheet => !sheet.IsData).Select(sheet => sheet.Name));
        var tank = outline.Single(sheet => sheet.Name == Tank);
        Assert.DoesNotContain(tank.Rows, row => row.Description == "Bể XLNT");
        Assert.Equal(27, tank.ItemCount);
        Assert.Equal(new BoqOutlineRow(56, null, "TỔNG GIÁ TRỊ TRƯỚC THUẾ", null, "total"),
            outline.Single(sheet => sheet.Name == Preparation).Rows[^1]);
        Assert.Contains(new BoqOutlineRow(335, null, "Tổng B", null, "subtotal"), outline.Single(sheet => sheet.Name == Electric).Rows);
        Assert.Contains(new BoqOutlineRow(8, "1", "Ống kẽm D168 x 3.96mm", "Mét", "item"), outline.Single(sheet => sheet.Name == Fire).Rows);
    }
}
