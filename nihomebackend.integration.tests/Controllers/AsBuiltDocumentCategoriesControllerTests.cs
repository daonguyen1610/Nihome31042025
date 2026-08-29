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
            nameVi = "Danh mục thiết kế",
            nameEn = "Design category",
            nameZh = "设计类别",
            nameJa = "設計カテゴリ",
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
            nameVi = "Danh mục trùng",
            nameEn = "Duplicate category",
            nameZh = "重复类别",
            nameJa = "重複カテゴリ",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("nameVi")]
    [InlineData("nameEn")]
    [InlineData("nameZh")]
    [InlineData("nameJa")]
    public async Task Create_MissingLocalizedName_ReturnsBadRequest(string missingField)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var payload = new Dictionary<string, object?>
        {
            ["code"] = $"Missing{Guid.NewGuid():N}"[..20],
            ["nameVi"] = "Danh mục kiểm thử",
            ["nameEn"] = "Test category",
            ["nameZh"] = "测试类别",
            ["nameJa"] = "テストカテゴリ",
            ["isRequired"] = false,
            ["isActive"] = true,
            ["sortOrder"] = 10,
        };
        payload.Remove(missingField);

        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", payload);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await ReadJsonAsync(res);
        json.GetProperty("errors")
            .EnumerateObject()
            .Any(property => property.Name.Equals(missingField, StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Create_WhitespaceLocalizedName_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var res = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code = $"Whitespace{Guid.NewGuid():N}"[..20],
            nameVi = "Danh mục kiểm thử",
            nameEn = "   ",
            nameZh = "测试类别",
            nameJa = "テストカテゴリ",
            isRequired = false,
            isActive = true,
            sortOrder = 10
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ChangingCode_ReturnsBadRequestAndPreservesCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var code = $"Immutable{Guid.NewGuid():N}"[..20];

        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            nameVi = "Mã không đổi",
            nameEn = "Immutable code",
            nameZh = "不可变编码",
            nameJa = "不変コード",
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
            nameVi = "Mã không đổi",
            nameEn = "Immutable code",
            nameZh = "不可变编码",
            nameJa = "不変コード",
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
            nameVi = "Danh mục vô hiệu",
            nameEn = "Inactive category",
            nameZh = "禁用类别",
            nameJa = "無効カテゴリ",
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
            nameVi = "Kiểm thử",
            nameEn = "Test",
            nameZh = "测试",
            nameJa = "テスト",
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
            nameVi = "Không có quyền",
            nameEn = "No permission",
            nameZh = "没有权限",
            nameJa = "権限なし",
            isRequired = false,
            isActive = true,
            sortOrder = 0
        });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
