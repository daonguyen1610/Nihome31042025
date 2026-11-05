using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

/// <summary>
/// Durable outbox for project master schedule emails. Notifications are queued
/// in the same request that submits or publishes a revision and delivered by a
/// background worker, so a temporary SMTP failure is retried instead of being
/// lost with the request.
/// </summary>
public sealed class ProjectScheduleEmail
{
    public const string PendingStatus = "Pending";
    public const string SendingStatus = "Sending";
    public const string SentStatus = "Sent";
    public const string NeedsReviewStatus = "NeedsReview";

    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public int RevisionId { get; set; }
    public ProjectScheduleRevision Revision { get; set; } = null!;
    public int? RecipientUserId { get; set; }
    [MaxLength(254)] public string ToEmail { get; set; } = string.Empty;
    [MaxLength(200)] public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    [MaxLength(30)] public string Status { get; set; } = PendingStatus;
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    [MaxLength(1000)] public string? LastError { get; set; }
}
