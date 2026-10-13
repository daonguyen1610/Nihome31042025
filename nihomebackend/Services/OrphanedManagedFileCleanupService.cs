namespace NihomeBackend.Services;

public sealed record ManagedFileCleanupResult(
    int TotalFiles,
    int ReferencedFiles,
    int RecentFiles,
    int QuarantinedFiles,
    int RestoredFiles,
    int PurgedFiles,
    int FailedFiles);

public interface IOrphanedManagedFileCleanupService
{
    Task<ManagedFileCleanupResult> CleanupAsync(DateTime utcNow, CancellationToken ct = default);
}

public sealed class OrphanedManagedFileCleanupService(
    IManagedFileReferenceProvider referenceProvider,
    IWebHostEnvironment environment,
    ILogger<OrphanedManagedFileCleanupService> logger) : IOrphanedManagedFileCleanupService
{
    private static readonly TimeSpan MinimumFileAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan QuarantineRetention = TimeSpan.FromHours(24);

    private static readonly string[] ManagedRoots =
    [
        "/images/upload",
        "/images/processes",
        "/files",
    ];

    private const string HardDeleteQuarantinePrefix = "/files/.hard-delete-quarantine/";
    private const string QuarantineDirectoryName = ".managed-file-quarantine";

    public async Task<ManagedFileCleanupResult> CleanupAsync(
        DateTime utcNow,
        CancellationToken ct = default)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Cleanup time must be expressed in UTC.", nameof(utcNow));

        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        var webRoot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot"));
        var quarantineRoot = Path.GetFullPath(Path.Combine(contentRoot, QuarantineDirectoryName));
        EnsureContained(quarantineRoot, contentRoot, "Managed file quarantine escapes the content root.");
        EnsureNoReparsePoints(webRoot, contentRoot);
        EnsureNoReparsePoints(quarantineRoot, contentRoot);

        var totalFiles = 0;
        var referencedFiles = 0;
        var recentFiles = 0;
        var quarantinedFiles = 0;
        var restoredFiles = 0;
        var purgedFiles = 0;
        var failedFiles = 0;
        var fileAgeCutoff = utcNow - MinimumFileAge;
        var quarantineCutoff = utcNow - QuarantineRetention;

        var referencedPaths = await referenceProvider.GetAsync(ct);
        var quarantineFiles = EnumerateFilesSafely(quarantineRoot, contentRoot).ToList();

        foreach (var quarantinedPath in quarantineFiles)
        {
            ct.ThrowIfCancellationRequested();
            var managedPath = ToManagedPath(quarantineRoot, quarantinedPath);
            if (referencedPaths.Contains(managedPath)
                && TryRestore(quarantinedPath, managedPath, webRoot, contentRoot))
            {
                restoredFiles++;
            }
        }

        var purgeCandidates = EnumerateFilesSafely(quarantineRoot, contentRoot)
            .Where(path => File.GetLastWriteTimeUtc(path) < quarantineCutoff)
            .ToList();

        // Re-read active references immediately before the irreversible purge.
        referencedPaths = await referenceProvider.GetAsync(ct);
        foreach (var quarantinedPath in purgeCandidates)
        {
            ct.ThrowIfCancellationRequested();
            var managedPath = ToManagedPath(quarantineRoot, quarantinedPath);
            if (referencedPaths.Contains(managedPath))
            {
                if (TryRestore(quarantinedPath, managedPath, webRoot, contentRoot))
                    restoredFiles++;
                continue;
            }

            try
            {
                EnsureNoReparsePoints(quarantinedPath, contentRoot);
                File.Delete(quarantinedPath);
                purgedFiles++;
                logger.LogInformation("Purged unreferenced managed file {ManagedPath} after quarantine retention", managedPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedFiles++;
                logger.LogWarning(ex, "Unable to purge quarantined managed file {ManagedPath}", managedPath);
            }
        }

        var quarantineCandidates = new List<(string PhysicalPath, string ManagedPath)>();
        foreach (var managedRoot in ManagedRoots)
        {
            var physicalRoot = ResolveUnderRoot(webRoot, managedRoot);
            EnsureNoReparsePoints(physicalRoot, contentRoot);

            foreach (var filePath in EnumerateFilesSafely(physicalRoot, contentRoot))
            {
                ct.ThrowIfCancellationRequested();
                var managedPath = ToManagedPath(webRoot, filePath);
                if (managedPath.StartsWith(HardDeleteQuarantinePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                totalFiles++;
                if (referencedPaths.Contains(managedPath))
                {
                    referencedFiles++;
                    continue;
                }

                if (File.GetLastWriteTimeUtc(filePath) >= fileAgeCutoff)
                {
                    recentFiles++;
                    continue;
                }

                quarantineCandidates.Add((filePath, managedPath));
            }
        }

        // Close the scan-to-mutation window as far as practical. A final scan
        // after moving restores files linked during the remaining narrow race.
        referencedPaths = await referenceProvider.GetAsync(ct);
        var movedFiles = new List<(string QuarantinePath, string ManagedPath)>();
        foreach (var candidate in quarantineCandidates)
        {
            ct.ThrowIfCancellationRequested();
            if (referencedPaths.Contains(candidate.ManagedPath))
            {
                referencedFiles++;
                continue;
            }

            var quarantinePath = ResolveUnderRoot(quarantineRoot, candidate.ManagedPath);
            try
            {
                EnsureNoReparsePoints(candidate.PhysicalPath, contentRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(quarantinePath)!);
                EnsureNoReparsePoints(Path.GetDirectoryName(quarantinePath)!, contentRoot);
                File.Move(candidate.PhysicalPath, quarantinePath, overwrite: false);
                File.SetLastWriteTimeUtc(quarantinePath, utcNow);
                movedFiles.Add((quarantinePath, candidate.ManagedPath));
                quarantinedFiles++;
                logger.LogInformation("Quarantined unreferenced managed file {ManagedPath}", candidate.ManagedPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedFiles++;
                logger.LogWarning(ex, "Unable to quarantine unreferenced managed file {ManagedPath}", candidate.ManagedPath);
            }
        }

        try
        {
            referencedPaths = await referenceProvider.GetAsync(ct);
        }
        catch
        {
            RollBackMovedFiles(movedFiles, webRoot, contentRoot);
            throw;
        }

        foreach (var movedFile in movedFiles)
        {
            if (!referencedPaths.Contains(movedFile.ManagedPath)) continue;
            if (TryRestore(movedFile.QuarantinePath, movedFile.ManagedPath, webRoot, contentRoot))
            {
                restoredFiles++;
                quarantinedFiles--;
            }
        }

        return new ManagedFileCleanupResult(
            totalFiles,
            referencedFiles,
            recentFiles,
            quarantinedFiles,
            restoredFiles,
            purgedFiles,
            failedFiles);
    }

    private bool TryRestore(
        string quarantinePath,
        string managedPath,
        string webRoot,
        string contentRoot)
    {
        var originalPath = ResolveUnderRoot(webRoot, managedPath);
        if (File.Exists(originalPath)) return false;

        try
        {
            EnsureNoReparsePoints(quarantinePath, contentRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            EnsureNoReparsePoints(Path.GetDirectoryName(originalPath)!, contentRoot);
            File.Move(quarantinePath, originalPath, overwrite: false);
            logger.LogInformation("Restored referenced managed file {ManagedPath} from quarantine", managedPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Unable to restore referenced managed file {ManagedPath} from quarantine", managedPath);
            return false;
        }
    }

    private void RollBackMovedFiles(
        IEnumerable<(string QuarantinePath, string ManagedPath)> movedFiles,
        string webRoot,
        string contentRoot)
    {
        foreach (var movedFile in movedFiles)
        {
            if (!File.Exists(movedFile.QuarantinePath)) continue;
            if (TryRestore(movedFile.QuarantinePath, movedFile.ManagedPath, webRoot, contentRoot)) continue;

            logger.LogCritical(
                "Failed to roll back managed file {ManagedPath} after reference validation failed",
                movedFile.ManagedPath);
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, string contentRoot)
    {
        if (!Directory.Exists(root)) return [];
        EnsureNoReparsePoints(root, contentRoot);
        return Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        });
    }

    private static string ToManagedPath(string root, string filePath) =>
        "/" + Path.GetRelativePath(root, filePath).Replace('\\', '/');

    private static string ResolveUnderRoot(string root, string managedPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            managedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        EnsureContained(fullPath, root, "Managed cleanup path escapes its root.");
        return fullPath;
    }

    private static void EnsureContained(string path, string root, string message)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.Equals(normalizedRoot, comparison)
            && !normalizedPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void EnsureNoReparsePoints(string path, string contentRoot)
    {
        EnsureContained(path, contentRoot, "Managed cleanup path escapes the content root.");
        var fullPath = Path.GetFullPath(path);
        FileSystemInfo? current = Directory.Exists(fullPath)
            ? new DirectoryInfo(fullPath)
            : new FileInfo(fullPath);
        var boundary = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        while (current is not null)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidOperationException($"Managed cleanup path contains a reparse point: {current.FullName}");

            if (current.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(boundary, comparison))
                return;

            current = current switch
            {
                FileInfo file => file.Directory,
                DirectoryInfo directory => directory.Parent,
                _ => null,
            };
        }

        throw new InvalidOperationException("Managed cleanup path is not under the content root.");
    }
}
