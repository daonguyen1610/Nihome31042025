using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class SlideshowControllerTests : IntegrationTestBase
{
    public SlideshowControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/slideshow")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBySlug_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync($"/api/slideshow/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var slug = UniqueSlug("slide");
        var created = await Client.PostAsJsonAsync("/api/slideshow", new
        {
            slug,
            imageUrl = "/images/slide.jpg",
            title = "Slide title",
            subtitle = "Sub",
            linkUrl = "/about",
            linkText = "More",
            isActive = true,
            sortOrder = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.GetAsync($"/api/slideshow/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await Client.PutAsJsonAsync($"/api/slideshow/{id}", new
        {
            slug,
            imageUrl = "/images/slide.jpg",
            title = "Slide v2",
            isActive = true,
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/slideshow/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
