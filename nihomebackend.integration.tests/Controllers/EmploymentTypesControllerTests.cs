using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class EmploymentTypesControllerTests : IntegrationTestBase
{
    public EmploymentTypesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_IsPublic()
    {
        (await Client.GetAsync("/api/employment-types")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/employment-types", new
        {
            code = "X-" + Guid.NewGuid().ToString("N")[..6],
            name = "X",
            isActive = true,
            sortOrder = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullRoundTrip_AsAdmin_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var code = "E-" + Guid.NewGuid().ToString("N")[..6];
        var created = await Client.PostAsJsonAsync("/api/employment-types", new
        {
            code,
            name = code,
            isActive = true,
            sortOrder = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/employment-types/{id}", new
        {
            code,
            name = code + "-v2",
            isActive = false,
            sortOrder = 9,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/employment-types/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
