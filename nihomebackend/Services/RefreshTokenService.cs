using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class RefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwt;

    public RefreshTokenService(AppDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    public async Task<RefreshToken> IssueAsync(ApplicationUser user)
    {
        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();
        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateAsync(string token)
    {
        var refreshToken = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token);
        if (refreshToken == null || refreshToken.IsRevoked || refreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return refreshToken;
    }

    public async Task RevokeAsync(RefreshToken refreshToken)
    {
        refreshToken.IsRevoked = true;
        await _db.SaveChangesAsync();
    }
}
