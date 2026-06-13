using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Data;

namespace NihomeBackend.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests sharing a single web host per test class.
/// Uses a fresh HttpClient per test so authentication headers don't leak between tests.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<NihomeWebApplicationFactory>
{
    protected readonly NihomeWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(NihomeWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected T WithDb<T>(Func<AppDbContext, T> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return fn(db);
    }

    protected async Task WithDbAsync(Func<AppDbContext, Task> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await fn(db);
    }

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> fn)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await fn(db);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Builds a short unique slug suitable for column length limits.</summary>
    protected static string UniqueSlug(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}".Substring(0, Math.Min(40, prefix.Length + 9));
}
