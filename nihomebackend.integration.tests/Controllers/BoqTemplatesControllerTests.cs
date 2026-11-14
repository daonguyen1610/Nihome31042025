using System.Net;
using System.Text.Json;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class BoqTemplatesControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task LoginAsync(string role)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }

    [Fact]
    public async Task Procurement_BrowsesTheLibraryAndDownloadsTheUnchangedForm()
    {
        await LoginAsync("PROCUREMENT");

        var list = await ReadJsonAsync(await Client.GetAsync("/api/boq-templates"));
        var pccc = list.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "PCCC-CAN-HO");
        var sheet = pccc.GetProperty("sheets").EnumerateArray().Single();
        (sheet.GetProperty("name").GetString(), sheet.GetProperty("isData").GetBoolean(), sheet.GetProperty("itemCount").GetInt32())
            .Should().Be(("ptct", true, 78));

        var file = await Client.GetAsync("/api/boq-templates/PCCC-CAN-HO/file");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync())
            .Should().Equal(ProjectBoqTemplateCatalog.ReadFile(ProjectBoqTemplateCatalog.Find("PCCC-CAN-HO")!));

        var outline = await ReadJsonAsync(await Client.GetAsync("/api/boq-templates/PCCC-CAN-HO/sheets/ptct"));
        var first = outline.GetProperty("rows")[1];
        (first.GetProperty("kind").GetString(), first.GetProperty("description").GetString(), first.GetProperty("unit").GetString())
            .Should().Be(("item", "Trung tâm báo cháy 8 zone", "bộ"));
    }

    [Fact]
    public async Task FactoryForm_OpensASheetWhoseNameHasAnAmpersandAndATrailingSpace()
    {
        await LoginAsync("PROCUREMENT");

        var outline = await ReadJsonAsync(await Client.GetAsync(
            $"/api/boq-templates/NHA-XUONG/sheets/{Uri.EscapeDataString("C4.Hệ Thống Điện & Điện Nhẹ ")}"));

        (outline.GetProperty("name").GetString(), outline.GetProperty("rows")[2].GetProperty("description").GetString())
            .Should().Be(("C4.Hệ Thống Điện & Điện Nhẹ ", "Recloser 3P-24kV-630A"));
    }

    [Fact]
    public async Task MeForm_OpensASheetWhoseNameHasAPlus_AndHidesMeasurementRows()
    {
        await LoginAsync("PROCUREMENT");

        var outline = await ReadJsonAsync(await Client.GetAsync(
            $"/api/boq-templates/XAY-DUNG-ME/sheets/{Uri.EscapeDataString("Cổng+HR")}"));

        var rows = outline.GetProperty("rows").EnumerateArray().ToList();
        (outline.GetProperty("name").GetString(), rows[0].GetProperty("description").GetString(), rows[2].GetProperty("description").GetString())
            .Should().Be(("Cổng+HR", "CỔNG + BẢNG TÊN", "Đào đất"));
        rows.Should().NotContain(row => row.GetProperty("description").GetString() == "Cổng chính");
    }

    [Fact]
    public async Task LumpSumForm_ListsItsVisibleSheets_AndOpensAnUnnumberedElectricalSheet()
    {
        await LoginAsync("PROCUREMENT");

        var list = await ReadJsonAsync(await Client.GetAsync("/api/boq-templates"));
        var sheets = list.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "TRON-GOI")
            .GetProperty("sheets").EnumerateArray().ToList();
        sheets.Should().HaveCount(21);
        sheets.Where(sheet => !sheet.GetProperty("isData").GetBoolean()).Select(sheet => sheet.GetProperty("name").GetString())
            .Should().Equal("Cover ", "Total ", "Summary ");

        var outline = await ReadJsonAsync(await Client.GetAsync("/api/boq-templates/TRON-GOI/sheets/Electric%20X1"));
        var first = outline.GetProperty("rows").EnumerateArray().First(row => row.GetProperty("kind").GetString() == "item");
        (first.GetProperty("description").GetString(), first.GetProperty("unit").GetString())
            .Should().Be(("Máy biến áp 3P-1000kVA\n3P-1000kVA Transformer", "Bộ / Set"));
    }

    [Fact]
    public async Task CsaForm_ListsItsThreeBills_AndOpensABillWhoseNameHasAnAmpersand()
    {
        await LoginAsync("PROCUREMENT");

        var list = await ReadJsonAsync(await Client.GetAsync("/api/boq-templates"));
        var bills = list.EnumerateArray().Single(item => item.GetProperty("code").GetString() == "CSA-WORK")
            .GetProperty("sheets").EnumerateArray().Where(sheet => sheet.GetProperty("isData").GetBoolean())
            .Select(sheet => sheet.GetProperty("name").GetString());
        bills.Should().Equal("Bill 1- Premilinary & General", "Bill 2-Main building works", "Bill 3-External works");

        var outline = await ReadJsonAsync(await Client.GetAsync(
            $"/api/boq-templates/CSA-WORK/sheets/{Uri.EscapeDataString("Bill 1- Premilinary & General")}"));
        var first = outline.GetProperty("rows").EnumerateArray().First(row => row.GetProperty("kind").GetString() == "item");
        (first.GetProperty("stt").GetString(), first.GetProperty("description").GetString(), first.GetProperty("unit").GetString())
            .Should().Be(("1.1", "Groundbreaking", "item"));
    }

    [Fact]
    public async Task UnknownTemplateOrSheet_IsNotFound_AndOtherRolesAreRefused()
    {
        await LoginAsync("PROCUREMENT_MANAGER");
        (await Client.GetAsync("/api/boq-templates/KHONG-CO/file")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync("/api/boq-templates/PCCC-CAN-HO/sheets/khong-co")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await LoginAsync("DESIGN");
        (await Client.GetAsync("/api/boq-templates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync("/api/boq-templates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
