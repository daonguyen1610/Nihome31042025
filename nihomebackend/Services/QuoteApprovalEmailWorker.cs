namespace NihomeBackend.Services;

public sealed class QuoteApprovalEmailWorker(IServiceScopeFactory scopes,
    ILogger<QuoteApprovalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<QuoteApprovalEmailService>()
                    .SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Quote approval email worker failed"); }
        }
    }
}
