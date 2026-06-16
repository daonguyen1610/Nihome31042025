using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ActivitiesControllerTests : IntegrationTestBase
{
    public ActivitiesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/activities")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetBySlug_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync($"/api/activities/{Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Read_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var slug = UniqueSlug("act");
        var payload = new
        {
            slug,
            date = "2026-06-13",
            imageUrl = "/images/a.jpg",
            title = "Activity",
            excerpt = "x",
            content = new[] { "para 1" },
            sortOrder = 0,
        };

        var created = await Client.PostAsJsonAsync("/api/activities", payload);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.GetAsync($"/api/activities/{slug}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await Client.PutAsJsonAsync($"/api/activities/{id}", new
        {
            slug,
            date = "2026-06-14",
            imageUrl = "/images/a.jpg",
            title = "Activity v2",
            excerpt = "x2",
            content = new[] { "para v2" },
            sortOrder = 1,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/activities/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
