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

public sealed record CompletedUpload(Guid Id, string StagingPath, string FileName, long FileSize);

/// <summary>The response an upload request got the first time, replayed when it is sent again.</summary>
public sealed record UploadReplay(int Status, string? ContentType, string Body);

/// <summary>
/// Resumable chunked uploads for every module. A session belongs to the user who opened
/// it and to the upload request it was opened for; chunks must arrive in order, a resent
/// chunk that was already stored is accepted without writing it twice, and the staged
/// file only becomes usable once every byte is in.
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

    /// <summary>Open sessions one user may keep at a time (a batch is at most 20 files).</summary>
    public const int MaxOpenSessionsPerUser = 50;

    public Task<int> CountOpenAsync(int userId, CancellationToken ct = default) =>
        db.UploadSessions.CountAsync(item => item.UserId == userId && item.UsedAt == null && item.ExpiresAt > DateTime.UtcNow, ct);

    public async Task<UploadSession> CreateAsync(int userId, string targetPath, string fileName, long fileSize,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = new UploadSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetPath = targetPath,
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
        // Only real sessions get a lock, so unknown ids cannot grow the lock table.
        if (await FindAsync(id, userId, ct) is null) return new(UploadChunkStatus.NotFound, 0);
        UploadSession? session = null;
        var gate = await LockAsync(id, async () => (session = await db.UploadSessions
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId && item.ExpiresAt > DateTime.UtcNow, ct)) is not null, ct);
        if (gate is null || session is null) return new(UploadChunkStatus.NotFound, 0);
        try
        {
            if (length is not > 0 || length > ChunkSize || offset < 0 || offset + length > session.FileSize)
                return new(UploadChunkStatus.Invalid, session.ReceivedBytes);
            // Already stored: the client lost our reply and sent it again.
            if (offset + length <= session.ReceivedBytes)
                return new(UploadChunkStatus.Accepted, session.ReceivedBytes);
            if (offset != session.ReceivedBytes)
                return new(UploadChunkStatus.OffsetMismatch, session.ReceivedBytes);
            if (!StagedFileHolds(id, session.ReceivedBytes, exactly: false))
            {
                // The staged bytes were lost (e.g. App_Data replaced on deploy); padding the
                // gap would store a corrupt file, so the client starts over.
                await ReleaseAsync(id, ct);
                return new(UploadChunkStatus.NotFound, 0);
            }

            await using (var file = new FileStream(StagingPath(id), FileMode.Open, FileAccess.Write))
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
    /// Holds the finished files of the caller's sessions for the upload request at
    /// <paramref name="targetPath"/>, under each session's lock so a double submit cannot
    /// use them twice. Null when any session is not theirs, not for this request, or still
    /// missing bytes. When the request already succeeded with these sessions, the held
    /// result carries that response to replay instead of files.
    /// </summary>
    public async Task<HeldUploads?> HoldCompletedAsync(IReadOnlyCollection<Guid> ids, int userId, string targetPath,
        CancellationToken ct = default)
    {
        var held = new HeldUploads(this);
        try
        {
            // A fixed order, so two requests holding the same sessions cannot deadlock.
            foreach (var id in ids.Distinct().Order())
            {
                // Only real sessions get a lock, so unknown ids cannot grow the lock table.
                if (await FindAsync(id, userId, ct) is null) return await held.AbandonAsync();
                // Read again under the lock: a concurrent request may have used it meanwhile.
                UploadSession? session = null;
                var gate = await LockAsync(id, async () => (session = await FindAsync(id, userId, ct)) is not null, ct);
                if (gate is null || session is null) return await held.AbandonAsync();
                held.Gates.Add(gate);
                if (!string.Equals(session.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase) ||
                    session.ReceivedBytes != session.FileSize)
                    return await held.AbandonAsync();
                if (session.UsedAt is not null)
                {
                    // The same request already succeeded; its reply never reached the client.
                    held.Replay ??= new UploadReplay(session.ResultStatus ?? StatusCodes.Status200OK,
                        session.ResultContentType, session.ResultBody ?? string.Empty);
                    held.UsedIds.Add(id);
                    continue;
                }
                if (!StagedFileHolds(id, session.FileSize, exactly: true))
                {
                    // The staged bytes were lost (e.g. App_Data replaced): nothing left to resume.
                    await ReleaseAsync(id, ct);
                    return await held.AbandonAsync();
                }
                held.Files.Add(new CompletedUpload(id, StagingPath(id), session.FileName, session.FileSize));
            }
            // Partly used sessions do not form a request that was ever sent.
            if (held.UsedIds.Count > 0 && held.Files.Count > 0) return await held.AbandonAsync();
            return held;
        }
        catch
        {
            await held.AbandonAsync();
            throw;
        }
    }

    /// <summary>Finished files held for one request; disposing lets other requests at them.</summary>
    public sealed class HeldUploads(UploadSessionService owner) : IAsyncDisposable
    {
        internal List<SemaphoreSlim> Gates { get; } = [];
        internal List<Guid> UsedIds { get; } = [];
        public List<CompletedUpload> Files { get; } = [];

        /// <summary>Set when this exact request already succeeded: answer with it again.</summary>
        public UploadReplay? Replay { get; internal set; }

        /// <summary>
        /// The request succeeded: drop the staged bytes, and keep its response until the
        /// sessions expire so a resend after a lost reply gets it again.
        /// </summary>
        public Task SucceededAsync(UploadReplay response) => owner.MarkUsedAsync(Files, response);

        /// <summary>The module refused the files for good: drop the sessions entirely.</summary>
        public async Task RefusedAsync()
        {
            foreach (var file in Files) await owner.ReleaseAsync(file.Id, CancellationToken.None);
        }

        internal async Task<HeldUploads?> AbandonAsync()
        {
            await DisposeAsync();
            return null;
        }

        public ValueTask DisposeAsync()
        {
            foreach (var gate in Gates) gate.Release();
            Gates.Clear();
            return ValueTask.CompletedTask;
        }
    }

    public async Task ReleaseAsync(Guid id, CancellationToken ct = default)
    {
        await RemoveRowsAsync([id], ct);
        DeleteStaged(id);
    }

    private async Task MarkUsedAsync(IReadOnlyCollection<CompletedUpload> files, UploadReplay response)
    {
        if (response.Body.Length > UploadSession.ResultBodyMaxLength)
        {
            // Too large to keep: close as before; a resend then starts over.
            foreach (var file in files) await ReleaseAsync(file.Id, CancellationToken.None);
            return;
        }
        var ids = files.Select(file => file.Id).ToList();
        var now = DateTime.UtcNow;
        foreach (var session in await db.UploadSessions.Where(item => ids.Contains(item.Id)).ToListAsync())
        {
            session.UsedAt = now;
            session.ResultStatus = response.Status;
            session.ResultContentType = response.ContentType;
            session.ResultBody = response.Body;
            session.ExpiresAt = now + Lifetime;
        }
        await db.SaveChangesAsync(CancellationToken.None);
        // The module has its own copy; the lock stays until the row expires.
        foreach (var id in ids) TryDelete(StagingPath(id));
    }

    /// <summary>Tracked removal, the same on SQL Server and the test provider (rows are few).</summary>
    private async Task RemoveRowsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = await db.UploadSessions.Where(item => ids.Contains(item.Id)).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.UploadSessions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> CancelAsync(Guid id, int userId, CancellationToken ct = default)
    {
        if (await FindAsync(id, userId, ct) is null) return false;
        // Waits for a chunk still being written, so its file is not deleted under it.
        var gate = await LockAsync(id, async () => await FindAsync(id, userId, ct) is not null, ct);
        if (gate is null) return false;
        try
        {
            await ReleaseAsync(id, ct);
        }
        finally
        {
            gate.Release();
        }
        return true;
    }

    /// <summary>
    /// Removes sessions idle past their expiry with their staged bytes, skipping any still
    /// in use (the next run takes them), and staged files no session refers to any more.
    /// </summary>
    public async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken ct = default)
    {
        var expired = await db.UploadSessions.Where(item => item.ExpiresAt <= now)
            .Select(item => item.Id).ToListAsync(ct);
        var taken = new List<(Guid Id, SemaphoreSlim Gate)>();
        foreach (var id in expired)
        {
            var gate = Locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
            if (gate.Wait(0)) taken.Add((id, gate));
        }
        try
        {
            var ids = taken.Select(item => item.Id).ToList();
            if (ids.Count > 0)
            {
                await RemoveRowsAsync(ids, ct);
                foreach (var id in ids) DeleteStaged(id);
            }
        }
        finally
        {
            foreach (var (id, gate) in taken)
            {
                gate.Release();
                Locks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(id, gate));
            }
        }

        // A file whose delete failed earlier (e.g. still open on Windows) has no row left.
        if (Directory.Exists(StagingDirectory))
        {
            var known = (await db.UploadSessions.Select(item => item.Id).ToListAsync(ct)).ToHashSet();
            foreach (var path in Directory.EnumerateFiles(StagingDirectory, "*.part"))
            {
                if (Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var id) && !known.Contains(id) &&
                    File.GetLastWriteTimeUtc(path) <= now - Lifetime)
                    TryDelete(path);
            }
        }
        return taken.Count;
    }

    /// <summary>
    /// Takes the session's lock and confirms under it that the session still exists; null
    /// when it is gone, leaving no lock behind for it.
    /// </summary>
    private async Task<SemaphoreSlim?> LockAsync(Guid id, Func<Task<bool>> exists, CancellationToken ct)
    {
        var gate = Locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        bool found;
        try
        {
            found = await exists();
        }
        catch
        {
            gate.Release();
            throw;
        }
        if (found) return gate;
        gate.Release();
        Locks.TryRemove(new KeyValuePair<Guid, SemaphoreSlim>(id, gate));
        return null;
    }

    private bool StagedFileHolds(Guid id, long bytes, bool exactly)
    {
        var file = new FileInfo(StagingPath(id));
        return file.Exists && (exactly ? file.Length == bytes : file.Length >= bytes);
    }

    private void DeleteStaged(Guid id)
    {
        Locks.TryRemove(id, out _);
        TryDelete(StagingPath(id));
    }

    // A file that cannot be deleted now (e.g. still open) is swept by DeleteExpiredAsync later.
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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
