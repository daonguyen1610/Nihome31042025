using System.Net;
using System.Text.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ProjectsControllerTests : IntegrationTestBase
{
    public ProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBySlug_ForUnknownProject_ReturnsNotFound()
    {
        var response = await Client.GetAsync($"/api/projects/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Then_GetBySlug_Then_Update_Then_Delete_RoundTrip()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var slug = $"itest-project-{Guid.NewGuid():N}".Substring(0, 30);
        var payload = new
        {
            slug,
            imageUrl = "/images/project-test.jpg",
            name = "Integration Test Project",
            client = "Test Client",
            location = "Hanoi",
            scale = "Small",
            scope = "Residential",
            status = "ongoing",
            year = "2026",
            sortOrder = 0,
        };

        // Create
        var createResponse = await Client.PostAsJsonAsync("/api/projects", payload);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdBody = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdBody);
        var id = createdDoc.RootElement.GetProperty("id").GetInt32();

        // Read by slug
        var readResponse = await Client.GetAsync($"/api/projects/{slug}");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Update
        var updatePayload = new
        {
            slug,
            imageUrl = payload.imageUrl,
            name = "Integration Test Project (Updated)",
            client = payload.client,
            location = payload.location,
            scale = payload.scale,
            scope = payload.scope,
            status = "completed",
            year = payload.year,
            sortOrder = 1,
        };
        var updateResponse = await Client.PutAsJsonAsync($"/api/projects/{id}", updatePayload);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedBody = await updateResponse.Content.ReadAsStringAsync();
        using var updatedDoc = JsonDocument.Parse(updatedBody);
        updatedDoc.RootElement.GetProperty("name").GetString().Should().Contain("Updated");
        updatedDoc.RootElement.GetProperty("status").GetString().Should().Be("completed");

        // Delete
        var deleteResponse = await Client.DeleteAsync($"/api/projects/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm gone
        var afterDelete = await Client.GetAsync($"/api/projects/{slug}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var payload = new
        {
            slug = "ghost",
            imageUrl = "/x.jpg",
            name = "Ghost",
            client = "x",
            location = "x",
            scope = "x",
            status = "ongoing",
        };
        var response = await Client.PutAsJsonAsync("/api/projects/9999999", payload);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
