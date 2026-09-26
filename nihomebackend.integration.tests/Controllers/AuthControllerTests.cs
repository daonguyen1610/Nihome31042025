using System.Net;
using System.Text.Json;

namespace NihomeBackend.IntegrationTests.Controllers;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Login_WithValidSuperAdminCredentials_ReturnsAccessToken()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = TestDataSeeder.SuperAdminPhone,
            password = TestDataSeeder.DefaultPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("role").GetString().Should().Be("SUPER_ADMIN");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = TestDataSeeder.SuperAdminPhone,
            password = "wrong-password",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithValidSuperAdminEmail_ReturnsAccessToken()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = TestDataSeeder.SuperAdminEmail,
            password = TestDataSeeder.DefaultPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("role").GetString().Should().Be("SUPER_ADMIN");
        doc.RootElement.GetProperty("phoneNumber").GetString().Should().Be(TestDataSeeder.SuperAdminPhone);
    }

    [Fact]
    public async Task Login_WithMixedCaseEmail_ReturnsAccessToken()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = "SuperAdmin@Nihome.test",
            password = TestDataSeeder.DefaultPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithMalformedIdentifier_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = "not-a-phone-or-email",
            password = TestDataSeeder.DefaultPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotStart_WithSuperAdminEmail_ReturnsAccountPhone()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/forgot/start", new
        {
            phoneNumber = TestDataSeeder.SuperAdminEmail,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("phone").GetString().Should().Be(TestDataSeeder.SuperAdminPhone);
    }

    [Fact]
    public async Task Login_WithUnknownPhone_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            phoneNumber = "0000000000",
            password = TestDataSeeder.DefaultPassword,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/job-applications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithAdminToken_ReturnsSuccess()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsAdminAsync);

        var response = await Client.GetAsync("/api/job-applications");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedAdminEndpoint_WithCustomerToken_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsCustomerAsync);

        var response = await Client.GetAsync("/api/job-applications");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
