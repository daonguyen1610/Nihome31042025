using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NihomeBackend.IntegrationTests.Infrastructure;

public static class AuthTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Task<string> LoginAsSuperAdminAsync(HttpClient client) =>
        LoginAsync(client, TestDataSeeder.SuperAdminPhone, TestDataSeeder.DefaultPassword);

    public static Task<string> LoginAsAdminAsync(HttpClient client) =>
        LoginAsync(client, TestDataSeeder.AdminPhone, TestDataSeeder.DefaultPassword);

    public static Task<string> LoginAsCustomerAsync(HttpClient client) =>
        LoginAsync(client, TestDataSeeder.CustomerPhone, TestDataSeeder.DefaultPassword);

    /// <summary>
    /// Log in as the seeded test user for any role code (system or business).
    /// Throws when the code is not in the seeded test matrix so tests fail
    /// loudly rather than silently using the wrong identity.
    /// </summary>
    public static Task<string> LoginAsRoleAsync(HttpClient client, string roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
            throw new ArgumentException("Role code is required.", nameof(roleCode));

        var phone = roleCode.ToUpperInvariant() switch
        {
            "SUPER_ADMIN" => TestDataSeeder.SuperAdminPhone,
            "ADMIN" => TestDataSeeder.AdminPhone,
            "USER" => TestDataSeeder.CustomerPhone,
            _ when TestDataSeeder.BusinessRolePhonesByCode.TryGetValue(roleCode, out var p) => p,
            _ => throw new ArgumentException($"No seeded test user for role '{roleCode}'.", nameof(roleCode)),
        };
        return LoginAsync(client, phone, TestDataSeeder.DefaultPassword);
    }

    public static async Task<string> LoginAsync(HttpClient client, string phoneNumber, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { phoneNumber, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LoginPayload>(JsonOptions)
            ?? throw new InvalidOperationException("Login response was empty.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Login response did not include an access token.");
        }

        return payload.AccessToken;
    }

    public static async Task AuthenticateAsync(HttpClient client, Func<HttpClient, Task<string>> loginFn)
    {
        var token = await loginFn(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class LoginPayload
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Role { get; set; }
    }
}
