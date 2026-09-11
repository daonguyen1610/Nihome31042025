using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class ContractMilestoneNotificationService(
    AppDbContext db,
    INotificationService notifications,
    ILogger<ContractMilestoneNotificationService> logger)
{
    private const string TemplateCode = "crm.contract.milestone-due";

    public async Task NotifyDueAsync(CancellationToken ct = default)
    {
        var today = GetVietnamToday();
        var ids = await db.ContractPaymentMilestones.AsNoTracking()
            .Where(item => item.DueNotificationSentAt == null && item.DueDate != null &&
                item.DueDate < today.AddDays(1) && item.Status != PaymentMilestoneStatus.Paid &&
                item.Contract.Direction == ContractDirection.Upstream &&
                item.Contract.Status != ContractStatus.Draft &&
                item.Contract.Status != ContractStatus.Cancelled &&
                item.Contract.Status != ContractStatus.Completed)
            .OrderBy(item => item.DueDate)
            .Select(item => item.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (var id in ids)
        {
            await NotifyOneAsync(id, today, ct);
        }
    }

    private async Task NotifyOneAsync(int id, DateTime today, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var milestone = await db.ContractPaymentMilestones
            .Include(item => item.Contract)
            .SingleOrDefaultAsync(item => item.Id == id, ct);
        if (milestone?.DueNotificationSentAt is not null || milestone?.DueDate is null ||
            milestone.Status == PaymentMilestoneStatus.Paid || milestone.DueDate.Value.Date > today ||
            milestone.Contract.Direction != ContractDirection.Upstream ||
            milestone.Contract.Status is ContractStatus.Draft or ContractStatus.Cancelled or ContractStatus.Completed)
        {
            return;
        }

        var recipientCandidates = new[]
        {
            milestone.Contract.OwnerUserId,
            milestone.ResponsibleAccountantUserId,
        }.Where(item => item.HasValue).Select(item => item!.Value).Distinct().ToList();
        var recipients = await db.Users.AsNoTracking()
            .Where(item => recipientCandidates.Contains(item.Id) && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync(ct);
        if (recipients.Count == 0)
        {
            logger.LogWarning("Due reminder for contract milestone {MilestoneId} has no active recipient.",
                milestone.Id);
            return;
        }

        var dueDate = milestone.DueDate.Value.ToString("dd/MM/yyyy");
        await notifications.NotifyManyFromTemplateAsync(
            recipients,
            TemplateCode,
            new Dictionary<string, string>
            {
                ["contractNumber"] = milestone.Contract.ContractNumber,
                ["milestoneName"] = milestone.Name,
                ["dueDate"] = dueDate,
            },
            nameof(ContractPaymentMilestone),
            milestone.Id,
            $"/admin/finance/contracts/{milestone.ContractId}?tab=schedule");

        milestone.DueNotificationSentAt = DateTime.UtcNow;
        milestone.UpdatedAt = milestone.DueNotificationSentAt.Value;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Sent due reminder for contract milestone {MilestoneId} to {RecipientCount} users.",
            milestone.Id, recipients.Count);
    }

    private static DateTime GetVietnamToday()
    {
        var timeZoneId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
        return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, timeZoneId).Date;
    }
}
