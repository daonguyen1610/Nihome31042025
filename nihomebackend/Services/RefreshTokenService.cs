using Microsoft.Extensions.Options;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class RefreshTokenService
{
    private readonly AuthStore _authStore;
    private readonly JwtOptions _jwt;

    public RefreshTokenService(AuthStore authStore, IOptions<JwtOptions> jwt)
    {
        _authStore = authStore;
        _jwt = jwt.Value;
    }

    public Task<RefreshToken> IssueAsync(ApplicationUser user)
    {
        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        };

        _authStore.SaveRefreshToken(refreshToken);
        return Task.FromResult(refreshToken);
    }

    public Task<RefreshToken?> ValidateAsync(string token)
    {
        var refreshToken = _authStore.GetRefreshToken(token);
        if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Task.FromResult<RefreshToken?>(null);
        }

        return Task.FromResult<RefreshToken?>(refreshToken);
    }

    public Task RevokeAsync(RefreshToken refreshToken)
    {
        refreshToken.IsRevoked = true;
        return Task.CompletedTask;
    }
}
