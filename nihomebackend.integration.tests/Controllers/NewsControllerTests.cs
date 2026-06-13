using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class NewsControllerTests : IntegrationTestBase
{
    public NewsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/news")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBySlug_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync($"/api/news/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        var slug = UniqueSlug("news");
        var payload = new
        {
            slug,
            date = "2026-06-13",
            imageUrl = "/images/n.jpg",
            category = "general",
            title = "News",
            excerpt = "x",
            content = new[] { "para 1" },
            sortOrder = 0,
        };
        var created = await Client.PostAsJsonAsync("/api/news", payload);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.GetAsync($"/api/news/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await Client.PutAsJsonAsync($"/api/news/{id}", new
        {
            slug,
            date = "2026-06-14",
            imageUrl = "/images/n.jpg",
            category = "general",
            title = "News v2",
            excerpt = "x2",
            content = new[] { "para v2" },
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
