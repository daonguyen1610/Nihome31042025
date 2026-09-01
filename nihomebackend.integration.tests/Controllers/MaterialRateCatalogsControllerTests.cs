using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
    [InlineData("vi", "CÁC BƯỚC THỰC HIỆN", "thuộc danh mục đang hoạt động")]
    [InlineData("en", "STEPS", "an active catalog")]
    [InlineData("zh", "操作步骤", "启用目录")]
    [InlineData("ja", "手順", "有効なカタログ")]
    public async Task TemplatePackage_ContainsLocalizedGuidanceAndImportableCsv(
        string language,
        string stepsHeading,
        string approvalGuidance)
    {
        using (var scope = Factory.Services.CreateScope())
        {
            TranslationSeeder.Seed(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));

        var packageResponse = await Client.GetAsync($"/api/material-rate-catalogs/template-package?language={language}");
        packageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        packageResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/zip");
        packageResponse.Content.Headers.ContentDisposition?.FileNameStar.Should().Be("material-rate-template-package.zip");

        await using var packageStream = await packageResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);
        var guideEntry = archive.GetEntry("README.txt");
        var csvEntry = archive.GetEntry("material-rates.csv");
        guideEntry.Should().NotBeNull();
        csvEntry.Should().NotBeNull();
        using (var reader = new StreamReader(guideEntry!.Open(), Encoding.UTF8, true))
        {
            var guide = await reader.ReadToEndAsync();
            guide.Should().Contain(stepsHeading);
            guide.Should().Contain(approvalGuidance);
        }

        byte[] csvBytes;
        await using (var csvStream = csvEntry!.Open())
        await using (var copy = new MemoryStream())
        {
            await csvStream.CopyToAsync(copy);
            csvBytes = copy.ToArray();
        }
        Encoding.UTF8.GetString(csvBytes).Should().Contain("MaterialCode,MaterialName,Unit,NormPerSqm,UnitRate,WastePercent");

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
        var file = new ByteArrayContent(csvBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", "material-rates.csv");
        var importResponse = await Client.PostAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/import",
            multipart);
        importResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(importResponse)).GetProperty("importedCount").GetInt32().Should().Be(5);

        (await Client.PostAsJsonAsync(
            $"/api/material-rate-catalogs/{catalogId}/revisions/{revisionId}/approve",
            new { note = "Đã kiểm tra gói dữ liệu khách hàng" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var effectiveResponse = await Client.GetAsync(
            $"/api/material-rate-catalogs/{catalogId}/effective?onDate=2026-09-01");
        effectiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(effectiveResponse)).GetProperty("lines").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task TemplatePackage_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/material-rate-catalogs/template-package?language=vi"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TemplatePackage_WithUnsupportedLanguage_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));

        (await Client.GetAsync("/api/material-rate-catalogs/template-package?language=fr"))
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
        multipart.Add(new ByteArrayContent(new byte[2 * 1024 * 1024 + 1]), "file", "oversized.csv");

        var response = await Client.PostAsync("/api/material-rate-catalogs/1/revisions/1/import", multipart);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = (await ReadJsonAsync(response)).GetProperty("errors")[0];
        error.GetProperty("messageKey").GetString().Should().Be("csv.error.maxBytes");
        error.GetProperty("messageArgs").GetProperty("max").GetInt32().Should().Be(2);
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
