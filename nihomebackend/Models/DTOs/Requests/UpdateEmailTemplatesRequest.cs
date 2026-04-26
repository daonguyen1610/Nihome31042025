namespace NihomeBackend.Models.DTOs.Requests;

public class UpdateEmailTemplatesRequest
{
    public string? NewApplicationEmailSubjectTemplate { get; set; }
    public string? NewApplicationEmailBodyTemplate { get; set; }
    public string? NotificationEmail { get; set; }
    public string? OtpEmailSubjectTemplate { get; set; }
    public string? OtpEmailBodyTemplate { get; set; }
}
