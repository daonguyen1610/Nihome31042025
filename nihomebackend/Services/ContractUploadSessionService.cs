using System.Text.Json;
using System.Text.RegularExpressions;

namespace NihomeBackend.Services;

/// <summary>A contract file being uploaded in chunks, owned by one user and contract.</summary>
public sealed record ContractUploadSession(
    string Id,
    int ContractId,
    int UserId,
    string FileName,
    long Size,
    string ContentType,
    DateTime CreatedAt);

/// <summary>Outcome of writing one chunk.</summary>
public enum ChunkWriteStatus
{
    Written,
    /// <summary>The chunk does not start where the received bytes end.</summary>
    OffsetMismatch,
    /// <summary>The chunk is larger than a chunk or than the bytes still due.</summary>
    TooLarge,
    /// <summary>Another request is writing this upload.</summary>
    Busy,
}

public sealed record ChunkWriteResult(ChunkWriteStatus Status, long ReceivedBytes);

/// <summary>
/// Stages large contract files sent in chunks so an upload can continue from the
/// last received byte after the connection drops. Chunks land in a private folder
/// outside wwwroot; the finished file is moved to contract storage by the caller.
/// Sessions are files on disk (no table): an abandoned one is removed by
/// <see cref="ContractUploadSessionCleanupWorker"/> after <see cref="Lifetime"/>.
/// </summary>
public sealed partial class ContractUploadSessionService(IWebHostEnvironment env)
{
    /// <summary>Bytes per chunk; small enough for any reverse proxy body limit.</summary>
    public const int ChunkSize = 8 * 1024 * 1024;

    /// <summary>How long an unfinished upload is kept for the user to continue it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const string MetadataFile = "session.json";
    private const string DataFile = "data.part";

    private string Root => Path.Combine(env.ContentRootPath, "App_Data", "contract-uploads");

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex SessionIdPattern();

    public ContractUploadSession Start(int contractId, int userId, string fileName, long size, string contentType)
    {
        var session = new ContractUploadSession(
            Guid.NewGuid().ToString("N"), contractId, userId, fileName, size, contentType, DateTime.UtcNow);
        var folder = Path.Combine(Root, session.Id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, MetadataFile), JsonSerializer.Serialize(session));
        File.Create(Path.Combine(folder, DataFile)).Dispose();
        return session;
    }

    /// <summary>The session, or null when it does not exist or belongs to another user or contract.</summary>
    public ContractUploadSession? Find(string id, int contractId, int userId)
    {
        if (!SessionIdPattern().IsMatch(id)) return null;
        var metadata = Path.Combine(Root, id, MetadataFile);
        if (!File.Exists(metadata)) return null;
        var session = JsonSerializer.Deserialize<ContractUploadSession>(File.ReadAllText(metadata));
        return session is not null && session.ContractId == contractId && session.UserId == userId
            ? session
            : null;
    }

    public long ReceivedBytes(ContractUploadSession session)
    {
        var data = new FileInfo(DataPath(session));
        return data.Exists ? data.Length : 0;
    }

    /// <summary>
    /// Appends a chunk that starts at <paramref name="offset"/>. Bytes that arrive
    /// before the connection drops are kept, so the client continues from
    /// <see cref="ReceivedBytes"/>.
    /// </summary>
    public async Task<ChunkWriteResult> AppendAsync(
        ContractUploadSession session, long offset, long length, Stream body, CancellationToken ct)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(DataPath(session), FileMode.Open, FileAccess.Write, FileShare.None);
        }
        catch (IOException)
        {
            return new ChunkWriteResult(ChunkWriteStatus.Busy, ReceivedBytes(session));
        }
        await using (stream)
        {
            if (stream.Length != offset)
                return new ChunkWriteResult(ChunkWriteStatus.OffsetMismatch, stream.Length);
            if (length > ChunkSize || offset + length > session.Size)
                return new ChunkWriteResult(ChunkWriteStatus.TooLarge, stream.Length);
            stream.Seek(offset, SeekOrigin.Begin);
            try
            {
                await body.CopyToAsync(stream, ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // The connection dropped: keep what arrived and let the client continue.
            }
            // A client that sends more than it announced cannot grow the file past the declared size.
            if (stream.Length > session.Size) stream.SetLength(session.Size);
            return new ChunkWriteResult(ChunkWriteStatus.Written, stream.Length);
        }
    }

    /// <summary>Moves the completed file to <paramref name="destination"/> and removes the session.</summary>
    public void MoveCompletedFile(ContractUploadSession session, string destination)
    {
        File.Move(DataPath(session), destination);
        Delete(session);
    }

    public void Delete(ContractUploadSession session)
    {
        var folder = Path.Combine(Root, session.Id);
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    /// <summary>Removes sessions not written to since <paramref name="cutoffUtc"/>; returns how many.</summary>
    public int DeleteExpired(DateTime cutoffUtc)
    {
        if (!Directory.Exists(Root)) return 0;
        var removed = 0;
        foreach (var folder in Directory.EnumerateDirectories(Root))
        {
            var lastWrite = Directory.EnumerateFiles(folder)
                .Select(File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder))
                .Max();
            if (lastWrite >= cutoffUtc) continue;
            try
            {
                Directory.Delete(folder, recursive: true);
                removed++;
            }
            catch (IOException)
            {
                // Still being written; the next run removes it.
            }
        }
        return removed;
    }

    private string DataPath(ContractUploadSession session) => Path.Combine(Root, session.Id, DataFile);
}

/// <summary>Removes contract uploads nobody finished within <see cref="ContractUploadSessionService.Lifetime"/>.</summary>
public sealed class ContractUploadSessionCleanupWorker(
    ContractUploadSessionService sessions,
    ILogger<ContractUploadSessionCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = sessions.DeleteExpired(DateTime.UtcNow - ContractUploadSessionService.Lifetime);
                if (count > 0) logger.LogInformation("Removed {Count} unfinished contract uploads", count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Contract upload cleanup failed; unfinished uploads stay on disk until the next run");
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
