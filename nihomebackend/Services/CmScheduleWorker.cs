using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>Technical polling interval; business thresholds come from SiteSettings.</summary>
public sealed class CmScheduleWorker(IServiceScopeFactory scopes, ILogger<CmScheduleWorker> logger,
    IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("IntegrationTests")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var projects = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.Status == "Published")
                    .Select(x => x.OperationalProjectId).Distinct().ToListAsync(stoppingToken);
                foreach (var projectId in projects)
                {
                    try
                    {
                        using var projectScope = scopes.CreateScope();
                        await projectScope.ServiceProvider.GetRequiredService<CmScheduleService>()
                            .CaptureAsync(projectId, null, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (CmScheduleException error)
                    {
                        logger.LogWarning("CM progress capture blocked for project {ProjectId}: {Code}. Review the CM workspace and correct project inputs", projectId, error.Code);
                    }
                    catch (Exception error)
                    {
                        logger.LogError(error, "CM progress capture failed for project {ProjectId}. Automatic retry on next poll", projectId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogError(error, "CM progress polling failed. Automatic retry on next poll");
            }
        }
    }
}
