using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

/// <summary>
/// Reusable notification blueprint keyed by a stable string code
/// (e.g. <c>quote.submitted-for-approval</c>). Titles and bodies are
/// resolved from the standard translation table by the key convention
/// <c>notification.&lt;code&gt;.title</c> / <c>notification.&lt;code&gt;.body</c>
/// — see <c>Data/Seeds/i18n/notification-templates.json</c>.
///
/// The <see cref="Channel"/> flag lets admins route the same event to
/// in-app only, email only, or both without touching code.
///
/// Placeholder syntax in the translated body is <c>{{token}}</c>. Callers
/// pass a small string dictionary at fire time (e.g.
/// <c>{ "quoteNumber": "Q/2026/001", "amount": "12,500,000₫" }</c>) and the
/// service renders them before persisting/sending.
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Module { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string TitleKey { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string BodyKey { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public bool IsActive { get; set; } = true;

    /// <summary>Optional short admin-facing description; not shown to end users.</summary>
    [MaxLength(400)]
    public string? AdminDescription { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
