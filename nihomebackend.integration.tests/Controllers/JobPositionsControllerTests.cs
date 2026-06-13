using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class JobPositionsControllerTests : IntegrationTestBase
{
    public JobPositionsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_IsPublic()
    {
        (await Client.GetAsync("/api/job-positions")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        (await Client.GetAsync("/api/job-positions/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/job-positions", new
        {
            title = "Dev",
            department = "Tech",
            location = "HCM",
            employmentType = "full-time",
            experienceLevel = "mid",
            description = "x",
            requirements = new[] { "csharp" },
            isActive = true,
            sortOrder = 0,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullRoundTrip_AsAdmin_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        // Seed an EmploymentType the JobPosition can reference.
        var etCode = "ft-" + Guid.NewGuid().ToString("N")[..6];
        var et = await Client.PostAsJsonAsync("/api/employment-types", new { code = etCode, name = etCode, isActive = true, sortOrder = 0 });
        et.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await Client.PostAsJsonAsync("/api/job-positions", new
        {
            title = $"Dev {Guid.NewGuid():N}".Substring(0, 16),
            department = "Tech",
            location = "HCM",
            employmentType = etCode,
            experienceLevel = "mid",
            description = "x",
            requirements = new[] { "csharp" },
            isActive = true,
            sortOrder = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var updated = await Client.PutAsJsonAsync($"/api/job-positions/{id}", new
        {
            title = "Dev v2",
            department = "Tech",
            location = "HCM",
            employmentType = etCode,
            experienceLevel = "senior",
            description = "x2",
            requirements = new[] { "csharp", "dotnet" },
            isActive = false,
            sortOrder = 1,
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.DeleteAsync($"/api/job-positions/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
