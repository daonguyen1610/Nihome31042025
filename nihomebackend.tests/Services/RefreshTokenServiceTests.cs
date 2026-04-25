using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;
using Xunit;

namespace nihomebackend.tests.Services;

public class RefreshTokenServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly RefreshTokenService _sut;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenServiceTests()
    {
        _db = DbContextFactory.Create();
        _jwtOptions = new JwtOptions
        {
            RefreshTokenDays = 7,
            AccessTokenMinutes = 30,
            Issuer = "test",
            Audience = "test",
            ActiveKeyId = "key1",
            Keys = new Dictionary<string, string> { { "key1", "secret" } }
        };
        _sut = new RefreshTokenService(_db, Options.Create(_jwtOptions));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task IssueAsync_CreatesTokenInDatabase()
    {
        var user = await SeedUser();

        var token = await _sut.IssueAsync(user);

        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.Equal(user.Id, token.UserId);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public async Task IssueAsync_SetsExpiryBasedOnConfig()
    {
        var user = await SeedUser();
        var before = DateTime.UtcNow;

        var token = await _sut.IssueAsync(user);

        var expectedExpiry = before.AddDays(_jwtOptions.RefreshTokenDays);
        Assert.True(token.ExpiresAt >= expectedExpiry.AddSeconds(-5));
        Assert.True(token.ExpiresAt <= expectedExpiry.AddSeconds(5));
    }

    [Fact]
    public async Task ValidateAsync_ValidToken_ReturnsToken()
    {
        var user = await SeedUser();
        var issued = await _sut.IssueAsync(user);

        var result = await _sut.ValidateAsync(issued.Token);

        Assert.NotNull(result);
        Assert.Equal(issued.Token, result.Token);
    }

    [Fact]
    public async Task ValidateAsync_NonExistentToken_ReturnsNull()
    {
        var result = await _sut.ValidateAsync("non-existent-token");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_RevokedToken_ReturnsNull()
    {
        var user = await SeedUser();
        var issued = await _sut.IssueAsync(user);
        await _sut.RevokeAsync(issued);

        var result = await _sut.ValidateAsync(issued.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredToken_ReturnsNull()
    {
        var user = await SeedUser();
        var token = new RefreshToken
        {
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        var result = await _sut.ValidateAsync(token.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_SetsIsRevokedTrue()
    {
        var user = await SeedUser();
        var issued = await _sut.IssueAsync(user);

        await _sut.RevokeAsync(issued);

        var fromDb = await _db.RefreshTokens.FindAsync(issued.Id);
        Assert.True(fromDb!.IsRevoked);
    }

    [Fact]
    public async Task ValidateAsync_IncludesUserNavigation()
    {
        var user = await SeedUser();
        var issued = await _sut.IssueAsync(user);

        var result = await _sut.ValidateAsync(issued.Token);

        Assert.NotNull(result?.User);
        Assert.Equal(user.PhoneNumber, result!.User.PhoneNumber);
    }

    private async Task<ApplicationUser> SeedUser()
    {
        var user = new ApplicationUser
        {
            PhoneNumber = "0123456789",
            FullName = "Test User",
            PasswordHash = "hashed",
            Role = UserRole.USER,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}
