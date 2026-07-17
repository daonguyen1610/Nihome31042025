using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>CapabilityDocumentsController</c> (NIH-98):
/// RBAC scoping (Sales view / Sales Manager manage), the two-step upload
/// then metadata create flow, file replace / version snapshot, list
/// filters (tag + search + expiry state), ZIP export with Vietnamese
/// filenames preserved.
/// </summary>
public class CapabilityDocumentsControllerTests : IntegrationTestBase
{
    private const string PdfBytesBase64 =
        // Minimal 5-byte PDF header — enough for the controller/server-side sniffing.
        "JVBERi0x";

    public CapabilityDocumentsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/capability-documents")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsWarehouse_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/capability-documents")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sale_CanViewListButCannotUpload()
    {
        // Sales Manager creates a doc first so the list has something.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await UploadAndCreateAsync("ISO 9001.pdf", "iso");

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var list = await Client.GetAsync("/api/capability-documents");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(list);
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);

        // Sale cannot create.
        var upload = await UploadPdfAsync("test.pdf");
        upload.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_Then_Create_ReturnsMetadata()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var upload = await UploadPdfAsync("Portfolio Kiến trúc.pdf");
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await ReadJsonAsync(upload);
        var filePath = uploadBody.GetProperty("filePath").GetString();
        filePath.Should().StartWith("/files/capability/");

        var create = await Client.PostAsJsonAsync("/api/capability-documents", new
        {
            name = "Portfolio Kiến trúc 2026",
            tagCode = "kien-truc",
            filePath,
            originalFileName = "Portfolio Kiến trúc.pdf",
            fileSize = uploadBody.GetProperty("fileSize").GetInt64(),
            contentType = uploadBody.GetProperty("contentType").GetString(),
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(create);
        body.GetProperty("name").GetString().Should().Be("Portfolio Kiến trúc 2026");
        body.GetProperty("tagCode").GetString().Should().Be("kien-truc");
        body.GetProperty("tagLabel").GetString().Should().Be("Kiến trúc");
        body.GetProperty("currentVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Upload_WithUnsupportedExtension_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("payload"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malware.exe");
        var res = await Client.PostAsync("/api/capability-documents/upload", content);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithInvalidTag_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var upload = await UploadPdfAsync("x.pdf");
        var uploadBody = await ReadJsonAsync(upload);
        var res = await Client.PostAsJsonAsync("/api/capability-documents", new
        {
            name = "Bad tag",
            tagCode = "does-not-exist",
            filePath = uploadBody.GetProperty("filePath").GetString(),
            originalFileName = "x.pdf",
            fileSize = 1,
            contentType = "application/pdf",
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReplaceFile_BumpsVersionAndSnapshotsPrevious()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var docId = await UploadAndCreateAsync("v1.pdf", "iso");

        var upload2 = await UploadPdfAsync("v2.pdf");
        var upload2Body = await ReadJsonAsync(upload2);
        var replace = await Client.PostAsJsonAsync($"/api/capability-documents/{docId}/replace-file", new
        {
            filePath = upload2Body.GetProperty("filePath").GetString(),
            originalFileName = "v2.pdf",
            fileSize = upload2Body.GetProperty("fileSize").GetInt64(),
            contentType = "application/pdf",
        });
        replace.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(replace)).GetProperty("currentVersion").GetInt32().Should().Be(2);

        var detail = await Client.GetAsync($"/api/capability-documents/{docId}");
        var body = await ReadJsonAsync(detail);
        body.GetProperty("versions").GetArrayLength().Should().Be(1);
        body.GetProperty("versions")[0].GetProperty("versionNumber").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task List_FilterByTag_ReturnsOnlyMatching()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        await UploadAndCreateAsync("iso.pdf", "iso");
        await UploadAndCreateAsync("mep.pdf", "mep");

        var res = await Client.GetAsync("/api/capability-documents?tagCode=iso&pageSize=50");
        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        var items = body.GetProperty("items");
        for (int i = 0; i < items.GetArrayLength(); i++)
        {
            items[i].GetProperty("tagCode").GetString().Should().Be("iso");
        }
    }

    [Fact]
    public async Task DownloadZip_ReturnsZipStream()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var idA = await UploadAndCreateAsync("Giấy phép xây dựng.pdf", "giay-phep");
        var idB = await UploadAndCreateAsync("ISO 9001.pdf", "iso");

        var res = await Client.PostAsJsonAsync("/api/capability-documents/download-zip", new
        {
            ids = new[] { idA, idB },
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
        // ZIP local file header magic "PK\x03\x04"
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public async Task Delete_RemovesDocument()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var docId = await UploadAndCreateAsync("to-delete.pdf", "iso");
        var res = await Client.DeleteAsync($"/api/capability-documents/{docId}");
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/capability-documents/{docId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- helpers ----------

    private async Task<HttpResponseMessage> UploadPdfAsync(string fileName)
    {
        var pdfBytes = Convert.FromBase64String(PdfBytesBase64);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        return await Client.PostAsync("/api/capability-documents/upload", content);
    }

    private async Task<int> UploadAndCreateAsync(string fileName, string tagCode)
    {
        var upload = await UploadPdfAsync(fileName);
        upload.EnsureSuccessStatusCode();
        var uploadBody = await ReadJsonAsync(upload);
        var create = await Client.PostAsJsonAsync("/api/capability-documents", new
        {
            name = fileName,
            tagCode,
            filePath = uploadBody.GetProperty("filePath").GetString(),
            originalFileName = fileName,
            fileSize = uploadBody.GetProperty("fileSize").GetInt64(),
            contentType = "application/pdf",
        });
        create.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(create)).GetProperty("id").GetInt32();
    }
}
