using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ProcessesControllerTests : IntegrationTestBase
{
    public ProcessesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/processes")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        var title = $"Process {Guid.NewGuid():N}".Substring(0, 24);
        var created = await Client.PostAsJsonAsync("/api/processes", new
        {
            groupKey = "general",
            code = "P01",
            title,
            sortOrder = 0,
            images = Array.Empty<object>(),
            files = Array.Empty<object>(),
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/processes/{id}", new
        {
            groupKey = "general",
            code = "P01",
            title = title + "-v2",
            sortOrder = 1,
            images = Array.Empty<object>(),
            files = Array.Empty<object>(),
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/processes/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UploadImage_NoFile_ReturnsBadRequest()
    {
        using var form = new MultipartFormDataContent();
        var response = await Client.PostAsync("/api/processes/upload-image", form);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
