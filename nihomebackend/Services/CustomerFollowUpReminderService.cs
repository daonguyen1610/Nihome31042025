using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Reminds whoever logged a customer-care activity to call back or visit on its follow-up date
/// ("nhắc gọi lại, nhắc thăm"). Customers that stopped trading and inactive staff are skipped.
/// </summary>
public sealed class CustomerFollowUpReminderService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<CustomerFollowUpReminderService> logger)
{
    public const string TemplateCode = "crm.customer.follow-up-due";
    private const int BatchSize = 100;
    private const int SummaryLength = 120;

    public async Task NotifyDueAsync(DateOnly today, CancellationToken ct)
    {
        var dueActivities = await db.CustomerActivities
            .Include(activity => activity.Customer)
            .Where(activity => activity.FollowUpDate != null && activity.FollowUpDate <= today &&
                activity.FollowUpNotifiedAt == null &&
                activity.CreatedBy.IsActive &&
                activity.Customer.RelationshipStatus != CustomerRelationshipStatus.Suspended)
            .OrderBy(activity => activity.FollowUpDate)
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var activity in dueActivities)
        {
            activity.FollowUpNotifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            try
            {
                await notifications.NotifyFromTemplateAsync(
                    activity.CreatedByUserId,
                    TemplateCode,
                    new Dictionary<string, string>
                    {
                        ["customerName"] = activity.Customer.Name,
                        ["dueDate"] = activity.FollowUpDate!.Value.ToString("dd/MM/yyyy"),
                        ["summary"] = activity.Content.Length <= SummaryLength
                            ? activity.Content
                            : activity.Content[..SummaryLength] + "…",
                    },
                    refEntityType: EntityTypes.Customer,
                    refEntityId: activity.CustomerId,
                    linkUrl: $"/admin/customers?open={activity.CustomerId}");
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Customer activity {ActivityId} follow-up was recorded but the notification failed.", activity.Id);
            }
        }
    }
}

/// <summary>Runs the CRM due-date reminders at startup and then every 15 minutes.</summary>
public sealed class CrmReminderWorker(IServiceScopeFactory scopes, ILogger<CrmReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var today = DisplaySettings.VietnamToday();
                await scope.ServiceProvider.GetRequiredService<LeadResponseReminderService>()
                    .NotifyDueAsync(today, stoppingToken);
                await scope.ServiceProvider.GetRequiredService<CustomerFollowUpReminderService>()
                    .NotifyDueAsync(today, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "CRM reminders failed; retrying on the next interval."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
