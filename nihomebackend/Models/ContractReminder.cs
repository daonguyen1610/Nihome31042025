namespace NihomeBackend.Models;

public sealed class ContractReminder
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string Kind { get; set; } = "Renewal";
    public DateTime ReminderDate { get; set; }
    public int ContactId { get; set; }
    public string ToEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string ContextHash { get; set; } = "";
    public string PreviewHash { get; set; } = "";
    public string Status { get; set; } = "Scheduled";
    public string? ErrorCode { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
