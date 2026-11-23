namespace NihomeBackend.Services;

public sealed class DisabledWorkTaskRetentionWorker(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<DisabledWorkTaskRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("IntegrationTests")) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDepartmentWorkPackageService>();
                var purged = await service.PurgeExpiredDisabledTasksAsync(DateTime.UtcNow, stoppingToken);
                if (purged > 0)
                    logger.LogInformation("Expired disabled work task trees purged. treeCount={TreeCount}", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Disabled work task retention purge failed; it will retry after {IntervalHours} hours.", RunInterval.TotalHours);
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
