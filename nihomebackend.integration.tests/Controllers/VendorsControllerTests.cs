using System.Net;
using System.Net.Http.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class VendorsControllerTests : IntegrationTestBase
{
    public VendorsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/vendors")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Warehouse_CanViewButCannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await Client.GetAsync("/api/vendors")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = "DENIED",
            companyName = "Denied Vendor",
            vendorType = "Supplier",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync("/api/vendors/2147483647")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_InvalidPayload_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));

        var response = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = " ",
            companyName = " ",
            email = "not-an-email",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_MissingVendor_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await Client.GetAsync("/api/vendors/2147483647")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrudRoundTrip_FiltersAndRejectsDuplicateCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var code = $"VEN-{suffix}";

        var created = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = code,
            companyName = $"Vendor {suffix}",
            vendorType = "Supplier",
            taxCode = $"TAX-{suffix}",
            phone = "0901234567",
            email = $"vendor-{suffix}@example.com",
            contactPerson = "Nguyen Van A",
            tradeCategory = "Electrical",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBody = await ReadJsonAsync(created);
        var id = createdBody.GetProperty("id").GetInt32();
        createdBody.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var createdByName = createdBody.GetProperty("createdByName").GetString();
        createdByName.Should().NotBeNullOrWhiteSpace();

        var detailBeforeUpdate = await Client.GetAsync($"/api/vendors/{id}");
        detailBeforeUpdate.EnsureSuccessStatusCode();
        (await ReadJsonAsync(detailBeforeUpdate)).GetProperty("createdByName").GetString().Should().Be(createdByName);

        var filtered = await Client.GetAsync($"/api/vendors?search={code}&vendorType=Supplier&isActive=true");
        filtered.EnsureSuccessStatusCode();
        var items = (await ReadJsonAsync(filtered)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("id").GetInt32().Should().Be(id);

        var updated = await Client.PutAsJsonAsync($"/api/vendors/{id}", new
        {
            vendorCode = code,
            companyName = $"Vendor Updated {suffix}",
            vendorType = "Both",
            email = $"vendor-{suffix}@example.com",
            isActive = false,
        });
        updated.EnsureSuccessStatusCode();
        var updatedBody = await ReadJsonAsync(updated);
        updatedBody.GetProperty("isActive").GetBoolean().Should().BeFalse();
        updatedBody.GetProperty("createdByName").GetString().Should().Be(createdByName);

        var duplicate = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = code.ToLowerInvariant(),
            companyName = "Duplicate Vendor",
            vendorType = "SubContractor",
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await Client.DeleteAsync($"/api/vendors/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/vendors/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/api/vendors/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}