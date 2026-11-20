using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public enum UploadChunkStatus
{
    Accepted,
    /// <summary>The chunk does not start where the session stands; resume from ReceivedBytes.</summary>
    OffsetMismatch,
    /// <summary>Too large, empty, past the declared size, or cut short.</summary>
    Invalid,
    NotFound,
}

public sealed record UploadChunkResult(UploadChunkStatus Status, long ReceivedBytes);

public sealed record CompletedUpload(string StagingPath, string FileName, long FileSize);

/// <summary>
/// Resumable chunked uploads. A session belongs to the user who opened it; chunks must
/// arrive in order, a resent chunk that was already stored is accepted without writing
/// it twice, and the staged file only becomes usable once every byte is in.
/// </summary>
public sealed class UploadSessionService(AppDbContext db, IWebHostEnvironment env)
{
    /// <summary>
    /// 8 MiB: far below the IIS default request limit (~28.6 MB), so chunks pass even
    /// where the server's request filtering was never raised.
    /// </summary>
    public const int ChunkSize = 8 * 1024 * 1024;

    /// <summary>An idle session is removed after this; each stored chunk extends it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    // One writer per session at a time, so concurrent retries cannot interleave bytes.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    private string StagingDirectory => Path.Combine(env.ContentRootPath, "App_Data", "upload-sessions");

    public string StagingPath(Guid id) => Path.Combine(StagingDirectory, $"{id:N}.part");

    public async Task<UploadSession> CreateAsync(int userId, UploadSessionPurpose purpose, int contextId,
        string fileName, long fileSize, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = new UploadSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = purpose,
            ContextId = contextId,
            FileName = fileName,
            FileSize = fileSize,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now + Lifetime,
        };
        Directory.CreateDirectory(StagingDirectory);
        await File.WriteAllBytesAsync(StagingPath(session.Id), [], ct);
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public Task<UploadSession?> FindAsync(Guid id, int userId, CancellationToken ct = default) =>
        db.UploadSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.ExpiresAt > DateTime.UtcNow, ct);

    public async Task<UploadChunkResult> AppendAsync(Guid id, int userId, long offset, Stream body,
        long? length, CancellationToken ct = default)
    {
        var gate = Locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var session = await db.UploadSessions
                .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.ExpiresAt > DateTime.UtcNow, ct);
            if (session is null) return new(UploadChunkStatus.NotFound, 0);
            if (length is not > 0 || length > ChunkSize || offset < 0 || offset + length > session.FileSize)
                return new(UploadChunkStatus.Invalid, session.ReceivedBytes);
            // Already stored: the client lost our reply and sent it again.
            if (offset + length <= session.ReceivedBytes)
                return new(UploadChunkStatus.Accepted, session.ReceivedBytes);
            if (offset != session.ReceivedBytes)
                return new(UploadChunkStatus.OffsetMismatch, session.ReceivedBytes);

            await using (var file = new FileStream(StagingPath(id), FileMode.OpenOrCreate, FileAccess.Write))
            {
                // Drop bytes of an earlier attempt that never completed.
                file.SetLength(session.ReceivedBytes);
                file.Seek(session.ReceivedBytes, SeekOrigin.Begin);
                var buffer = new byte[81920];
                long written = 0;
                int read;
                while (written < length && (read = await body.ReadAsync(
                           buffer.AsMemory(0, (int)Math.Min(buffer.Length, length.Value - written)), ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read), ct);
                    written += read;
                }
                if (written != length)
                {
                    file.SetLength(session.ReceivedBytes);
                    return new(UploadChunkStatus.Invalid, session.ReceivedBytes);
                }
            }

            var now = DateTime.UtcNow;
            session.ReceivedBytes += length.Value;
            session.UpdatedAt = now;
            session.ExpiresAt = now + Lifetime;
            await db.SaveChangesAsync(ct);
            return new(UploadChunkStatus.Accepted, session.ReceivedBytes);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// The finished file of the caller's session for this purpose and record, or null when
    /// it is not theirs, not for this record, or still missing bytes. The caller moves the
    /// file and then calls <see cref="ReleaseAsync"/>.
    /// </summary>
    public async Task<CompletedUpload?> TakeCompletedAsync(Guid id, int userId, UploadSessionPurpose purpose,
        int contextId, CancellationToken ct = default)
    {
        var session = await FindAsync(id, userId, ct);
        if (session is null || session.Purpose != purpose || session.ContextId != contextId ||
            session.ReceivedBytes != session.FileSize || !File.Exists(StagingPath(id)))
            return null;
        return new CompletedUpload(StagingPath(id), session.FileName, session.FileSize);
    }

    public async Task ReleaseAsync(Guid id, CancellationToken ct = default)
    {
        await db.UploadSessions.Where(item => item.Id == id).ExecuteDeleteOrRemoveAsync(db, ct);
        DeleteStaged(id);
    }

    public async Task<bool> CancelAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (await FindAsync(id, userId, ct) is null) return false;
        await ReleaseAsync(id, ct);
        return true;
    }

    /// <summary>Removes sessions idle past their expiry with their staged bytes.</summary>
    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken ct = default)
    {
        var expired = await db.UploadSessions.Where(item => item.ExpiresAt <= now)
            .Select(item => item.Id).ToListAsync(ct);
        foreach (var id in expired) await ReleaseAsync(id, ct);
        return expired.Count;
    }

    private void DeleteStaged(Guid id)
    {
        Locks.TryRemove(id, out _);
        var path = StagingPath(id);
        if (File.Exists(path)) File.Delete(path);
    }
}

internal static class UploadSessionQueryExtensions
{
    /// <summary>ExecuteDelete on SQL Server; tracked removal for the in-memory test provider.</summary>
    public static async Task ExecuteDeleteOrRemoveAsync(this IQueryable<UploadSession> query, AppDbContext db,
        CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await query.ExecuteDeleteAsync(ct);
            return;
        }
        db.UploadSessions.RemoveRange(await query.ToListAsync(ct));
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Removes upload sessions left idle past their lifetime, with their staged bytes.</summary>
public sealed class UploadSessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<UploadSessionCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sessions = scope.ServiceProvider.GetRequiredService<UploadSessionService>();
                var count = await sessions.DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (count > 0) logger.LogInformation("Removed {Count} expired upload sessions", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Upload session cleanup failed");
            }

            try
            {
                await Task.Delay(RunInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
