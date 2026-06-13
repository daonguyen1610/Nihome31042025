using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class JobApplicationsControllerTests : IntegrationTestBase
{
    public JobApplicationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var response = await Client.GetAsync("/api/job-applications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Submit_AsPublic_Then_AdminListsAndDeletes()
    {
        // Seed a job position in this test's scope.
        int positionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var position = new JobPosition
            {
                Title = $"Integration Test Engineer {Guid.NewGuid():N}".Substring(0, 40),
                Department = "QA",
                Location = "Hanoi",
                EmploymentType = "full-time",
                ExperienceLevel = "mid",
                IsActive = true,
            };
            db.JobPositions.Add(position);
            await db.SaveChangesAsync();
            positionId = position.Id;
        }

        // Public submit
        var submitResponse = await Client.PostAsJsonAsync("/api/job-applications", new
        {
            jobPositionId = positionId,
            candidateName = "Test Candidate",
            email = "candidate@nihome.test",
            phone = "0123456789",
            experienceYears = 3,
            coverLetter = "I love testing.",
            cvUrl = "/files/cv/test-cv.pdf",
        });
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var submitBody = await submitResponse.Content.ReadAsStringAsync();
        using var submitDoc = JsonDocument.Parse(submitBody);
        var applicationId = submitDoc.RootElement.GetProperty("id").GetInt32();

        // Admin list
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var listResponse = await Client.GetAsync($"/api/job-applications?positionId={positionId}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadAsStringAsync();
        listBody.Should().Contain("Test Candidate");

        // Admin updates status
        var statusResponse = await Client.PatchAsync(
            $"/api/job-applications/{applicationId}/status",
            JsonContent.Create(new { status = "interview" }));
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin deletes
        var deleteResponse = await Client.DeleteAsync($"/api/job-applications/{applicationId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
