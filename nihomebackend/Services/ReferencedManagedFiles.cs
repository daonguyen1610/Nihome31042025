using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>
/// Collects every managed local file that is still referenced by active
/// application data. Historical audit/snapshot payloads are intentionally not
/// treated as ownership: they describe past state and must not keep orphaned
/// physical files alive forever.
/// </summary>
public static class ReferencedManagedFiles
{
    private static readonly string[] ManagedPrefixes =
    [
        "/images/upload/",
        "/images/processes/",
        "/files/",
    ];

    public static async Task<HashSet<string>> GetAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfManaged(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            var path = value.Trim();
            if (!ManagedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                return;

            var queryOrFragment = path.IndexOfAny(['?', '#']);
            if (queryOrFragment >= 0) path = path[..queryOrFragment];
            if (path.Contains('\\') || path.Split('/').Any(segment => segment is "." or "..")) return;

            referenced.Add(path);
        }

        async Task AddValuesAsync(IQueryable<string?> query)
        {
            foreach (var value in await query.ToListAsync(cancellationToken))
                AddIfManaged(value);
        }

        async Task AddJsonValuesAsync(IQueryable<string?> query)
        {
            foreach (var value in await query.ToListAsync(cancellationToken))
                WalkJson(value, AddIfManaged);
        }

        var activities = await db.Activities.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson, x.ContentJson })
            .ToListAsync(cancellationToken);
        foreach (var item in activities)
        {
            AddIfManaged(item.ImageUrl);
            WalkJson(item.GalleryJson, AddIfManaged);
            WalkJson(item.ContentJson, AddIfManaged);
        }

        var news = await db.NewsArticles.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.GalleryJson, x.ContentJson })
            .ToListAsync(cancellationToken);
        foreach (var item in news)
        {
            AddIfManaged(item.ImageUrl);
            WalkJson(item.GalleryJson, AddIfManaged);
            WalkJson(item.ContentJson, AddIfManaged);
        }

        var projects = await db.Projects.AsNoTracking()
            .Select(x => new
            {
                x.ImageUrl,
                x.GalleryJson,
                x.ChallengesJson,
                x.SolutionsJson,
                x.HighlightsJson,
                x.ContentJson,
            })
            .ToListAsync(cancellationToken);
        foreach (var item in projects)
        {
            AddIfManaged(item.ImageUrl);
            WalkJson(item.GalleryJson, AddIfManaged);
            WalkJson(item.ChallengesJson, AddIfManaged);
            WalkJson(item.SolutionsJson, AddIfManaged);
            WalkJson(item.HighlightsJson, AddIfManaged);
            WalkJson(item.ContentJson, AddIfManaged);
        }

        var logos = await db.ClientLogos.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.Href })
            .ToListAsync(cancellationToken);
        foreach (var item in logos)
        {
            AddIfManaged(item.ImageUrl);
            AddIfManaged(item.Href);
        }

        var slideshowItems = await db.SlideshowItems.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.LinkUrl })
            .ToListAsync(cancellationToken);
        foreach (var item in slideshowItems)
        {
            AddIfManaged(item.ImageUrl);
            AddIfManaged(item.LinkUrl);
        }

        var aboutSections = await db.AboutSectionContents.AsNoTracking()
            .Select(x => new { x.ImageUrl, x.ItemsJson })
            .ToListAsync(cancellationToken);
        foreach (var item in aboutSections)
        {
            AddIfManaged(item.ImageUrl);
            WalkJson(item.ItemsJson, AddIfManaged);
        }

        await AddJsonValuesAsync(db.ServiceItems.Select(x => x.SectionsJson));
        await AddJsonValuesAsync(db.ServiceItems.Select(x => x.HighlightsJson));
        await AddJsonValuesAsync(db.ServiceItems.Select(x => x.IntroBlocksJson));
        await AddJsonValuesAsync(db.ProcessDocuments.Select(x => x.ImagesJson));
        await AddJsonValuesAsync(db.ProcessDocuments.Select(x => x.FilesJson));

        await AddValuesAsync(db.Users.Select(x => x.AvatarUrl));
        await AddValuesAsync(db.UserDocuments.Select(x => x.FileUrl));
        await AddValuesAsync(db.JobApplications.Select(x => x.CvUrl));
        await AddValuesAsync(db.CustomerDocuments.Select(x => x.FilePath));
        await AddValuesAsync(db.Vendors.Select(x => x.CapabilityFileUrl));
        await AddValuesAsync(db.VendorDocumentUploads.Select(x => x.Path));
        await AddValuesAsync(db.QuoteDocuments.Select(x => x.FilePath));
        await AddValuesAsync(db.ContractAppendices.Select(x => x.FilePath));
        await AddValuesAsync(db.ContractAttachments.Select(x => x.FilePath));
        await AddValuesAsync(db.CapabilityDocuments.Select(x => x.FilePath));
        await AddValuesAsync(db.CapabilityDocumentVersions.Select(x => x.FilePath));
        await AddValuesAsync(db.TenderChecklistItems.Select(x => x.FilePath));
        await AddValuesAsync(db.TenderChecklistEvidence.Select(x => x.FilePath));
        await AddValuesAsync(db.TenderInvitationDocuments.Select(x => x.FilePath));
        await AddValuesAsync(db.TenderSubmissionItems.Select(x => x.FilePath));
        await AddValuesAsync(db.SurveyMedia.Select(x => x.RelativePath));
        await AddValuesAsync(db.ProjectDocuments.Select(x => x.LocalPath));
        await AddValuesAsync(db.PaymentRequestAttachments.Select(x => x.FilePath));
        await AddValuesAsync(db.BasicDesignDocs.Select(x => x.FilePath));
        await AddValuesAsync(db.ShopDrawings.Select(x => x.FilePath));

        var permitFiles = await db.PermitChecklistItems.AsNoTracking()
            .Select(x => new { x.SubmittedFilePath, x.IssuedFilePath })
            .ToListAsync(cancellationToken);
        foreach (var item in permitFiles)
        {
            AddIfManaged(item.SubmittedFilePath);
            AddIfManaged(item.IssuedFilePath);
        }

        await AddJsonValuesAsync(db.AcceptanceRecords.Select(x => x.Documents));
        await AddValuesAsync(db.AsBuiltDocuments.Select(x => x.FileUrl));
        await AddJsonValuesAsync(db.HandoverRecords.Select(x => x.Documents));
        await AddJsonValuesAsync(db.HseViolations.Select(x => x.EvidenceDocumentsJson));

        return referenced;
    }

    private static void WalkJson(string? json, Action<string?> addIfManaged)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        // Fail the complete reference scan when active JSON is malformed.
        // Cleanup resolves every reference before touching the filesystem, so
        // an unverifiable record can never cause a still-used file to be lost.
        using var document = JsonDocument.Parse(json);
        WalkElement(document.RootElement, addIfManaged);
    }

    private static void WalkElement(JsonElement element, Action<string?> addIfManaged)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    WalkElement(property.Value, addIfManaged);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    WalkElement(item, addIfManaged);
                break;
            case JsonValueKind.String:
                addIfManaged(element.GetString());
                break;
        }
    }
}

public interface IManagedFileReferenceProvider
{
    Task<HashSet<string>> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class ManagedFileReferenceProvider(AppDbContext db) : IManagedFileReferenceProvider
{
    public Task<HashSet<string>> GetAsync(CancellationToken cancellationToken = default) =>
        ReferencedManagedFiles.GetAsync(db, cancellationToken);
}
