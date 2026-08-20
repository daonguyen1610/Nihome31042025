using System.Security.Cryptography;
using System.Text;

namespace NihomeBackend.Services;

/// <summary>
/// Computes the request identity used to bind an idempotency key to one
/// HTTP operation and payload.
/// </summary>
public sealed class FingerprintService
{
    public async Task<string> ComputeAsync(HttpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.EnableBuffering();
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        request.Body.Position = 0;

        var payload = string.Join('|',
            request.Method,
            request.Path,
            request.QueryString,
            request.ContentType ?? string.Empty,
            request.Headers.AcceptLanguage.ToString(),
            body);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
