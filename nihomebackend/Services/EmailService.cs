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
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

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

        await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, socketOptions);
        smtp.AuthenticationMechanisms.Clear();
        smtp.AuthenticationMechanisms.Add("LOGIN");
        await smtp.AuthenticateAsync(new SaslMechanismLogin(username, password));
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        _logger.LogInformation("Email sent to {Email}", toEmail);
    }
}
