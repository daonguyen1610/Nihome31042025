using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class JwtService
{
    private readonly JwtOptions _jwt;
    private readonly ILogger<JwtService> _logger;

    public JwtService(IOptions<JwtOptions> options, ILogger<JwtService> logger)
    {
        _jwt = options.Value;
        _logger = logger;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        if (!_jwt.Keys.TryGetValue(_jwt.ActiveKeyId, out var secret) || string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"JWT key '{_jwt.ActiveKeyId}' is invalid.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("uid", user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("phone", user.PhoneNumber),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToUpperInvariant())
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        token.Header["kid"] = _jwt.ActiveKeyId;
        _logger.LogInformation("JWT token generated for user {UserId}", user.Id);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
