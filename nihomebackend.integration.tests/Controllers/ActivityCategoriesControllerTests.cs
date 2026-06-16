using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ActivityCategoriesControllerTests : IntegrationTestBase
{
    public ActivityCategoriesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        (await Client.GetAsync("/api/activity-categories")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var name = $"Cat-{Guid.NewGuid():N}".Substring(0, 16);
        var created = await Client.PostAsJsonAsync("/api/activity-categories", new { name, isActive = true, sortOrder = 0 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/activity-categories/{id}", new { name = name + "-v2", isActive = false, sortOrder = 2 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/activity-categories/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/activity-categories/999999", new { name = "x", isActive = true, sortOrder = 0 });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
