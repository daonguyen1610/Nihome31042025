using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>Rules for the lead response deadline ("deadline phản hồi" in NICON's CRM requirements).</summary>
public static class LeadResponseDeadline
{
    /// <summary>Statuses where sales still owes the lead a response.</summary>
    public static readonly LeadStatus[] OpenStatuses = [LeadStatus.New, LeadStatus.Contacted, LeadStatus.Interested];

    public static bool IsOverdue(LeadStatus status, DateOnly? dueDate, DateOnly today) =>
        dueDate.HasValue && dueDate.Value < today && OpenStatuses.Contains(status);

    public static DateOnly VietnamToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, DisplaySettings.VietnamTimeZone).DateTime);
}

/// <summary>Reminds a lead's owner once, on the response due date, while the lead is still open.</summary>
public sealed class LeadResponseReminderService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<LeadResponseReminderService> logger)
{
    public const string TemplateCode = "crm.lead.response-due";
    private const int BatchSize = 100;

    public async Task NotifyDueAsync(DateOnly today, CancellationToken ct)
    {
        var dueLeads = await db.Leads
            .Where(lead => lead.ResponseDueDate != null && lead.ResponseDueDate <= today &&
                lead.ResponseDueNotifiedAt == null && lead.OwnerUserId != null &&
                LeadResponseDeadline.OpenStatuses.Contains(lead.Status) &&
                lead.Owner!.IsActive)
            .OrderBy(lead => lead.ResponseDueDate)
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var lead in dueLeads)
        {
            lead.ResponseDueNotifiedAt = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone edited the lead meanwhile; the next run re-evaluates it.
                db.Entry(lead).State = EntityState.Detached;
                continue;
            }
            try
            {
                await notifications.NotifyFromTemplateAsync(
                    lead.OwnerUserId!.Value,
                    TemplateCode,
                    new Dictionary<string, string>
                    {
                        ["leadName"] = string.IsNullOrWhiteSpace(lead.CompanyName)
                            ? lead.Name
                            : $"{lead.Name} ({lead.CompanyName})",
                        ["dueDate"] = lead.ResponseDueDate!.Value.ToString("dd/MM/yyyy"),
                    },
                    refEntityType: EntityTypes.Lead,
                    refEntityId: lead.Id,
                    linkUrl: $"/admin/leads/{lead.Id}");
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Lead {LeadId} response reminder was recorded but the notification failed.", lead.Id);
            }
        }
    }
}

public sealed class LeadResponseReminderWorker(
    IServiceScopeFactory scopes,
    ILogger<LeadResponseReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check once at startup so reminders due during downtime are not delayed a full interval.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<LeadResponseReminderService>()
                    .NotifyDueAsync(LeadResponseDeadline.VietnamToday(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogError(error, "Lead response reminders failed; retrying on the next interval."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
