using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

// Scans every content table for uploaded image URLs still in use, so
// UploadedImageCleanupService never deletes a file a page still renders.
// JSON-blob columns are walked generically (every string value is checked
// against the managed prefix, regardless of property name), so adding a new
// field to an existing entity's JSON does not require touching this file.
// Adding a brand new entity type that stores managed image URLs still does.
public static class ReferencedUploadedImages
{
    private const string ManagedImagePrefix = "/images/upload/";

    public static async Task<HashSet<string>> GetAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfManaged(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(ManagedImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relPath = url[ManagedImagePrefix.Length..].TrimStart('/');
            if (!string.IsNullOrWhiteSpace(relPath))
            {
                referenced.Add(relPath);
            }
        }

        var activities = await db.Activities.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson, x.ContentJson })
            .ToListAsync(cancellationToken);
        foreach (var a in activities)
        {
            AddIfManaged(a.ImageUrl);
            WalkJson(a.GalleryJson, AddIfManaged);
            WalkJson(a.ContentJson, AddIfManaged);
        }

        var news = await db.NewsArticles.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson, x.ContentJson })
            .ToListAsync(cancellationToken);
        foreach (var n in news)
        {
            AddIfManaged(n.ImageUrl);
            WalkJson(n.GalleryJson, AddIfManaged);
            WalkJson(n.ContentJson, AddIfManaged);
        }

        var projects = await db.Projects.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson, x.ChallengesJson, x.SolutionsJson, x.HighlightsJson })
            .ToListAsync(cancellationToken);
        foreach (var p in projects)
        {
            AddIfManaged(p.ImageUrl);
            WalkJson(p.GalleryJson, AddIfManaged);
            WalkJson(p.ChallengesJson, AddIfManaged);
            WalkJson(p.SolutionsJson, AddIfManaged);
            WalkJson(p.HighlightsJson, AddIfManaged);
        }

        var services = await db.ServiceItems.AsNoTracking()
            .Select(x => new { x.SectionsJson, x.HighlightsJson, x.IntroBlocksJson })
            .ToListAsync(cancellationToken);
        foreach (var s in services)
        {
            WalkJson(s.SectionsJson, AddIfManaged);
            WalkJson(s.HighlightsJson, AddIfManaged);
            WalkJson(s.IntroBlocksJson, AddIfManaged);
        }

        var logoImageUrls = await db.ClientLogos.AsNoTracking().Select(x => x.ImageUrl).ToListAsync(cancellationToken);
        foreach (var url in logoImageUrls)
        {
            AddIfManaged(url);
        }

        var slideshowImageUrls = await db.SlideshowItems.AsNoTracking().Select(x => x.ImageUrl).ToListAsync(cancellationToken);
        foreach (var url in slideshowImageUrls)
        {
            AddIfManaged(url);
        }

        var aboutSections = await db.AboutSectionContents.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.ItemsJson })
            .ToListAsync(cancellationToken);
        foreach (var section in aboutSections)
        {
            AddIfManaged(section.ImageUrl);
            WalkJson(section.ItemsJson, AddIfManaged);
        }

        var processDocuments = await db.ProcessDocuments.AsNoTracking()
            .Select(x => new { x.ImagesJson, x.FilesJson })
            .ToListAsync(cancellationToken);
        foreach (var p in processDocuments)
        {
            WalkJson(p.ImagesJson, AddIfManaged);
            WalkJson(p.FilesJson, AddIfManaged);
        }

        return referenced;
    }

    private static void WalkJson(string? json, Action<string?> addIfManaged)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            WalkElement(doc.RootElement, addIfManaged);
        }
        catch (JsonException)
        {
            // Ignore malformed legacy data to keep the scan resilient.
        }
    }

    private static void WalkElement(JsonElement element, Action<string?> addIfManaged)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    WalkElement(prop.Value, addIfManaged);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    WalkElement(item, addIfManaged);
                }
                break;
            case JsonValueKind.String:
                addIfManaged(element.GetString());
                break;
        }
    }
}
