using System.Net;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class HardDeleteOperationsControllerTests : IntegrationTestBase
{
    public HardDeleteOperationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Status_Anonymous_IsUnauthorized()
    {
        var response = await Client.GetAsync($"/api/hard-delete-operations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StatusAndRetry_DifferentUser_ReturnNotFound()
    {
        var operationId = await SeedOperationForAsync(TestDataSeeder.SuperAdminPhone);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        (await Client.GetAsync($"/api/hard-delete-operations/{operationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.PostAsync($"/api/hard-delete-operations/{operationId}/retry", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StatusAndRetry_Owner_ReturnOperationWithoutBypassingManualState()
    {
        var operationId = await SeedOperationForAsync(TestDataSeeder.SuperAdminPhone);
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);

        var status = await Client.GetAsync($"/api/hard-delete-operations/{operationId}");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusBody = await ReadJsonAsync(status);
        statusBody.GetProperty("operationId").GetGuid().Should().Be(operationId);
        statusBody.GetProperty("requiresManualAction").GetBoolean().Should().BeTrue();

        var retry = await Client.PostAsync($"/api/hard-delete-operations/{operationId}/retry", null);
        retry.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var retryBody = await ReadJsonAsync(retry);
        retryBody.GetProperty("status").GetString().Should().Be("ManualActionRequired");
    }

    private async Task<Guid> SeedOperationForAsync(string phoneNumber)
    {
        var operationId = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            var userId = await db.Users.Where(user => user.PhoneNumber == phoneNumber)
                .Select(user => user.Id)
                .SingleAsync();
            db.HardDeleteOperations.Add(new HardDeleteOperation
            {
                Id = operationId,
                ResourceType = $"unregistered-{Guid.NewGuid():N}",
                ResourceId = Guid.NewGuid().ToString("N"),
                ResourceLabel = "Restricted operation",
                PlanToken = new string('a', 64),
                Confirmation = "DELETE",
                RequestedBy = userId.ToString(),
                Status = HardDeleteOperationStatus.ManualActionRequired,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                LastErrorCode = "resource_handler_missing",
            });
            await db.SaveChangesAsync();
        });
        return operationId;
    }
}
