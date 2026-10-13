namespace NihomeBackend.Services;

public sealed class OrphanedManagedFileCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<OrphanedManagedFileCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WebApplicationFactory uses the repository's physical content root.
        // Destructive reconciliation is covered through the scoped service
        // with an isolated temporary root instead of running against source
        // assets during unrelated integration tests.
        if (environment.IsEnvironment("IntegrationTests"))
        {
            logger.LogInformation("Orphan managed file cleanup is disabled in IntegrationTests");
            return;
        }

        logger.LogInformation(
            "Orphan managed file cleanup started (interval: {IntervalHours}h)",
            RunInterval.TotalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<IOrphanedManagedFileCleanupService>();
                var result = await cleanup.CleanupAsync(DateTime.UtcNow, stoppingToken);
                logger.LogInformation(
                    "Orphan managed file cleanup completed. totalFiles={TotalFiles}, referenced={ReferencedFiles}, recent={RecentFiles}, quarantined={QuarantinedFiles}, restored={RestoredFiles}, purged={PurgedFiles}, failed={FailedFiles}",
                    result.TotalFiles,
                    result.ReferencedFiles,
                    result.RecentFiles,
                    result.QuarantinedFiles,
                    result.RestoredFiles,
                    result.PurgedFiles,
                    result.FailedFiles);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Orphan managed file cleanup job failed");
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

        logger.LogInformation("Orphan managed file cleanup stopped");
    }
}
