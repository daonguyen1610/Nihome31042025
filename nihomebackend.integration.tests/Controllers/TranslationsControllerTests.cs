using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

public class TranslationsControllerTests : IntegrationTestBase
{
    public TranslationsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetByLang_IsPublic_ReturnsOk()
    {
        (await Client.GetAsync("/api/translations/en")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpsertPair_WithoutAuth_ReturnsUnauthorized()
    {
        var res = await Client.PostAsJsonAsync("/api/translations/pair", new
        {
            key = "test.key",
            vietnameseValue = "Xin chào",
            translations = new Dictionary<string, string> { ["en"] = "Hello" },
            category = "test",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpsertPair_AsAdmin_ReturnsOk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var key = $"test.key.{Guid.NewGuid():N}".Substring(0, 24);
        var res = await Client.PostAsJsonAsync("/api/translations/pair", new
        {
            key,
            vietnameseValue = "Xin chào",
            translations = new Dictionary<string, string> { ["en"] = "Hello", ["zh"] = "你好", ["ja"] = "こんにちは" },
            category = "test",
        });
        res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        (await Client.DeleteAsync($"/api/translations/key/{key}")).StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    [Fact]
    public async Task AsBuiltCategory_TranslationRoundTrip_UsesContentTranslations()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var typesRes = await Client.GetAsync("/api/translations/entity/types");
        typesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await ReadJsonAsync(typesRes);
        types.EnumerateArray().Should().Contain(item =>
            item.GetProperty("type").GetString() == "AsBuiltDocumentCategory");

        var code = $"Translated{Guid.NewGuid():N}"[..24];
        var createRes = await Client.PostAsJsonAsync("/api/asbuilt-categories", new
        {
            code,
            name = "Ảnh thi công",
            isRequired = false,
            isActive = true,
            sortOrder = 90,
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createRes);
        var id = created.GetProperty("id").GetInt32();
        created.GetProperty("nameEn").GetString().Should().Be("Ảnh thi công");

        var saveRes = await Client.PostAsJsonAsync(
            $"/api/translations/entity/AsBuiltDocumentCategory/{id}",
            new
            {
                languageCode = "en",
                translations = new Dictionary<string, string> { ["Name"] = "Construction photos" },
            });
        saveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusRes = await Client.GetAsync("/api/translations/entity/AsBuiltDocumentCategory");
        var status = await ReadJsonAsync(statusRes);
        var translatedItem = status.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt32() == id);
        translatedItem.GetProperty("translationCount").GetInt32().Should().Be(1);
        translatedItem.GetProperty("expectedFields").GetInt32().Should().Be(3);

        var detailRes = await Client.GetAsync($"/api/translations/entity/AsBuiltDocumentCategory/{id}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await ReadJsonAsync(detailRes);
        detail.GetProperty("original").GetProperty("Name").GetString().Should().Be("Ảnh thi công");
        detail.GetProperty("translations").GetProperty("en").GetProperty("Name")
            .GetString().Should().Be("Construction photos");
        detail.GetProperty("translations").GetProperty("zh").GetProperty("Name")
            .GetString().Should().BeEmpty();

        var updateRes = await Client.PutAsJsonAsync($"/api/asbuilt-categories/{id}", new
        {
            code,
            name = "Hình ảnh thi công",
            isRequired = false,
            isActive = true,
            sortOrder = 90,
        });
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await ReadJsonAsync(updateRes);
        updated.GetProperty("nameEn").GetString().Should().Be("Construction photos");
        updated.GetProperty("nameZh").GetString().Should().Be("Hình ảnh thi công");
        updated.GetProperty("nameJa").GetString().Should().Be("Hình ảnh thi công");

        var resetRes = await Client.DeleteAsync($"/api/translations/entity/AsBuiltDocumentCategory/{id}");
        resetRes.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var resetCategoryRes = await Client.GetAsync($"/api/asbuilt-categories/{id}");
        var resetCategory = await ReadJsonAsync(resetCategoryRes);
        resetCategory.GetProperty("nameEn").GetString().Should().Be("Hình ảnh thi công");

        var missingRes = await Client.GetAsync("/api/translations/entity/AsBuiltDocumentCategory/999999");
        missingRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Client.DeleteAsync($"/api/asbuilt-categories/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("vi", "Tên tiếng Việt không hợp lệ")]
    [InlineData("en", "   ")]
    public async Task AsBuiltCategory_InvalidTranslation_ReturnsBadRequest(string languageCode, string value)
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);
        var categoriesRes = await Client.GetAsync("/api/asbuilt-categories");
        var categories = await ReadJsonAsync(categoriesRes);
        var id = categories[0].GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync(
            $"/api/translations/entity/AsBuiltDocumentCategory/{id}",
            new
            {
                languageCode,
                translations = new Dictionary<string, string> { ["Name"] = value },
            });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
