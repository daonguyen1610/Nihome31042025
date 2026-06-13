using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class RecruitmentDropdownOptionsControllerTests : IntegrationTestBase
{
    public RecruitmentDropdownOptionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetByType_WithoutType_ReturnsBadRequest()
    {
        (await Client.GetAsync("/api/recruitment-dropdown-options")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetByType_ExperienceLevel_SeedsDefaults_AndReturnsList()
    {
        var res = await Client.GetAsync("/api/recruitment-dropdown-options?type=experience-level");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await ReadJsonAsync(res);
        json.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/recruitment-dropdown-options", new
        {
            type = "experience-level",
            code = "anon-" + Guid.NewGuid().ToString("N")[..6],
            name = "Anonymous",
            isActive = true,
            sortOrder = 99,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullRoundTrip_AsAdmin_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var code = "lvl-" + Guid.NewGuid().ToString("N")[..6];
        var created = await Client.PostAsJsonAsync("/api/recruitment-dropdown-options", new
        {
            type = "experience-level",
            code,
            name = "Custom Level",
            isActive = true,
            sortOrder = 50,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/recruitment-dropdown-options/{id}", new
        {
            type = "experience-level",
            code,
            name = "Renamed Level",
            isActive = false,
            sortOrder = 51,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(updated)).GetProperty("name").GetString().Should().Be("Renamed Level");

        (await Client.DeleteAsync($"/api/recruitment-dropdown-options/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await Client.PutAsJsonAsync($"/api/recruitment-dropdown-options/{id}", new
        {
            type = "experience-level",
            code,
            name = "Already gone",
            isActive = true,
            sortOrder = 0,
        })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Client.DeleteAsync($"/api/recruitment-dropdown-options/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithDuplicateCode_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var code = "dup-" + Guid.NewGuid().ToString("N")[..6];
        var first = await Client.PostAsJsonAsync("/api/recruitment-dropdown-options", new
        {
            type = "benefit",
            code,
            name = "First",
            isActive = true,
            sortOrder = 1,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync("/api/recruitment-dropdown-options", new
        {
            type = "benefit",
            code,
            name = "Second",
            isActive = true,
            sortOrder = 2,
        });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithBlankName_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/recruitment-dropdown-options", new
        {
            type = "benefit",
            code = "blank-" + Guid.NewGuid().ToString("N")[..6],
            name = "   ",
            isActive = true,
            sortOrder = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
