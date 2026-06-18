using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace NihomeBackend.Services;

/// <summary>
/// Derives a stable, opaque fingerprint per request without relying on
/// browser-side libraries. Combines User-Agent, client IP and primary
/// Accept-Language so that a retry from the same client/device produces
/// the same hash, while a different machine looks distinct.
/// </summary>
public sealed class FingerprintService
{
    /// <summary>Lower-cased hex SHA-256 of UA + IP + AcceptLanguage.</summary>
    public string Compute(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var ua = httpContext.Request.Headers.UserAgent.ToString();
        var lang = httpContext.Request.Headers.AcceptLanguage.ToString();
        var ip = ResolveClientIp(httpContext);

        var payload = $"{ua}|{ip}|{lang}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ResolveClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out var parsed))
            {
                return parsed.ToString();
            }
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
