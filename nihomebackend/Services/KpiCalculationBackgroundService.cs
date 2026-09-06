using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

public sealed class KpiCalculationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<KpiCalculationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("IntegrationTests")) return;
        await CalculateCurrentPeriodAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(DateTimeOffset.UtcNow);
            logger.LogInformation("Next KPI calculation scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);
            await CalculateCurrentPeriodAsync(stoppingToken);
        }
    }

    private async Task CalculateCurrentPeriodAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<IKpiService>();
            var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTimeOffset.UtcNow,
                OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
            var users = await db.Users.AsNoTracking()
                .Where(user => user.IsActive)
                .Select(user => new
                {
                    user.Id,
                    RoleCode = user.RoleEntity != null ? user.RoleEntity.Code : user.Role.ToString(),
                })
                .ToListAsync(ct);
            foreach (var user in users.Where(user => HasKpiPosition(user.RoleCode)))
            {
                try
                {
                    await service.CalculateAsync(now.Year, now.Month, user.Id, user.Id, ct);
                }
                catch (KpiOperationException exception)
                {
                    logger.LogDebug(exception, "KPI calculation skipped for user {UserId}", user.Id);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "KPI calculation failed for user {UserId}", user.Id);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Daily KPI calculation failed");
        }
    }

    internal static TimeSpan DelayUntilNextRun(DateTimeOffset utcNow)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        var todayRun = localNow.Date.AddHours(1);
        var nextLocal = localNow.DateTime < todayRun ? todayRun : todayRun.AddDays(1);
        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, timeZone);
        return nextUtc - utcNow.UtcDateTime;
    }

    private static bool HasKpiPosition(string roleCode) => roleCode is
        "SALE" or "SALES_MANAGER" or
        "DESIGN" or "DESIGN_LEAD" or "ARCHITECT" or "MEP_ENGINEER" or "STRUCT_ENGINEER" or
        "PM" or "QS" or "ACCOUNTANT";
}
