using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;

namespace NihomeBackend.IntegrationTests.Controllers;

public class MaterialRateCatalogsControllerTests : IntegrationTestBase
{
    public MaterialRateCatalogsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/material-rate-catalogs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsRoleWithoutPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        var response = await Client.GetAsync("/api/material-rate-catalogs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_AsSaleWithoutApprovePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        var response = await Client.PostAsJsonAsync(
            "/api/material-rate-catalogs/1/revisions/1/approve",
            new { note = "Không có quyền" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Lifecycle_ImportsApprovesAndReturnsEffectiveRevision()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var code = "RATE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var catalogResponse = await Client.PostAsJsonAsync("/api/material-rate-catalogs", new
        {
            code,
            name = "Đơn giá kiểm thử",
            currency = "VND",
            isActive = true,
        });
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var catalogId = (await ReadJsonAsync(catalogResponse)).GetProperty("id").GetInt32();

        var revisionResponse = await Client.PostAsJsonAsync($"/api/material-rate-catalogs/{catalogId}/revisions", new
        {
            effectiveFrom = "2026-09-01",
            effectiveTo = "2026-12-31",
        });
        revisionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var revisionId = (await ReadJsonAsync(revisionResponse)).GetProperty("id").GetInt32();

        const string csv = "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\r\nVL-1,Keo dán gạch,kg,2.5,15000,5\r\n";
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", "rates.csv");
        var importResponse = await Client.PostAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/import",
            multipart);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(importResponse)).GetProperty("importedCount").GetInt32().Should().Be(1);

        var approveResponse = await Client.PostAsJsonAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/approve",
            new { note = "Đã kiểm tra" });
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var effectiveResponse = await Client.GetAsync(
            $"/api/material-rate-catalogs/{catalogId}/effective?onDate=2026-12-31");
        effectiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var effective = await ReadJsonAsync(effectiveResponse);
        effective.GetProperty("status").GetString().Should().Be("Approved");
        effective.GetProperty("lines")[0].GetProperty("amountPerSqm").GetDecimal().Should().Be(39375m);
    }

    [Theory]
    [InlineData("vi", "Nhập liệu", "BIỂU MẪU ĐỊNH MỨC VÀ ĐƠN GIÁ VẬT LIỆU", "NICON-Bieu-mau-dinh-muc-don-gia.xlsx")]
    [InlineData("en", "Entry", "MATERIAL NORM AND RATE FORM", "NICON-Material-Rate-Form.xlsx")]
    [InlineData("zh", "录入", "材料定额与单价表", "NICON-材料定额单价表.xlsx")]
    [InlineData("ja", "入力", "材料基準量・単価フォーム", "NICON-材料基準単価表.xlsx")]
    public async Task ExcelTemplate_IsLocalizedFormattedAndImportable(
        string language,
        string entrySheetName,
        string title,
        string fileName)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            TranslationSeeder.Seed(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));

        var templateResponse = await Client.GetAsync($"/api/material-rate-catalogs/excel-template?language={language}");
        templateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        templateResponse.Content.Headers.ContentType?.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        templateResponse.Content.Headers.ContentDisposition?.FileNameStar.Should().Be(fileName);

        await using var templateStream = await templateResponse.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(templateStream);
        var entrySheet = workbook.Worksheet(entrySheetName);
        entrySheet.Cell("A1").GetString().Should().Be(title);
        entrySheet.Cell("A4").GetString().Should().NotContain("MaterialCode");
        entrySheet.Cell("A4").Style.Fill.BackgroundColor.ColorType.Should().NotBe(XLColorType.Indexed);
        entrySheet.Column(2).Width.Should().BeGreaterThan(30);
        workbook.Worksheets.Should().Contain(sheet => sheet.Name != entrySheetName && sheet.Visibility == XLWorksheetVisibility.Visible);
        workbook.Worksheet("_NICON").Visibility.Should().Be(XLWorksheetVisibility.VeryHidden);

        entrySheet.Cell("A5").Value = "VL-FORM-01";
        entrySheet.Cell("B5").Value = "Vật liệu từ biểu mẫu";
        entrySheet.Cell("C5").Value = "kg";
        entrySheet.Cell("D5").Value = 2.5;
        entrySheet.Cell("E5").Value = 15000;
        entrySheet.Cell("F5").Value = 5;
        await using var completedForm = new MemoryStream();
        workbook.SaveAs(completedForm);
        var workbookBytes = completedForm.ToArray();

        var code = "PACKAGE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var catalogResponse = await Client.PostAsJsonAsync("/api/material-rate-catalogs", new
        {
            code,
            name = "Đơn giá từ gói mẫu",
            currency = "VND",
            isActive = true,
        });
        var catalogId = (await ReadJsonAsync(catalogResponse)).GetProperty("id").GetInt32();
        var revisionResponse = await Client.PostAsJsonAsync($"/api/material-rate-catalogs/{catalogId}/revisions", new
        {
            effectiveFrom = "2026-09-01",
            effectiveTo = "2026-12-31",
        });
        var revisionId = (await ReadJsonAsync(revisionResponse)).GetProperty("id").GetInt32();

        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbookBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        multipart.Add(file, "file", fileName);
        var importResponse = await Client.PostAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/import",
            multipart);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(importResponse)).GetProperty("importedCount").GetInt32().Should().Be(1);

        (await Client.PostAsJsonAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/approve",
            new { note = "Đã kiểm tra gói dữ liệu khách hàng" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var effectiveResponse = await Client.GetAsync(
            $"/api/material-rate-catalogs/{catalogId}/effective?onDate=2026-09-01");
        effectiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var effective = await ReadJsonAsync(effectiveResponse);
        effective.GetProperty("lines").GetArrayLength().Should().Be(1);
        effective.GetProperty("lines")[0].GetProperty("amountPerSqm").GetDecimal().Should().Be(39375m);
    }

    [Fact]
    public async Task ExcelTemplate_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/material-rate-catalogs/excel-template?language=vi"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExcelTemplate_WithUnsupportedLanguage_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));

        (await Client.GetAsync("/api/material-rate-catalogs/excel-template?language=fr"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Import_WithEmptyFile_ReturnsLocalizedValidationKey()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent([]), "file", "empty.csv");

        var response = await Client.PostAsync("/api/material-rate-catalogs/1/revisions/1/import", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("errors")[0].GetProperty("messageKey").GetString()
            .Should().Be("materialRates.validation.csvEmpty");
    }

    [Fact]
    public async Task Import_WithOversizedFile_ReturnsLocalizedValidationArguments()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]), "file", "oversized.xlsx");

        var response = await Client.PostAsync("/api/material-rate-catalogs/1/revisions/1/import", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await ReadJsonAsync(response)).GetProperty("errors")[0];
        error.GetProperty("messageKey").GetString().Should().Be("materialRates.validation.maxBytes");
        error.GetProperty("messageArgs").GetProperty("max").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Import_WithAnyInvalidRow_ReturnsBadRequestWithoutPersistingLines()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var code = "ATOMIC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var catalogResponse = await Client.PostAsJsonAsync("/api/material-rate-catalogs", new
        {
            code,
            name = "Kiểm thử nguyên tử",
            currency = "VND",
        });
        var catalogId = (await ReadJsonAsync(catalogResponse)).GetProperty("id").GetInt32();
        var revisionResponse = await Client.PostAsJsonAsync($"/api/material-rate-catalogs/{catalogId}/revisions", new
        {
            effectiveFrom = "2026-09-01",
        });
        var revisionId = (await ReadJsonAsync(revisionResponse)).GetProperty("id").GetInt32();

        const string csv = "MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent\nOK,Hợp lệ,kg,1,100,0\nBAD,Sai,kg,1,12,5,0";
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "invalid.csv");
        var response = await Client.PostAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/import",
            multipart);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var detail = await Client.GetAsync($"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}");
        detail.EnsureSuccessStatusCode();
        (await ReadJsonAsync(detail)).GetProperty("lines").GetArrayLength().Should().Be(0);
    }
}
