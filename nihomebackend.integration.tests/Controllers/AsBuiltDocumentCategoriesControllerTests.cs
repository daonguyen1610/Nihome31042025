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
    public async Task GetAll_DesignRole_CanReadInactiveCategoriesButCannotManage()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "DESIGN"));

        var listRes = await Client.GetAsync("/api/asbuilt-categories?includeInactive=true");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = $"Design{Guid.NewGuid():N}"[..20],
            name = "Danh mục thiết kế",
            isRequired = false,
            isActive = true,
            sortOrder = 10
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
            isRequired = false,
            isActive = true,
            sortOrder = 99
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        var id = created.GetProperty("id").GetInt32();
        created.GetProperty("code").GetString().Should().Be(code);
        created.GetProperty("nameVi").GetString().Should().Be(name);
        created.GetProperty("nameEn").GetString().Should().Be(name);
        created.GetProperty("nameZh").GetString().Should().Be(name);
        created.GetProperty("nameJa").GetString().Should().Be(name);
        created.GetProperty("isRequired").GetBoolean().Should().BeFalse();

        // Update
        var updateRes = await Client.PutAsJsonAsync($"/api/asbuilt-categories/{id}", new
        {
            code,
            name = name + " Updated",
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

        var listRes = await Client.GetAsync("/api/asbuilt-categories");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await ReadJsonAsync(listRes);
        var drawingCat = list.EnumerateArray()
            .Single(c => c.GetProperty("code").GetString() == "Drawing");
        var id = drawingCat.GetProperty("id").GetInt32();

        await WithDbAsync(async db =>
        {
            var project = new NihomeBackend.Models.DesignProject
            {
                ProjectCode = $"DP-DELETE-{Guid.NewGuid():N}"[..30],
                Name = "Category deletion dependency project",
                CustomerId = 1,
            };
            db.DesignProjects.Add(project);
            await db.SaveChangesAsync();

            db.AsBuiltDocuments.Add(new NihomeBackend.Models.AsBuiltDocument
            {
                DesignProjectId = project.Id,
                CategoryId = id,
                DocumentCode = $"AB-DELETE-{Guid.NewGuid():N}"[..30],
                Title = "Document preventing category deletion",
            });
            await db.SaveChangesAsync();
        });

        var deleteRes = await Client.DeleteAsync($"/api/asbuilt-categories/{id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var getRes = await Client.GetAsync($"/api/asbuilt-categories/{id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var listRes = await Client.GetAsync("/api/asbuilt-categories");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await ReadJsonAsync(listRes);
        list.EnumerateArray()
            .Should().Contain(category => category.GetProperty("code").GetString() == "Drawing");

        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = "Drawing",
            name = "Danh mục trùng",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_MissingName_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var payload = new Dictionary<string, object?>
        {
            ["code"] = $"Missing{Guid.NewGuid():N}"[..20],
            ["isRequired"] = false,
            ["isActive"] = true,
            ["sortOrder"] = 10,
        };
        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", payload);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(res);
        json.GetProperty("errors")
            .EnumerateObject()
            .Any(property => property.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Create_WhitespaceName_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = $"Whitespace{Guid.NewGuid():N}"[..20],
            name = "   ",
            isRequired = false,
            isActive = true,
            sortOrder = 10
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_LegacyNameViAlias_RemainsSupported()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var code = $"Legacy{Guid.NewGuid():N}"[..20];

        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            nameVi = "Tên nguồn kiểu cũ",
            nameEn = "Legacy English name",
            nameZh = "旧版中文名称",
            nameJa = "旧版日本語名",
            isRequired = false,
            isActive = true,
            sortOrder = 90,
        });

        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        created.GetProperty("nameEn").GetString().Should().Be("Legacy English name");
        created.GetProperty("nameZh").GetString().Should().Be("旧版中文名称");
        created.GetProperty("nameJa").GetString().Should().Be("旧版日本語名");
        var id = created.GetProperty("id").GetInt32();
        (await Client.DeleteAsync($"/api/asbuilt-categories/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_ChangingCode_ReturnsBadRequestAndPreservesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var code = $"Immutable{Guid.NewGuid():N}"[..20];

        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            name = "Mã không đổi",
            isRequired = false,
            isActive = true,
            sortOrder = 10
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        var id = created.GetProperty("id").GetInt32();

        var updateRes = await Client.PutAsJsonAsync($"/api/asbuilt-categories/{id}", new
        {
            code = code + "Changed",
            name = "Mã không đổi",
            isRequired = false,
            isActive = true,
            sortOrder = 10
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var getRes = await Client.GetAsync($"/api/asbuilt-categories/{id}");
        var persisted = await ReadJsonAsync(getRes);
        persisted.GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task CreateDocument_WithInactiveCategory_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var code = $"Inactive{Guid.NewGuid():N}"[..20];

        var categoryRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            name = "Danh mục vô hiệu",
            isRequired = false,
            isActive = false,
            sortOrder = 10
        });
        categoryRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var projectId = await WithDbAsync(async db =>
        {
            var project = new NihomeBackend.Models.DesignProject
            {
                ProjectCode = $"DP-INACTIVE-{Guid.NewGuid():N}"[..30],
                Name = "Inactive category project",
                CustomerId = 1,
            };
            db.DesignProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });

        var documentRes = await Client.PostAsJsonAsync("/api/as-built-documents", new
        {
            designProjectId = projectId,
            title = "Must reject inactive category",
            category = code,
        });

        documentRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_NonExistent_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var res = await Client.PutAsJsonAsync("/api/asbuilt-categories/999999", new
        {
            code = "NonExistent",
            name = "Kiểm thử",
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
            name = "Không có quyền",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
