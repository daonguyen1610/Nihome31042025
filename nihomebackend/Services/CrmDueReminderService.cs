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
    public const string TenderTemplateCode = "crm.tender.deadline-due";
    public const string TenderChecklistTemplateCode = "crm.tender.checklist-due";
    public const string QuoteTemplateCode = "crm.quote.expiry-due";
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

    /// <summary>Reminds the preparer (else the invitation receiver) N days before each open tender's deadline.</summary>
    public async Task NotifyTenderDeadlinesAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DisplaySettings.TodayIn(zone));
        var horizon = DateTime.UtcNow.AddDays(ReminderDefaults.MaxDaysBefore + 1);
        var since = DateTime.UtcNow.AddDays(-1);
        var tenders = await db.Tenders.AsNoTracking()
            .Where(tender => tender.Status == TenderStatus.Preparing &&
                tender.SubmissionDeadline >= since && tender.SubmissionDeadline <= horizon)
            .Select(tender => new
            {
                tender.Id,
                tender.Code,
                tender.Name,
                tender.SubmissionDeadline,
                tender.DeadlineReminderDays,
                Recipient = tender.Preparer != null && tender.Preparer.IsActive
                    ? tender.PreparerUserId
                    : tender.InvitationReceivedByUser.IsActive ? tender.InvitationReceivedByUserId : (int?)null,
            })
            .ToListAsync(ct);
        var due = tenders
            .Select(tender => new { Tender = tender, DueDate = LocalDate(tender.SubmissionDeadline, zone) })
            .Where(item => item.Tender.Recipient.HasValue && IsInReminderWindow(item.DueDate, item.Tender.DeadlineReminderDays, today))
            .ToList();
        await SendOnceAsync(CrmDueReminderKinds.TenderDeadline, await NotSentAsync(CrmDueReminderKinds.TenderDeadline,
            due.Select(item => new DueItem(
                item.Tender.Id, item.DueDate, item.Tender.Recipient!.Value, TenderTemplateCode,
                new Dictionary<string, string>
                {
                    ["code"] = item.Tender.Code,
                    ["name"] = item.Tender.Name,
                    ["dueDate"] = item.DueDate.ToString("dd/MM/yyyy"),
                },
                EntityTypes.Tender, item.Tender.Id, $"/admin/tenders/{item.Tender.Id}")), ct), ct);
    }

    /// <summary>Reminds the item owner (else the tender preparer) on an unfinished item's internal deadline.</summary>
    public async Task NotifyTenderChecklistItemsAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DisplaySettings.TodayIn(zone));
        var until = DateTime.UtcNow.AddDays(1);
        var since = DateTime.UtcNow.AddDays(-7);
        var items = await db.TenderChecklistItems.AsNoTracking()
            .Where(item => item.InternalDeadline != null && item.InternalDeadline >= since && item.InternalDeadline <= until &&
                (item.Status == TenderChecklistItemStatus.NotStarted || item.Status == TenderChecklistItemStatus.Preparing) &&
                item.Tender.Status == TenderStatus.Preparing)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.TenderId,
                TenderCode = item.Tender.Code,
                Deadline = item.InternalDeadline!.Value,
                Recipient = item.Owner != null && item.Owner.IsActive
                    ? item.OwnerUserId
                    : item.Tender.Preparer != null && item.Tender.Preparer.IsActive ? item.Tender.PreparerUserId : null,
            })
            .ToListAsync(ct);
        var due = items
            .Select(item => new { Item = item, DueDate = LocalDate(item.Deadline, zone) })
            .Where(entry => entry.Item.Recipient.HasValue && entry.DueDate <= today && entry.DueDate >= today.AddDays(-7))
            .ToList();
        await SendOnceAsync(CrmDueReminderKinds.TenderChecklistItem, await NotSentAsync(CrmDueReminderKinds.TenderChecklistItem,
            due.Select(entry => new DueItem(
                entry.Item.Id, entry.DueDate, entry.Item.Recipient!.Value, TenderChecklistTemplateCode,
                new Dictionary<string, string>
                {
                    ["code"] = entry.Item.TenderCode,
                    ["item"] = entry.Item.Title,
                    ["dueDate"] = entry.DueDate.ToString("dd/MM/yyyy"),
                },
                EntityTypes.Tender, entry.Item.TenderId, $"/admin/tenders/{entry.Item.TenderId}")), ct), ct);
    }

    /// <summary>Reminds the owner N days before an approved or sent quote stops being valid.</summary>
    public async Task NotifyQuoteExpiriesAsync(TimeZoneInfo zone, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DisplaySettings.TodayIn(zone));
        var horizon = DateTime.UtcNow.AddDays(ReminderDefaults.MaxDaysBefore + 1);
        var quotes = await db.Quotes.AsNoTracking()
            .Where(quote => (quote.Status == QuoteStatus.Approved || quote.Status == QuoteStatus.SentToCustomer) &&
                quote.ValidUntil >= DateTime.UtcNow && quote.ValidUntil <= horizon &&
                quote.OwnerUserId != null && quote.Owner!.IsActive)
            .Select(quote => new { quote.Id, quote.Code, quote.ValidUntil, quote.ExpiryReminderDays, Owner = quote.OwnerUserId!.Value })
            .ToListAsync(ct);
        var due = quotes
            .Select(quote => new { Quote = quote, DueDate = LocalDate(quote.ValidUntil, zone) })
            .Where(item => IsInReminderWindow(item.DueDate, item.Quote.ExpiryReminderDays, today))
            .ToList();
        await SendOnceAsync(CrmDueReminderKinds.QuoteExpiry, await NotSentAsync(CrmDueReminderKinds.QuoteExpiry,
            due.Select(item => new DueItem(
                item.Quote.Id, item.DueDate, item.Quote.Owner, QuoteTemplateCode,
                new Dictionary<string, string>
                {
                    ["code"] = item.Quote.Code,
                    ["dueDate"] = item.DueDate.ToString("dd/MM/yyyy"),
                },
                EntityTypes.Quote, item.Quote.Id, $"/admin/quotes/{item.Quote.Id}")), ct), ct);
    }

    /// <summary>True from <paramref name="daysBefore"/> days ahead of the due date up to the due date itself.</summary>
    public static bool IsInReminderWindow(DateOnly dueDate, int daysBefore, DateOnly today) =>
        today >= dueDate.AddDays(-Math.Max(0, daysBefore)) && today <= dueDate;

    private static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    private async Task<List<DueItem>> NotSentAsync(string kind, IEnumerable<DueItem> items, CancellationToken ct)
    {
        var candidates = items.ToList();
        var ids = candidates.Select(item => item.EntityId).Distinct().ToList();
        var sent = await db.CrmDueReminders.AsNoTracking()
            .Where(item => item.Kind == kind && ids.Contains(item.EntityId))
            .Select(item => new { item.EntityId, item.DueDate })
            .ToListAsync(ct);
        return candidates
            .Where(item => !sent.Any(done => done.EntityId == item.EntityId && done.DueDate == item.DueDate))
            .Take(BatchSize)
            .ToList();
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
            await RunAsync("lead responses", (service, zone) =>
                service.NotifyLeadResponsesAsync(DateOnly.FromDateTime(DisplaySettings.TodayIn(zone)), stoppingToken), stoppingToken);
            await RunAsync("customer follow-ups", (service, zone) =>
                service.NotifyCustomerFollowUpsAsync(DateOnly.FromDateTime(DisplaySettings.TodayIn(zone)), stoppingToken), stoppingToken);
            await RunAsync("tender deadlines", (service, zone) => service.NotifyTenderDeadlinesAsync(zone, stoppingToken), stoppingToken);
            await RunAsync("tender checklist items", (service, zone) => service.NotifyTenderChecklistItemsAsync(zone, stoppingToken), stoppingToken);
            await RunAsync("quote expiries", (service, zone) => service.NotifyQuoteExpiriesAsync(zone, stoppingToken), stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(string name, Func<CrmDueReminderService, TimeZoneInfo, Task> send, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await send(scope.ServiceProvider.GetRequiredService<CrmDueReminderService>(), await DisplaySettings.ZoneAsync(db, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error) { logger.LogError(error, "CRM {Reminder} reminders failed; retrying on the next interval.", name); }
    }
}
