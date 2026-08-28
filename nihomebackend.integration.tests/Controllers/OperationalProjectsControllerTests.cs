using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class OperationalProjectsControllerTests : IntegrationTestBase
{
    public OperationalProjectsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuthentication_IsUnauthorized()
    {
        var response = await Client.GetAsync("/api/operational-projects");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateReadAndActivateProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();

        var create = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Operational {Guid.NewGuid():N}",
            customerId,
            startDate = "2026-08-01",
            endDate = "2026-12-31",
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(create);
        created.GetProperty("code").GetString().Should().StartWith("PJ-");
        created.GetProperty("status").GetString().Should().Be("Planning");

        var id = created.GetProperty("id").GetInt32();
        var update = await Client.PutAsJsonAsync($"/api/operational-projects/{id}", new
        {
            name = created.GetProperty("name").GetString(),
            customerId,
            projectManagerUserId = created.GetProperty("projectManagerUserId").GetInt32(),
            startDate = "2026-08-01",
            endDate = "2026-12-31",
            status = "Active",
            rowVersion = created.GetProperty("rowVersion").GetString(),
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Opportunity_ProjectFromDifferentCustomer_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerIds = new[]
        {
            await CreateCustomerAsync("Project owner"),
            await CreateCustomerAsync("Different customer"),
        };

        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Cross customer {Guid.NewGuid():N}",
            customerId = customerIds[0],
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();

        var opportunityResponse = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = $"Wrong project {Guid.NewGuid():N}",
            customerId = customerIds[1],
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 10,
            stage = "Prospecting",
        });

        opportunityResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contract_InheritsProjectFromOpportunity()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Inheritance {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();

        var opportunityResponse = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = $"Inheritance opportunity {Guid.NewGuid():N}",
            customerId,
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 20,
            stage = "Prospecting",
        });
        opportunityResponse.EnsureSuccessStatusCode();
        var opportunityId = (await ReadJsonAsync(opportunityResponse)).GetProperty("id").GetInt32();

        var contractResponse = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            opportunityId,
            status = "Draft",
            value = 1000,
        });

        contractResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(contractResponse))
            .GetProperty("operationalProjectId").GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Opportunity_UpdateWithoutProjectField_PreservesLink()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Preserved link {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();
        var create = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Linked opportunity",
            customerId,
            operationalProjectId = projectId,
            estimatedValue = 1000,
            winProbability = 25,
            stage = "Prospecting",
        });
        create.EnsureSuccessStatusCode();
        var opportunity = await ReadJsonAsync(create);

        var update = await Client.PutAsJsonAsync(
            $"/api/opportunities/{opportunity.GetProperty("id").GetInt32()}",
            new
            {
                name = "Updated linked opportunity",
                customerId,
                estimatedValue = 2000,
                winProbability = 30,
                rowVersion = opportunity.GetProperty("rowVersion").GetString(),
            });

        update.EnsureSuccessStatusCode();
        (await ReadJsonAsync(update)).GetProperty("operationalProjectId")
            .GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task DesignProject_InheritsProjectFromContract()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Design inheritance {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();
        var contractResponse = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            operationalProjectId = projectId,
            status = "Draft",
            value = 1000,
        });
        contractResponse.EnsureSuccessStatusCode();
        var contractId = (await ReadJsonAsync(contractResponse)).GetProperty("id").GetInt32();

        var designResponse = await Client.PostAsJsonAsync("/api/design-projects", new
        {
            name = "Inherited design workflow",
            customerId,
            contractId,
        });

        designResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(designResponse)).GetProperty("operationalProjectId")
            .GetInt32().Should().Be(projectId);
    }

    [Fact]
    public async Task Delete_ProjectWithDependencies_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Protected {Guid.NewGuid():N}",
            customerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var project = await ReadJsonAsync(projectResponse);
        var projectId = project.GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            db.Contracts.Add(new Contract
            {
                ContractNumber = $"HD-OP-{Guid.NewGuid():N}",
                CustomerId = customerId,
                OperationalProjectId = projectId,
            });
            await db.SaveChangesAsync();
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/operational-projects/{projectId}");
        request.Headers.IfMatch.ParseAdd(
            $"\"{project.GetProperty("rowVersion").GetString()}\"");
        var delete = await Client.SendAsync(request);

        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private Task<int> CreateCustomerAsync(string name = "Operational project customer")
    {
        return WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                Name = $"{name} {Guid.NewGuid():N}",
                Type = CustomerType.Company,
                SourceCode = "referral",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.Id;
        });
    }
}
