using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class LogosControllerTests : IntegrationTestBase
{
    public LogosControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/logos")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        var name = $"Logo-{Guid.NewGuid():N}".Substring(0, 18);
        var created = await Client.PostAsJsonAsync("/api/logos", new
        {
            name,
            imageUrl = "/images/logos/x.png",
            href = "https://example.com",
            kind = "Client",
            sortOrder = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/logos/{id}", new
        {
            name = name + "-v2",
            imageUrl = "/images/logos/x.png",
            href = "https://example.com",
            kind = "Partner",
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/logos/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
