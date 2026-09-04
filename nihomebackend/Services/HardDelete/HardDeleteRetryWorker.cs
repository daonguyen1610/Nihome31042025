using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services.HardDelete;

public sealed class HardDeleteRetryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<HardDeleteRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueOperationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Hard-delete retry scan failed");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessDueOperationsAsync(CancellationToken ct)
    {
        using var lookupScope = scopeFactory.CreateScope();
        var lookupDb = lookupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var staleProcessingBefore = now.Subtract(TimeSpan.FromMinutes(15));
        var operationIds = await lookupDb.HardDeleteOperations.AsNoTracking()
            .Where(item => item.Status == HardDeleteOperationStatus.Ready ||
            item.Status == HardDeleteOperationStatus.Failed && item.NextAttemptAt <= now ||
            item.Status == HardDeleteOperationStatus.Processing && item.LastAttemptAt <= staleProcessingBefore)
            .OrderBy(item => item.NextAttemptAt ?? item.CreatedAt)
            .Select(item => item.Id)
            .Take(10)
            .ToListAsync(ct);

        foreach (var operationId in operationIds)
        {
            using var operationScope = scopeFactory.CreateScope();
            var service = operationScope.ServiceProvider.GetRequiredService<IHardDeleteOperationService>();
            try
            {
                await service.ProcessAsync(operationId, ct);
            }
            catch (HardDeleteOperationConflictException)
            {
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Hard-delete retry failed for operation {OperationId}", operationId);
            }
        }
    }
}