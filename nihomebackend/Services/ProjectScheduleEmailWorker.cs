using Microsoft.Extensions.DependencyInjection;

namespace NihomeBackend.Services;

public sealed class ProjectScheduleEmailWorker(IServiceScopeFactory scopes,
    ILogger<ProjectScheduleEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProjectScheduleEmailService>()
                    .SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogError(error, "Project schedule email worker failed");
            }
        }
    }
}
