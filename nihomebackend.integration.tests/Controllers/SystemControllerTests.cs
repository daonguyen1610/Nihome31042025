using System.Net;

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
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-image", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadVideo_NoFile_ReturnsBadRequest()
    {
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-video", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadCv_NoFile_ReturnsBadRequest()
    {
        using var form = new MultipartFormDataContent();
        var res = await Client.PostAsync("/api/system/upload-cv", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
