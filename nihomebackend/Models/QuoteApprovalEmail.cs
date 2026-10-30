using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

public sealed class QuoteApprovalEmail
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public int ApproverUserId { get; set; }
    [MaxLength(254)] public string ToEmail { get; set; } = "";
    [MaxLength(20000)] public string CcEmailsJson { get; set; } = "[]";
    [MaxLength(200)] public string Subject { get; set; } = "";
    [MaxLength(16000)] public string Body { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
