using NihomeBackend.Data;

namespace NihomeBackend.Services;

public sealed record ManagedFileCleanupResult(
    int TotalFiles,
    int ReferencedFiles,
    int RecentFiles,
    int DeletedFiles,
    int FailedFiles);

public interface IOrphanedManagedFileCleanupService
{
    Task<ManagedFileCleanupResult> CleanupAsync(DateTime cutoffUtc, CancellationToken ct = default);
}

public sealed class OrphanedManagedFileCleanupService(
    AppDbContext db,
    IWebHostEnvironment environment,
    ILogger<OrphanedManagedFileCleanupService> logger) : IOrphanedManagedFileCleanupService
{
    private static readonly string[] ManagedRoots =
    [
        "/images/upload",
        "/images/processes",
        "/files",
    ];

    private const string HardDeleteQuarantinePrefix = "/files/.hard-delete-quarantine/";

    public async Task<ManagedFileCleanupResult> CleanupAsync(
        DateTime cutoffUtc,
        CancellationToken ct = default)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Cleanup cutoff must be expressed in UTC.", nameof(cutoffUtc));

        var referencedPaths = await ReferencedManagedFiles.GetAsync(db, ct);
        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var totalFiles = 0;
        var referencedFiles = 0;
        var recentFiles = 0;
        var deletedFiles = 0;
        var failedFiles = 0;

        foreach (var managedRoot in ManagedRoots)
        {
            var physicalRoot = ResolveUnderWebRoot(webRoot, managedRoot);
            if (!Directory.Exists(physicalRoot)) continue;

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = true,
            };

            foreach (var filePath in Directory.EnumerateFiles(physicalRoot, "*", enumerationOptions))
            {
                ct.ThrowIfCancellationRequested();

                var managedPath = "/" + Path.GetRelativePath(webRoot, filePath).Replace('\\', '/');
                if (managedPath.StartsWith(HardDeleteQuarantinePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                totalFiles++;

                if (referencedPaths.Contains(managedPath))
                {
                    referencedFiles++;
                    continue;
                }

                if (File.GetLastWriteTimeUtc(filePath) >= cutoffUtc)
                {
                    recentFiles++;
                    continue;
                }

                try
                {
                    File.Delete(filePath);
                    deletedFiles++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failedFiles++;
                    logger.LogWarning(ex, "Unable to delete orphan managed file {ManagedPath}", managedPath);
                }
            }
        }

        return new ManagedFileCleanupResult(
            totalFiles,
            referencedFiles,
            recentFiles,
            deletedFiles,
            failedFiles);
    }

    private static string ResolveUnderWebRoot(string webRoot, string managedPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(
            webRoot,
            managedPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = webRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Managed cleanup root escapes the web root.");
        return fullPath;
    }
}
