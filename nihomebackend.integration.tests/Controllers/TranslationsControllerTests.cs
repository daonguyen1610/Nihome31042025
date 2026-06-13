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
}
