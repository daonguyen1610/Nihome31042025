using System.Net;
using System.Text.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ServicesControllerTests : IntegrationTestBase
{
    public ServicesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/services")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBySlug_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync($"/api/services/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        var slug = UniqueSlug("svc");
        var sections = JsonDocument.Parse("[{\"title\":\"Section\",\"items\":[]}]").RootElement;

        var created = await Client.PostAsJsonAsync("/api/services", new
        {
            slug,
            title = "Service",
            shortTitle = "Short",
            tagline = "Tag",
            intro = "Intro",
            sections,
            highlights = new[] { "h1" },
            sortOrder = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.GetAsync($"/api/services/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await Client.PutAsJsonAsync($"/api/services/{id}", new
        {
            slug,
            title = "Service v2",
            shortTitle = "Short",
            tagline = "Tag",
            intro = "Intro",
            sections,
            highlights = new[] { "h1", "h2" },
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/services/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
