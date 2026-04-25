using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services;
using Xunit;

namespace nihomebackend.tests.Services;

public class JwtServiceTests
{
    private readonly JwtService _sut;
    private readonly JwtOptions _jwtOptions;

    public JwtServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 7,
            ActiveKeyId = "key1",
            Keys = new Dictionary<string, string>
            {
                { "key1", "this-is-a-test-secret-key-that-must-be-at-least-32-chars!" }
            }
        };

        _sut = new JwtService(
            Options.Create(_jwtOptions),
            Mock.Of<ILogger<JwtService>>());
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwtString()
    {
        var user = CreateUser();

        var token = _sut.GenerateAccessToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        var user = CreateUser();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("1", jwt.Claims.First(c => c.Type == "uid").Value);
        Assert.Equal("0123456789", jwt.Claims.First(c => c.Type == "phone").Value);
        Assert.Equal("USER", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateAccessToken_SetsCorrectIssuerAndAudience()
    {
        var user = CreateUser();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_SetsKidInHeader()
    {
        var user = CreateUser();

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("key1", jwt.Header["kid"]?.ToString());
    }

    [Fact]
    public void GenerateAccessToken_SetsExpiry()
    {
        var user = CreateUser();
        var before = DateTime.UtcNow;

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var expectedExpiry = before.AddMinutes(_jwtOptions.AccessTokenMinutes);

        Assert.True(jwt.ValidTo <= expectedExpiry.AddSeconds(5));
        Assert.True(jwt.ValidTo >= expectedExpiry.AddSeconds(-5));
    }

    [Fact]
    public void GenerateAccessToken_InvalidKeyId_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            ActiveKeyId = "missing-key",
            Keys = new Dictionary<string, string>()
        };

        var sut = new JwtService(
            Options.Create(options),
            Mock.Of<ILogger<JwtService>>());

        Assert.Throws<InvalidOperationException>(() => sut.GenerateAccessToken(CreateUser()));
    }

    [Fact]
    public void GenerateAccessToken_EmptyKeyValue_ThrowsInvalidOperationException()
    {
        var options = new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            ActiveKeyId = "key1",
            Keys = new Dictionary<string, string> { { "key1", "" } }
        };

        var sut = new JwtService(
            Options.Create(options),
            Mock.Of<ILogger<JwtService>>());

        Assert.Throws<InvalidOperationException>(() => sut.GenerateAccessToken(CreateUser()));
    }

    [Fact]
    public void GenerateAccessToken_SuperAdmin_HasCorrectRole()
    {
        var user = CreateUser(role: UserRole.SUPER_ADMIN);

        var token = _sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("SUPER_ADMIN", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    private static ApplicationUser CreateUser(
        int id = 1,
        string phone = "0123456789",
        UserRole role = UserRole.USER) =>
        new()
        {
            Id = id,
            PhoneNumber = phone,
            FullName = "Test User",
            PasswordHash = "hashed",
            Role = role,
            IsActive = true
        };
}
