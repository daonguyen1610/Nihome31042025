using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Storage-backed Idempotency-Key replay protection. A second call with the
/// same (scope, key) within TTL returns the cached response instead of
/// re-executing the mutation.
/// </summary>
public sealed class IdempotencyService
{
    /// <summary>How long a cached response stays valid for replay.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    /// <summary>Max accepted Idempotency-Key length (matches DB column).</summary>
    public const int MaxKeyLength = 120;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(AppDbContext db, ILogger<IdempotencyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public static bool IsValidKey(string? key)
        => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxKeyLength;

    public async Task<CachedResponse?> TryGetCachedAsync(
        string scope,
        string? key,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return null;

        var record = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, ct);

        if (record == null) return null;

        if (record.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return new CachedResponse(record.StatusCode, record.ResponseJson);
    }

    public async Task SaveAsync<TPayload>(
        string scope,
        string? key,
        string? fingerprint,
        int? userId,
        int statusCode,
        TPayload payload,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return;

        var json = payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions);

        try
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Scope = scope,
                Key = key!,
                Fingerprint = fingerprint,
                UserId = userId,
                StatusCode = statusCode,
                ResponseJson = json,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(DefaultTtl),
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Race: another worker beat us to it. Safe to ignore — the original
            // record will satisfy future replays.
            _logger.LogDebug(ex, "Idempotency record already stored for {Scope}/{Key}", scope, key);
        }
    }

    public readonly record struct CachedResponse(int StatusCode, string? ResponseJson);
}
