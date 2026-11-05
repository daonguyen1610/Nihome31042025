using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpdateEmailTemplatesRequest
{
    public string? NewApplicationEmailSubjectTemplate { get; set; }
    public string? NewApplicationEmailBodyTemplate { get; set; }
    public string? NotificationEmail { get; set; }
    public string? OtpEmailSubjectTemplate { get; set; }
    public string? OtpEmailBodyTemplate { get; set; }

    [StringLength(200, MinimumLength = 1)]
    public string? QuoteEmailSubjectTemplate { get; set; }

    [StringLength(8000, MinimumLength = 1)]
    public string? QuoteEmailBodyTemplate { get; set; }

    [StringLength(200, MinimumLength = 1)]
    public string? ProjectScheduleEmailSubjectTemplate { get; set; }

    [StringLength(8000, MinimumLength = 1)]
    public string? ProjectScheduleEmailBodyTemplate { get; set; }
}

public sealed class PreviewProjectScheduleEmailRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(8000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}
