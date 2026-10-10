using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        await SendAsync(toEmail, subject, htmlBody, null, CancellationToken.None);
    }

    public async Task SendEmailWithAttachmentAsync(
        string toEmail,
        string subject,
        string htmlBody,
        EmailAttachment attachment,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        await SendAsync(toEmail, subject, htmlBody, attachment, ct);
    }

    private async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        EmailAttachment? attachment,
        CancellationToken ct)
    {
        var username = _emailSettings.Username?.Trim();
        var password = _emailSettings.Password?.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("EmailSettings Username/Password is missing.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment is not null)
        {
            body.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                MimeKit.ContentType.Parse(attachment.ContentType));
        }
        message.Body = body.ToMessageBody();

        using var smtp = new SmtpClient();

        var socketOptions = SecureSocketOptions.None;
        if (_emailSettings.UseSsl)
        {
            socketOptions = SecureSocketOptions.SslOnConnect;
        }
        else if (_emailSettings.UseStartTls)
        {
            socketOptions = SecureSocketOptions.StartTls;
        }

        await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, socketOptions, ct);
        smtp.AuthenticationMechanisms.Clear();
        smtp.AuthenticationMechanisms.Add("LOGIN");
        await smtp.AuthenticateAsync(new SaslMechanismLogin(username, password), ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent to {Email}", toEmail);
    }
}
