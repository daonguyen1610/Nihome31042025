using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class AboutSectionsControllerTests : IntegrationTestBase
{
    public AboutSectionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/about-sections")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Read_Update_Delete()
    {
        var slug = UniqueSlug("about");
        var payload = new
        {
            slug,
            eyebrow = "About",
            titleA = "Title A",
            titleB = "Title B",
            paragraph1 = "P1",
            paragraph2 = "P2",
            imageUrl = "/images/x.jpg",
            isActive = true,
            sortOrder = 0,
        };

        var created = await Client.PostAsJsonAsync("/api/about-sections", payload);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.GetAsync($"/api/about-sections/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await Client.PutAsJsonAsync($"/api/about-sections/{id}", new
        {
            slug,
            eyebrow = "About v2",
            titleA = "Title A2",
            titleB = "Title B2",
            paragraph1 = "P1",
            paragraph2 = "P2",
            imageUrl = "/images/x.jpg",
            isActive = true,
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/about-sections/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetBySlug_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync($"/api/about-sections/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
