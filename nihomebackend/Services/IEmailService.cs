namespace NihomeBackend.Services;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task SendEmailWithCcAsync(string toEmail, IReadOnlyCollection<string> ccEmails,
        string subject, string htmlBody, CancellationToken ct = default)
    {
        if (ccEmails.Count != 0) throw new NotSupportedException("This email sender does not support CC.");
        return SendEmailAsync(toEmail, subject, htmlBody);
    }
    Task SendEmailWithAttachmentAsync(
        string toEmail,
        string subject,
        string htmlBody,
        EmailAttachment attachment,
        CancellationToken ct = default);
}
