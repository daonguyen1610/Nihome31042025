namespace NihomeBackend.Models;

/// <summary>
/// Records that a CRM due-date reminder was sent, so each (kind, record, due date) is reminded once
/// without writing to the reminded record itself (which would change its concurrency token).
/// </summary>
public class CrmDueReminder
{
    public long Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public static class CrmDueReminderKinds
{
    public const string LeadResponse = "lead.response";
    public const string CustomerFollowUp = "customer.follow-up";
    public const string TenderDeadline = "tender.deadline";
    public const string TenderChecklistItem = "tender.checklist-item";
    public const string QuoteExpiry = "quote.expiry";
}
