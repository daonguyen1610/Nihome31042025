using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class NewsCategoriesControllerTests : IntegrationTestBase
{
    public NewsCategoriesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/news-categories")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var name = $"NCat-{Guid.NewGuid():N}".Substring(0, 18);
        var created = await Client.PostAsJsonAsync("/api/news-categories", new { name, isActive = true, sortOrder = 0 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/news-categories/{id}", new { name = name + "-v2", isActive = false, sortOrder = 3 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/news-categories/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
