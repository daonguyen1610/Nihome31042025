using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectBoqTemplateOutlineTests
{
    private static BoqOutlineSheet Sheet(string code) =>
        Assert.Single(ProjectBoqTemplateCatalog.Outline(ProjectBoqTemplateCatalog.Find(code)!));

    [Fact]
    public void Outline_ServicedApartment_ListsGroupsItemsSubtotalsAndTotal()
    {
        var sheet = Sheet("PCCC-CAN-HO");

        Assert.Equal(("ptct", true, 78), (sheet.Name, sheet.IsData, sheet.ItemCount));
        Assert.Equal(new BoqOutlineRow(15, "A", "HỆ THỐNG BÁO CHÁY TỰ ĐỘNG", null, "group"), sheet.Rows[0]);
        Assert.Equal(new BoqOutlineRow(16, "1", "Trung tâm báo cháy 8 zone", "bộ", "item"), sheet.Rows[1]);
        Assert.Contains(new BoqOutlineRow(30, null, "CỘNG A", null, "subtotal"), sheet.Rows);
        Assert.Equal(new BoqOutlineRow(101, null, "CỘNG ( A + B + C)", null, "total"), sheet.Rows[^1]);
    }

    [Fact]
    public void Outline_DesignPermit_SkipsRepeatedHeadingsAndUsesFirstDescriptionLine()
    {
        var sheet = Sheet("THIET-KE-XIN-PHEP");

        Assert.Equal(("Bao gia", 9), (sheet.Name, sheet.ItemCount));
        Assert.DoesNotContain(sheet.Rows, row => row.Row == 36);
        Assert.Contains(new BoqOutlineRow(32, "2", "Thiết kế cơ sở", "m2", "item"), sheet.Rows);
        Assert.Contains(new BoqOutlineRow(37, "III", "Các thủ tục xin phép xây dựng", null, "group"), sheet.Rows);
        Assert.Equal(new BoqOutlineRow(46, null, "Tổng cộng trước VAT (B+C)", null, "total"), sheet.Rows[^1]);
    }

    [Fact]
    public void Outline_MarksExactlyTheProfileSheetsAsData()
    {
        foreach (var template in ProjectBoqTemplateCatalog.All)
        {
            var outline = ProjectBoqTemplateCatalog.Outline(template).Where(sheet => sheet.IsData);
            Assert.Equal(template.Sheets.Select(sheet => sheet.Name), outline.Select(sheet => sheet.Name));
        }
    }
}
