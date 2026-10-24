using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

public sealed class WorkPackageApprovalEmail
{
    public int Id { get; set; }
    public int WorkPackageId { get; set; }
    public DepartmentWorkPackage WorkPackage { get; set; } = null!;
    [MaxLength(30)] public string RecordType { get; set; } = "";
    public int RecordId { get; set; }
    public int AuthorUserId { get; set; }
    public int RecipientUserId { get; set; }
    [MaxLength(254)] public string ToEmail { get; set; } = "";
    [MaxLength(254)] public string LeadEmail { get; set; } = "";
    [MaxLength(20000)] public string CcEmailsJson { get; set; } = "[]";
    [MaxLength(200)] public string Subject { get; set; } = "";
    [MaxLength(16000)] public string Body { get; set; } = "";
    [MaxLength(64)] public string PreviewHash { get; set; } = "";
    [MaxLength(30)] public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAt { get; set; }
    public DateTime? SentAt { get; set; }
}
