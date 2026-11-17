using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>
/// Sends CRM due-date reminders once per (kind, record, due date): lead response deadlines
/// ("deadline phản hồi") and customer follow-ups ("nhắc gọi lại, nhắc thăm"). Sent reminders are
/// recorded in <see cref="CrmDueReminder"/> rather than on the record, so a reminder never changes
/// the record's concurrency token, and a new due date is reminded again.
/// </summary>
public sealed class CrmDueReminderService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<CrmDueReminderService> logger)
{
    public const string LeadTemplateCode = "crm.lead.response-due";
    public const string CustomerTemplateCode = "crm.customer.follow-up-due";
    private const int BatchSize = 100;
    private const int SummaryLength = 120;

    private sealed record DueItem(
        int EntityId,
        DateOnly DueDate,
        int RecipientUserId,
        string TemplateCode,
        Dictionary<string, string> Data,
        string RefEntityType,
        int RefEntityId,
        string LinkUrl);

    public async Task NotifyLeadResponsesAsync(DateOnly today, CancellationToken ct)
    {
        var leads = await db.Leads.AsNoTracking()
            .Where(lead => lead.ResponseDueDate != null && lead.ResponseDueDate <= today &&
                lead.OwnerUserId != null && lead.Owner!.IsActive &&
                LeadResponseDeadline.OpenStatuses.Contains(lead.Status) &&
                !db.CrmDueReminders.Any(sent => sent.Kind == CrmDueReminderKinds.LeadResponse &&
                    sent.EntityId == lead.Id && sent.DueDate == lead.ResponseDueDate))
            .OrderBy(lead => lead.ResponseDueDate)
            .Take(BatchSize)
            .Select(lead => new { lead.Id, lead.Name, lead.CompanyName, DueDate = lead.ResponseDueDate!.Value, Owner = lead.OwnerUserId!.Value })
            .ToListAsync(ct);
        await SendOnceAsync(CrmDueReminderKinds.LeadResponse, leads.Select(lead => new DueItem(
            lead.Id, lead.DueDate, lead.Owner, LeadTemplateCode,
            new Dictionary<string, string>
            {
                ["leadName"] = string.IsNullOrWhiteSpace(lead.CompanyName) ? lead.Name : $"{lead.Name} ({lead.CompanyName})",
                ["dueDate"] = lead.DueDate.ToString("dd/MM/yyyy"),
            },
            EntityTypes.Lead, lead.Id, $"/admin/leads/{lead.Id}")), ct);
    }

    /// <summary>The customer's current owner is reminded (they own the relationship); the author only when there is no active owner.</summary>
    public async Task NotifyCustomerFollowUpsAsync(DateOnly today, CancellationToken ct)
    {
        var activities = await db.CustomerActivities.AsNoTracking()
            .Where(activity => activity.FollowUpDate != null && activity.FollowUpDate <= today &&
                activity.Customer.RelationshipStatus != CustomerRelationshipStatus.Suspended &&
                !db.CrmDueReminders.Any(sent => sent.Kind == CrmDueReminderKinds.CustomerFollowUp &&
                    sent.EntityId == activity.Id && sent.DueDate == activity.FollowUpDate))
            .OrderBy(activity => activity.FollowUpDate)
            .Take(BatchSize)
            .Select(activity => new
            {
                activity.Id,
                activity.CustomerId,
                CustomerName = activity.Customer.Name,
                activity.Content,
                DueDate = activity.FollowUpDate!.Value,
                Recipient = activity.Customer.Owner != null && activity.Customer.Owner.IsActive
                    ? (int?)activity.Customer.OwnerUserId
                    : activity.CreatedBy.IsActive ? activity.CreatedByUserId : null,
            })
            .ToListAsync(ct);
        await SendOnceAsync(CrmDueReminderKinds.CustomerFollowUp, activities
            .Where(activity => activity.Recipient.HasValue)
            .Select(activity => new DueItem(
                activity.Id, activity.DueDate, activity.Recipient!.Value, CustomerTemplateCode,
                new Dictionary<string, string>
                {
                    ["customerName"] = activity.CustomerName,
                    ["dueDate"] = activity.DueDate.ToString("dd/MM/yyyy"),
                    ["summary"] = activity.Content.Length <= SummaryLength
                        ? activity.Content
                        : activity.Content[..SummaryLength] + "…",
                },
                EntityTypes.Customer, activity.CustomerId, $"/admin/customers?open={activity.CustomerId}")), ct);
    }

    private async Task SendOnceAsync(string kind, IEnumerable<DueItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var record = new CrmDueReminder { Kind = kind, EntityId = item.EntityId, DueDate = item.DueDate };
            db.CrmDueReminders.Add(record);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Another run already claimed this reminder (unique key) or the record vanished.
                db.Entry(record).State = EntityState.Detached;
                continue;
            }
            try
            {
                await notifications.NotifyFromTemplateAsync(item.RecipientUserId, item.TemplateCode, item.Data,
                    refEntityType: item.RefEntityType, refEntityId: item.RefEntityId, linkUrl: item.LinkUrl);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "CRM reminder {Kind} for record {EntityId} was recorded but the notification failed.", kind, item.EntityId);
            }
        }
    }
}

/// <summary>Runs CRM due-date reminders at startup and then every 15 minutes; each kind fails independently.</summary>
public sealed class CrmReminderWorker(IServiceScopeFactory scopes, ILogger<CrmReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            await RunAsync("lead responses", (service, today) => service.NotifyLeadResponsesAsync(today, stoppingToken), stoppingToken);
            await RunAsync("customer follow-ups", (service, today) => service.NotifyCustomerFollowUpsAsync(today, stoppingToken), stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(string name, Func<CrmDueReminderService, DateOnly, Task> send, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await send(scope.ServiceProvider.GetRequiredService<CrmDueReminderService>(), await DisplaySettings.TodayAsync(db, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error) { logger.LogError(error, "CRM {Reminder} reminders failed; retrying on the next interval.", name); }
    }
}
