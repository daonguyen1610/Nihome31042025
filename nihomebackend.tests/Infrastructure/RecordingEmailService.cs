using NihomeBackend.Services;

namespace nihomebackend.tests.Infrastructure;

public sealed class RecordingEmailService : IEmailService
{
    private readonly List<SentEmail> _sentEmails = [];

    public IReadOnlyList<SentEmail> SentEmails => _sentEmails;

    public Task SendEmailAsync(string toEmail, string subject, string htmlContent)
    {
        _sentEmails.Add(new SentEmail(toEmail, subject, htmlContent));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(string ToEmail, string Subject, string HtmlContent);
