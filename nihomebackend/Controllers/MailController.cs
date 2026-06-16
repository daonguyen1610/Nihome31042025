using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NihomeBackend.Authorization;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests.Mail;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[Authorize]
[RequirePermission("mail", "send")]
[ApiController]
[Route("api/[controller]")]
public class MailController : ControllerBase
{
    private readonly ILogger<MailController> _logger;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public MailController(
        ILogger<MailController> logger,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings)
    {
        _logger = logger;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMailRequest request)
    {
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "[Nihome] Test email"
            : request.Subject.Trim();
        var htmlBody = string.IsNullOrWhiteSpace(request.HtmlBody)
            ? "<p>Nihome email configuration is working.</p>"
            : request.HtmlBody.Trim();

        await _emailService.SendEmailAsync(request.ToEmail, subject, htmlBody);
        return Ok(new { message = "Email sent successfully.", toEmail = request.ToEmail });
    }

    [HttpPost("diagnose")]
    public async Task<IActionResult> Diagnose()
    {
        var username = _emailSettings.Username?.Trim();
        var password = _emailSettings.Password?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return BadRequest(new { message = "EmailSettings Username/Password is missing." });
        }

        var socketOptions = SecureSocketOptions.None;
        if (_emailSettings.UseSsl)
        {
            socketOptions = SecureSocketOptions.SslOnConnect;
        }
        else if (_emailSettings.UseStartTls)
        {
            socketOptions = SecureSocketOptions.StartTls;
        }

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, socketOptions);
            var authMechanisms = smtp.AuthenticationMechanisms.ToArray();
            smtp.AuthenticationMechanisms.Clear();
            smtp.AuthenticationMechanisms.Add("LOGIN");
            await smtp.AuthenticateAsync(new SaslMechanismLogin(username, password));
            await smtp.DisconnectAsync(true);

            return Ok(new
            {
                message = "SMTP connection and authentication succeeded.",
                host = _emailSettings.Host,
                port = _emailSettings.Port,
                useSsl = _emailSettings.UseSsl,
                useStartTls = _emailSettings.UseStartTls,
                authenticationMechanisms = authMechanisms,
                selectedMechanism = "LOGIN"
            });
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "SMTP authentication failed.");
            return BadRequest(new
            {
                message = "SMTP authentication failed.",
                error = ex.Message,
                host = _emailSettings.Host,
                port = _emailSettings.Port,
                useSsl = _emailSettings.UseSsl,
                useStartTls = _emailSettings.UseStartTls,
                username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP diagnostic failed.");
            return StatusCode(500, new
            {
                message = "SMTP diagnostic failed.",
                error = ex.Message,
                host = _emailSettings.Host,
                port = _emailSettings.Port,
                useSsl = _emailSettings.UseSsl,
                useStartTls = _emailSettings.UseStartTls
            });
        }
    }
}
