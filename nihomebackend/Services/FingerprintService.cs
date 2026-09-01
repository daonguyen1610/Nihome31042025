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

        if (request.HasFormContentType)
        {
            return await ComputeFormAsync(request, ct);
        }

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

    private static async Task<string> ComputeFormAsync(HttpRequest request, CancellationToken ct)
    {
        request.EnableBuffering();
        var form = await request.ReadFormAsync(ct);
        if (request.Body.CanSeek) request.Body.Position = 0;

        var components = new List<string>
        {
            request.Method,
            request.Path,
            request.QueryString.ToString(),
            request.ContentType?.Split(';', 2)[0].Trim().ToLowerInvariant() ?? string.Empty,
            request.Headers.AcceptLanguage.ToString(),
        };
        foreach (var field in form.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            foreach (var value in field.Value.OrderBy(item => item, StringComparer.Ordinal))
            {
                components.Add($"field:{field.Key.Length}:{field.Key}:{value?.Length ?? 0}:{value}");
            }
        }
        foreach (var file in form.Files
                     .OrderBy(item => item.Name, StringComparer.Ordinal)
                     .ThenBy(item => item.FileName, StringComparer.Ordinal)
                     .ThenBy(item => item.ContentType, StringComparer.Ordinal))
        {
            await using var stream = file.OpenReadStream();
            var contentHash = await SHA256.HashDataAsync(stream, ct);
            components.Add($"file:{file.Name.Length}:{file.Name}:{file.FileName.Length}:{file.FileName}:" +
                $"{file.ContentType.Length}:{file.ContentType}:{file.Length}:{Convert.ToHexString(contentHash)}");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', components)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
