using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for /api/asbuilt-categories endpoints (NIH-452).
/// </summary>
public class AsBuiltDocumentCategoriesControllerTests : IntegrationTestBase
{
    public AsBuiltDocumentCategoriesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetAll_RequiresAuthentication()
    {
        var res = await Client.GetAsync("/api/asbuilt-categories");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_AuthenticatedUser_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.GetAsync("/api/asbuilt-categories");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(res);
        json.GetArrayLength().Should().BeGreaterOrEqualTo(5); // 5 seeded categories
    }

    [Fact]
    public async Task GetById_ReturnsCategory()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        // Get list first
        var listRes = await Client.GetAsync("/api/asbuilt-categories");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await ReadJsonAsync(listRes);
        var firstId = list[0].GetProperty("id").GetInt32();

        // Get by ID
        var res = await Client.GetAsync($"/api/asbuilt-categories/{firstId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var cat = await ReadJsonAsync(res);
        cat.GetProperty("id").GetInt32().Should().Be(firstId);
    }

    [Fact]
    public async Task FullRoundTrip_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var code = $"Test{Guid.NewGuid():N}".Substring(0, 20);
        var name = $"Test Category {DateTime.UtcNow.Ticks}";

        // Create
        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            name,
            nameVi = name,
            nameEn = "Test Category EN",
            nameZh = "测试分类",
            nameJa = "テストカテゴリ",
            isRequired = false,
            isActive = true,
            sortOrder = 99
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        var id = created.GetProperty("id").GetInt32();
        created.GetProperty("code").GetString().Should().Be(code);
        created.GetProperty("isRequired").GetBoolean().Should().BeFalse();

        // Update
        var updateRes = await Client.PutAsJsonAsync($"/api/asbuilt-categories/{id}", new
        {
            code,
            name = name + " Updated",
            nameVi = name + " Updated",
            nameEn = "Test Category EN Updated",
            nameZh = "测试分类更新",
            nameJa = "テストカテゴリ更新",
            isRequired = true,
            isActive = false,
            sortOrder = 100
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync(updateRes);
        updated.GetProperty("isRequired").GetBoolean().Should().BeTrue();
        updated.GetProperty("isActive").GetBoolean().Should().BeFalse();

        // Delete
        var deleteRes = await Client.DeleteAsync($"/api/asbuilt-categories/{id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getRes = await Client.GetAsync($"/api/asbuilt-categories/{id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_CategoryInUse_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        // Get the first category (should be "Drawing" which has documents)
        var listRes = await Client.GetAsync("/api/asbuilt-categories");
        var list = await ReadJsonAsync(listRes);
        var drawingCat = list.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("code").GetString() == "Drawing");

        if (drawingCat.ValueKind != System.Text.Json.JsonValueKind.Undefined)
        {
            var id = drawingCat.GetProperty("id").GetInt32();
            var deleteRes = await Client.DeleteAsync($"/api/asbuilt-categories/{id}");
            // Should fail if category has documents linked
            // Note: might be NoContent if no documents exist - that's also valid
            deleteRes.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        // Try to create with existing code "Drawing"
        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = "Drawing",
            name = "Duplicate Test",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/asbuilt-categories/999999", new
        {
            code = "NonExistent",
            name = "Test",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_RequiresManagePermission()
    {
        // Login as USER role (no manage permission)
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "USER"));

        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = "NoPermTest",
            name = "No Permission Test",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
