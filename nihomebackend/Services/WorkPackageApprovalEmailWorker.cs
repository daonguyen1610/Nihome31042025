namespace NihomeBackend.Services;

public sealed class WorkPackageApprovalEmailWorker(IServiceScopeFactory scopes,
    ILogger<WorkPackageApprovalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<WorkPackageApprovalEmailService>().SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Approval email worker failed"); }
        }
    }
}
