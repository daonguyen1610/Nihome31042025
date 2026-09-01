using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

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
