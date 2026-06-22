using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class SystemControllerTests : IntegrationTestBase
{
    public SystemControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var res = await Client.GetAsync("/api/system/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UploadImage_NoFile_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-image", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadVideo_NoFile_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-video", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadCv_NoFile_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-cv", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("activities")]
    [InlineData("news")]
    [InlineData("projects")]
    [InlineData("logos")]
    [InlineData("misc")]
    public async Task UploadImage_PlacesFileInRequestedBucket(string bucket)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "photo.jpg");
        form.Add(new StringContent(bucket), "category");

        var res = await Client.PostAsync("/api/system/upload-image", form);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await res.Content.ReadFromJsonAsync<UploadImageResponse>();
        payload.Should().NotBeNull();
        payload!.ImageUrl.Should().StartWith($"/images/upload/{bucket}/");
    }

    [Fact]
    public async Task UploadImage_UnknownCategory_FallsBackToMisc()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "photo.jpg");
        form.Add(new StringContent("../etc"), "category");

        var res = await Client.PostAsync("/api/system/upload-image", form);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await res.Content.ReadFromJsonAsync<UploadImageResponse>();
        payload!.ImageUrl.Should().StartWith("/images/upload/misc/");
    }

    private sealed record UploadImageResponse(string ImageUrl);
}
