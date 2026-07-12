using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;

namespace NihomeBackend.Data;

/// <summary>
/// Idempotent bootstrap of the <see cref="NotificationTemplate"/> catalogue.
/// Loads <c>Data/Seeds/notification-templates/defaults.json</c> at every
/// startup. New template rows are inserted; existing rows are never
/// overwritten so admin edits to <c>Channel</c> and <c>IsActive</c> survive
/// a redeploy.
///
/// Title and body i18n live in <c>Data/Seeds/i18n/notification-templates.json</c>
/// and are seeded through the standard <see cref="TranslationSeeder"/>.
/// </summary>
public static class NotificationTemplateSeeder
{
    public static void Seed(AppDbContext db) => Seed(db, typeof(NotificationTemplateSeeder).Assembly);

    public static void Seed(AppDbContext db, Assembly assembly)
    {
        // .NET rewrites the hyphenated folder name to <c>notification_templates</c>
        // inside the resource id, so match either form.
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".notification_templates.defaults.json", StringComparison.OrdinalIgnoreCase)
                              || n.EndsWith(".notification-templates.defaults.json", StringComparison.OrdinalIgnoreCase));
        if (resource == null) return;

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("templates", out var templatesEl) ||
            templatesEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var existingCodes = db.NotificationTemplates
            .Select(t => t.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var toInsert = new List<NotificationTemplate>();

        foreach (var el in templatesEl.EnumerateArray())
        {
            var code = el.TryGetProperty("code", out var codeProp)
                ? (codeProp.GetString() ?? string.Empty).Trim()
                : string.Empty;
            if (string.IsNullOrEmpty(code) || existingCodes.Contains(code)) continue;

            var module = el.TryGetProperty("module", out var modProp)
                ? (modProp.GetString() ?? string.Empty).Trim()
                : string.Empty;
            var channelStr = el.TryGetProperty("channel", out var chanProp)
                ? chanProp.GetString()
                : nameof(NotificationChannel.InApp);
            var channel = Enum.TryParse<NotificationChannel>(channelStr, ignoreCase: true, out var parsed)
                ? parsed
                : NotificationChannel.InApp;
            var adminDescription = el.TryGetProperty("adminDescription", out var descProp)
                ? descProp.GetString()
                : null;

            toInsert.Add(new NotificationTemplate
            {
                Code = code,
                Module = module,
                TitleKey = $"notification.{code}.title",
                BodyKey = $"notification.{code}.body",
                Channel = channel,
                IsActive = true,
                AdminDescription = adminDescription,
                CreatedAt = now,
            });
        }

        if (toInsert.Count == 0) return;

        db.NotificationTemplates.AddRange(toInsert);
        db.SaveChanges();
    }
}
