using System.Net;
using System.Net.Http.Headers;

namespace NihomeBackend.IntegrationTests.Controllers;

public class BusinessDocumentsControllerTests : IntegrationTestBase
{
    public BusinessDocumentsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Upload_WithoutAuthentication_ReturnsUnauthorized()
    {
        (await UploadAsync("vendors", "capability.pdf")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_WithoutDomainPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        (await UploadAsync("vendors", "capability.pdf")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("vendors")]
    [InlineData("acceptance")]
    [InlineData("as-built")]
    [InlineData("handover")]
    public async Task Upload_AsSuperAdmin_ReturnsManagedHostRelativePath(string area)
    {
        await AuthTestHelper.AuthenticateAsync(
            Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));

        var response = await UploadAsync(area, "evidence.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("path").GetString()
            .Should().StartWith($"/files/business-documents/{area}/");
        body.GetProperty("originalFileName").GetString().Should().Be("evidence.pdf");
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));

        (await UploadAsync("acceptance", "payload.exe")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpResponseMessage> UploadAsync(string area, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("document"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", fileName);
        return await Client.PostAsync($"/api/business-documents/{area}", content);
    }
}