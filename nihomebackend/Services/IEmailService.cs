namespace NihomeBackend.Services;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task SendEmailWithAttachmentAsync(
        string toEmail,
        string subject,
        string htmlBody,
        EmailAttachment attachment,
        CancellationToken ct = default);
}
