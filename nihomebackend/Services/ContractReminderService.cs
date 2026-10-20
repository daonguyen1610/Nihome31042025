using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ContractReminderService(AppDbContext db, IDataProtectionProvider protection,
    IEmailService email, IPermissionService permissions, ILogger<ContractReminderService> logger)
{
    private readonly IDataProtector protector = protection.CreateProtector("ContractReminder.Preview.v1");
    private sealed record Preview(int ContractId, string Kind, DateTime Date, int ContactId,
        string ContentHash, DateTime ExpiresAt, int UserId);

    public static DateTime Today => DateTime.UtcNow.AddHours(7).Date;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Context(Contract contract, CustomerContact contact) => Hash(JsonSerializer.Serialize(new
    {
        contract.CustomerId,
        contract.Customer.Name,
        contract.ContractNumber,
        EndDate = contract.EndDate?.Date,
        contact.Id,
        Email = contact.Email?.Trim(),
    }));

    private async Task<Contract?> LoadAsync(int id, int userId, bool canSeeAll, CancellationToken ct) =>
        await db.Contracts.AsNoTracking().Include(item => item.Customer).ThenInclude(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == id && (canSeeAll || item.OwnerUserId == userId), ct);

    private static void EnsureEligible(Contract contract)
    {
        if (contract.Direction != ContractDirection.Upstream || contract.EndDate is null ||
            contract.Status is ContractStatus.Draft or ContractStatus.Cancelled or ContractStatus.Completed)
            throw new ArgumentException("An active Upstream contract with an end date is required.");
    }

    public async Task<ContractReminderPreviewResponse?> PreviewAsync(int id, ContractReminderPreviewRequest request,
        int userId, bool canSeeAll, CancellationToken ct)
    {
        var contract = await LoadAsync(id, userId, canSeeAll, ct);
        if (contract is null) return null;
        EnsureEligible(contract);
        if (request.Kind is not ("Renewal" or "End") || request.ReminderDate is null ||
            request.ReminderDate.Value.ToDateTime(TimeOnly.MinValue) < Today)
            throw new ArgumentException("Choose Renewal or End and a reminder date from today (Vietnam time).");
        var contacts = contract.Customer.Contacts.Where(item => item.Email?.Trim().Length <= 254 && ContactValidation.IsValidEmail(item.Email))
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Id).ToList();
        var contact = request.ContactId.HasValue ? contacts.FirstOrDefault(item => item.Id == request.ContactId) : contacts.FirstOrDefault();
        if (contact is null) throw new ArgumentException("Add a valid email to the customer's contact before scheduling.");
        var templates = ContractReminderTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var rendered = ContractReminderTemplates.Render(templates, request.Kind, contract);
        var contentHash = Hash(JsonSerializer.Serialize(new[] { Context(contract, contact), rendered.Subject, rendered.Body }));
        var token = protector.Protect(JsonSerializer.Serialize(new Preview(id, request.Kind,
            request.ReminderDate.Value.ToDateTime(TimeOnly.MinValue), contact.Id, contentHash,
            DateTime.UtcNow.AddMinutes(15), userId)));
        return new(token, contact.Id, contact.Email!.Trim(), rendered.Subject, rendered.Body,
            request.Kind, request.ReminderDate.Value.ToDateTime(TimeOnly.MinValue),
            contacts.Select(item => new ContractReminderContact(item.Id, item.FullName, item.Email!.Trim())).ToList());
    }

    public async Task<ContractReminderResponse?> ScheduleAsync(int id, string token, int userId, bool canSeeAll, CancellationToken ct)
    {
        Preview preview;
        try { preview = JsonSerializer.Deserialize<Preview>(protector.Unprotect(token))!; }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        { throw new ArgumentException("Invalid preview. Preview the email again."); }
        if (preview is null || preview.ContractId != id || preview.UserId != userId)
            throw new ArgumentException("Invalid preview. Preview the email again.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var contract = await LoadAsync(id, userId, canSeeAll, ct);
        if (contract is null) return null;
        var previewHash = Hash(token);
        var existing = await db.ContractReminders.SingleOrDefaultAsync(item => item.PreviewHash == previewHash, ct);
        if (existing is not null) return ToResponse(existing);
        EnsureEligible(contract);
        if (preview.ExpiresAt < DateTime.UtcNow || preview.Date < Today)
            throw new ArgumentException("Preview expired. Preview the email again.");
        var contact = contract.Customer.Contacts.SingleOrDefault(item => item.Id == preview.ContactId);
        if (contact is null || contact.Email?.Trim().Length > 254 || !ContactValidation.IsValidEmail(contact.Email))
            throw new ArgumentException("Customer contact changed. Preview the email again.");
        var templates = ContractReminderTemplates.Get(await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct));
        var rendered = ContractReminderTemplates.Render(templates, preview.Kind, contract);
        if (preview.ContentHash != Hash(JsonSerializer.Serialize(new[] { Context(contract, contact), rendered.Subject, rendered.Body })))
            throw new ArgumentException("Contract, contact or template changed. Preview the email again.");
        if (await db.ContractReminders.AnyAsync(item => item.ContractId == id && item.Kind == preview.Kind &&
            (item.Status == "Scheduled" || item.Status == "Sending"), ct))
            throw new ArgumentException("Cancel the existing pending reminder of this type before scheduling another.");
        var reminder = new ContractReminder
        {
            ContractId = id,
            Kind = preview.Kind,
            ReminderDate = preview.Date,
            ContactId = contact.Id,
            ToEmail = contact.Email!.Trim(),
            Subject = rendered.Subject,
            Body = rendered.Body,
            ContextHash = Context(contract, contact),
            PreviewHash = previewHash,
            CreatedByUserId = userId,
        };
        db.ContractReminders.Add(reminder);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return ToResponse(reminder);
    }

    public async Task<List<ContractReminderResponse>> ListAsync(int id, CancellationToken ct) =>
        (await db.ContractReminders.AsNoTracking().Where(item => item.ContractId == id)
            .OrderByDescending(item => item.Id).Take(100).ToListAsync(ct)).Select(ToResponse).ToList();

    public async Task<bool> CancelAsync(int id, int reminderId, int userId, bool canSeeAll, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await LoadAsync(id, userId, canSeeAll, ct) is null) return false;
        var reminder = await db.ContractReminders.SingleOrDefaultAsync(item => item.Id == reminderId && item.ContractId == id, ct);
        if (reminder is null) return false;
        if (reminder.Status != "Scheduled") throw new ArgumentException("Only scheduled reminders can be cancelled.");
        reminder.Status = "Cancelled";
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return true;
    }

    public async Task SendDueAsync(CancellationToken ct = default)
    {
        var ids = await db.ContractReminders.AsNoTracking().Where(item => item.Status == "Scheduled" && item.ReminderDate <= Today)
            .OrderBy(item => item.ReminderDate).ThenBy(item => item.Id).Select(item => item.Id).Take(100).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            await SendOneAsync(id, ct);
        }
    }

    private async Task SendOneAsync(int id, CancellationToken ct)
    {
        ContractReminder reminder;
        // Commit the claim BEFORE SMTP. Sending is deliberately never retried automatically:
        // an SMTP disconnect or process crash can leave delivery outcome unknown.
        await using (var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null)
        {
            reminder = await db.ContractReminders.SingleAsync(item => item.Id == id, ct);
            await db.Entry(reminder).ReloadAsync(ct);
            if (reminder.Status != "Scheduled" || reminder.ReminderDate > Today) return;
            var contract = await LoadAsync(reminder.ContractId, 0, true, ct);
            var contact = contract?.Customer.Contacts.SingleOrDefault(item => item.Id == reminder.ContactId);
            var activeCreator = await db.Users.AsNoTracking().AnyAsync(item => item.Id == reminder.CreatedByUserId && item.IsActive, ct);
            var canManage = activeCreator && await permissions.HasAsync(reminder.CreatedByUserId, "crm.contracts.manage", ct);
            var hasScope = contract?.OwnerUserId == reminder.CreatedByUserId ||
                await permissions.HasAsync(reminder.CreatedByUserId, "crm.contracts.view.all", ct);
            var contractId = reminder.ContractId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var deleting = await db.HardDeleteOperations.AsNoTracking().AnyAsync(item =>
                item.ResourceType == EntityTypes.Contract && item.ResourceId == contractId &&
                item.Status != HardDeleteOperationStatus.Completed, ct);
            if (contract is null || contact is null || !canManage || !hasScope || deleting ||
                contract.Direction != ContractDirection.Upstream ||
                contract.Status is ContractStatus.Draft or ContractStatus.Cancelled or ContractStatus.Completed ||
                !ContactValidation.IsValidEmail(contact.Email) || Context(contract, contact) != reminder.ContextHash)
            {
                reminder.Status = "NeedsReview";
                reminder.ErrorCode = "CONTEXT_CHANGED";
            }
            else
            {
                reminder.Status = "Sending";
                reminder.AttemptedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        if (reminder.Status != "Sending") return;
        try
        {
            await email.SendEmailAsync(reminder.ToEmail, reminder.Subject, reminder.Body);
        }
        catch (Exception error)
        {
            reminder.Status = "NeedsReview";
            reminder.ErrorCode = "DELIVERY_UNCERTAIN";
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(error, "Contract reminder {ReminderId} delivery requires review; no automatic retry.", id);
            return;
        }
        reminder.Status = "Sent";
        reminder.SentAt = DateTime.UtcNow;
        db.ContractCareActivities.Add(new ContractCareActivity
        {
            ContractId = reminder.ContractId,
            Type = CustomerActivityType.Email,
            Content = $"{reminder.Subject}\n{reminder.ToEmail}",
            OccurredAt = reminder.SentAt.Value,
            CreatedAt = reminder.SentAt.Value,
            CreatedByUserId = reminder.CreatedByUserId,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static ContractReminderResponse ToResponse(ContractReminder item) => new(item.Id, item.Kind,
        item.ReminderDate, item.ToEmail, item.Subject, item.Body, item.Status, item.ErrorCode,
        item.CreatedAt, item.AttemptedAt, item.SentAt);
}
