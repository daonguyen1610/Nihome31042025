using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace nihomebackend.tests.Helpers;

/// <summary>
/// Builds a real <see cref="NotificationService"/> for unit tests with
/// stub email + a fresh TranslationService bound to the same DbContext.
/// Optional <paramref name="emailCapture"/> lets a test observe outgoing
/// mail (useful for channel-routing assertions).
/// </summary>
public static class NotificationServiceTestFactory
{
    public static NotificationService Create(
        AppDbContext db,
        CapturingEmailService? emailCapture = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var translations = new TranslationService(db, cache);
        var email = emailCapture ?? new CapturingEmailService();
        return new NotificationService(db, email, translations, NullLogger<NotificationService>.Instance);
    }
}

/// <summary>Captures outgoing emails so tests can assert on channel routing.</summary>
public sealed class CapturingEmailService : IEmailService
{
    private readonly List<(string To, string Subject, string Body)> _sent = new();

    public IReadOnlyList<(string To, string Subject, string Body)> Sent => _sent;

    public bool ThrowOnSend { get; set; }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (ThrowOnSend) throw new InvalidOperationException("SMTP down");
        _sent.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
