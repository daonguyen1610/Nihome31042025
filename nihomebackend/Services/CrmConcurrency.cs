using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class CrmConcurrencyException(string message) : Exception(message);
public sealed class CrmConcurrencyTokenException(string message) : Exception(message);

public static class CrmConcurrency
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static string ToEntityTag(string rowVersion) => $"\"{rowVersion}\"";

    public static string? ResolveRequestToken(HttpRequest request, string? bodyToken)
    {
        var headerToken = request.Headers[HeaderNames.IfMatch].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(bodyToken) && !string.IsNullOrWhiteSpace(headerToken) &&
            !string.Equals(Normalize(bodyToken), Normalize(headerToken), StringComparison.Ordinal))
        {
            throw new CrmConcurrencyTokenException(
                "RowVersion trong nội dung yêu cầu không khớp với If-Match.");
        }

        return !string.IsNullOrWhiteSpace(bodyToken) ? bodyToken : headerToken;
    }

    public static string ResolveRequiredRequestToken(HttpRequest request, string? bodyToken)
    {
        var token = ResolveRequestToken(request, bodyToken);
        if (string.IsNullOrWhiteSpace(token))
            throw new CrmConcurrencyTokenException(
                "Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại dữ liệu trước khi cập nhật.");
        return token;
    }

    public static void SetResponseEntityTag(HttpResponse response, string rowVersion)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            response.Headers.ETag = ToEntityTag(rowVersion);
        }
    }

    public static void Apply<TEntity>(AppDbContext db, TEntity entity, string? token)
        where TEntity : class, IConcurrencyTracked
    {
        if (string.IsNullOrWhiteSpace(token)) return;

        var normalized = Normalize(token);
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(normalized);
        }
        catch (FormatException)
        {
            throw new CrmConcurrencyTokenException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại dữ liệu.");
        }

        if (expectedVersion.Length != 8)
        {
            throw new CrmConcurrencyTokenException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại dữ liệu.");
        }

        db.Entry(entity).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    public static void EnsureMatches(byte[] rowVersion, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new CrmConcurrencyTokenException(
                "Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại dữ liệu trước khi xoá.");
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(Normalize(token));
        }
        catch (FormatException)
        {
            throw new CrmConcurrencyTokenException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại dữ liệu.");
        }
        if (expectedVersion.Length != 8)
            throw new CrmConcurrencyTokenException("Phiên bản dữ liệu không hợp lệ. Vui lòng tải lại dữ liệu.");
        if (!rowVersion.SequenceEqual(expectedVersion))
            throw new CrmConcurrencyException("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trước khi thử lại.");
    }

    public static async Task SaveChangesAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CrmConcurrencyException("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trước khi thử lại.");
        }
    }

    private static string Normalize(string token) => token.Trim().Trim('"');
}
