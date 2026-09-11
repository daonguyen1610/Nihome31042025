namespace NihomeBackend.Services;

public sealed class ContractMilestoneDueWorker(
    IServiceScopeFactory scopes,
    ILogger<ContractMilestoneDueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ContractMilestoneNotificationService>()
                    .NotifyDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Contract milestone due notifications failed; retrying on the next interval.");
            }
        }
    }
}
