using System.Text.Json;
using System.Text.Json.Serialization;
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
    public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(5);

    /// <summary>Max accepted Idempotency-Key length (matches DB column).</summary>
    public const int MaxKeyLength = 120;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

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
        string? fingerprint = null,
        int? userId = null,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return null;

        var record = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, ct);

        if (record == null) return null;

        if (record.UserId != userId ||
            (!string.IsNullOrWhiteSpace(fingerprint) &&
             !string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal)))
        {
            throw new IdempotencyConflictException(
                "Idempotency-Key đã được dùng cho một yêu cầu khác.");
        }

        if (record.ExpiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        var headers = string.IsNullOrWhiteSpace(record.ResponseHeadersJson)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(record.ResponseHeadersJson, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return new CachedResponse(record.StatusCode, record.ResponseJson, headers);
    }

    public async Task<BeginResult> TryBeginAsync(
        string scope,
        string? key,
        string fingerprint,
        int? userId,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return BeginResult.Execute;

        var existing = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(record => record.Scope == scope && record.Key == key, ct);
        if (existing is not null)
        {
            if (existing.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogInformation(
                    "Removing expired idempotency record for {Scope}/{Key}", scope, key);
                _db.IdempotencyRecords.Remove(existing);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                ValidateIdentity(existing, fingerprint, userId);
                return existing.StatusCode == 0 ? BeginResult.InProgress : BeginResult.Replay;
            }
        }

        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Scope = scope,
            Key = key!,
            Fingerprint = fingerprint,
            UserId = userId,
            StatusCode = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(PendingTtl),
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            return BeginResult.Execute;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var winner = await _db.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(record => record.Scope == scope && record.Key == key, ct);
            if (winner is null) throw;
            ValidateIdentity(winner, fingerprint, userId);
            return winner.StatusCode == 0 ? BeginResult.InProgress : BeginResult.Replay;
        }
    }

    public async Task SaveAsync<TPayload>(
        string scope,
        string? key,
        string? fingerprint,
        int? userId,
        int statusCode,
        TPayload payload,
        IReadOnlyDictionary<string, string>? responseHeaders = null,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return;

        var json = payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions);

        var record = await _db.IdempotencyRecords
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, ct)
            ?? throw new InvalidOperationException("Idempotency reservation was not found.");
        ValidateIdentity(record, fingerprint ?? string.Empty, userId);
        if (record.StatusCode != 0)
        {
            throw new InvalidOperationException("Idempotency reservation is already completed.");
        }

        record.StatusCode = statusCode;
        record.ResponseJson = json;
        record.ResponseHeadersJson = responseHeaders is { Count: > 0 }
            ? JsonSerializer.Serialize(responseHeaders, JsonOptions)
            : null;
        record.ExpiresAt = DateTime.UtcNow.Add(DefaultTtl);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AbandonAsync(
        string scope,
        string? key,
        string fingerprint,
        int? userId,
        CancellationToken ct = default)
    {
        if (!IsValidKey(key)) return;
        var record = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(item => item.Scope == scope && item.Key == key && item.StatusCode == 0, ct);
        if (record is null) return;
        ValidateIdentity(record, fingerprint, userId);
        _db.IdempotencyRecords.Remove(record);
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateIdentity(IdempotencyRecord record, string fingerprint, int? userId)
    {
        if (record.UserId != userId ||
            !string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException(
                "Idempotency-Key đã được dùng cho một yêu cầu khác.");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public readonly record struct CachedResponse(
        int StatusCode,
        string? ResponseJson,
        IReadOnlyDictionary<string, string> Headers);
    public enum BeginResult { Execute, Replay, InProgress }
}

public sealed class IdempotencyConflictException(string message) : Exception(message);
