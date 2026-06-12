namespace NihomeBackend.Models;

public class SiteSettings
{
    public int Id { get; set; }

    public string SiteName { get; set; } = "Nihome";

    public string? SiteDescription { get; set; }

    public string? PrimaryEmail { get; set; }

    public string? SecondaryEmail { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? SecondaryPhone { get; set; }

    public string? Address { get; set; }

    public string? MapEmbedUrl { get; set; }

    public bool EnableOtpForRegistration { get; set; } = true;

    public bool EnableOtpForForgotPassword { get; set; } = true;

    public string? OtpEmailSubjectTemplate { get; set; }

    public string? OtpEmailBodyTemplate { get; set; }

    public string? NewApplicationEmailSubjectTemplate { get; set; }

    public string? NewApplicationEmailBodyTemplate { get; set; }

    public string? NotificationEmail { get; set; }

    public int AuditLogRetentionMinutes { get; set; } = 43200;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
