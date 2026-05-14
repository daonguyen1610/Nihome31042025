using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public partial class LegacyProcessImportService(
    IConfiguration configuration,
    ProcessAssetStorageService assetStorage,
    ProcessService processService,
    ILogger<LegacyProcessImportService> logger)
{
    private const string DefaultBaseUrl = "https://nicon.vn";

    private static readonly LegacyProcessPage[] LegacyPages =
    [
        new("general", "GeneralProcess"),
        new("ptcskh", "PTCSKHProcess"),
        new("dt", "DTProcess"),
        new("tk", "TKProcess"),
        new("tc", "TCProcess"),
        new("ttqtct", "TTQTCTProcess"),
        new("qlns", "QLNSProcess"),
        new("mhdgncu", "MHDGNCUProcess"),
    ];

    public async Task<LegacyProcessImportResponse> ImportAsync(
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var email = configuration["LegacyProcess:Email"]
            ?? Environment.GetEnvironmentVariable("NICON_LEGACY_EMAIL");
        var password = configuration["LegacyProcess:Password"]
            ?? Environment.GetEnvironmentVariable("NICON_LEGACY_PASSWORD");
        var baseUrl = configuration["LegacyProcess:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("NICON_LEGACY_BASE_URL")
            ?? DefaultBaseUrl;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Legacy process credentials are missing. Set NICON_LEGACY_EMAIL and NICON_LEGACY_PASSWORD.");
        }

        using var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5),
        };

        await LoginAsync(client, email, password, cancellationToken);

        var parsedGroups = new List<ParsedLegacyProcessGroup>();
        foreach (var page in LegacyPages)
        {
            var html = await client.GetStringAsync($"/Admin/{page.PageName}", cancellationToken);
            var processes = ParseLegacyPage(page.GroupKey, page.PageName, html);
            parsedGroups.Add(new ParsedLegacyProcessGroup(page.GroupKey, page.PageName, processes));
            logger.LogInformation(
                "Parsed legacy process page {PageName}: processes={ProcessCount}",
                page.PageName,
                processes.Count);
        }

        var processCount = parsedGroups.Sum(g => g.Processes.Count);
        var imageCount = parsedGroups.Sum(g => g.Processes.Sum(p => p.Images.Count));
        var fileCount = parsedGroups.Sum(g => g.Processes.Sum(p => p.Files.Count));

        if (dryRun)
        {
            return new LegacyProcessImportResponse
            {
                DryRun = true,
                Groups = parsedGroups.Count,
                Processes = processCount,
                Images = imageCount,
                Files = fileCount,
                Message = "Dry-run completed. No database rows or files were changed.",
            };
        }

        var downloadedAssetUrls = new List<string>();
        var importedGroups = new List<LegacyProcessGroupImport>();
        var importedImageCount = 0;
        var importedFileCount = 0;
        var skippedAssetCount = 0;
        try
        {
            foreach (var group in parsedGroups)
            {
                var importedProcesses = new List<LegacyProcessDocumentImport>();
                foreach (var process in group.Processes)
                {
                    var assets = new List<LegacyProcessAssetImport>();

                    for (var i = 0; i < process.Images.Count; i++)
                    {
                        var image = process.Images[i];
                        var stored = await TryDownloadAssetAsync(
                            client,
                            image.Url,
                            image.DisplayName,
                            ProcessAssetType.Image,
                            cancellationToken);
                        if (stored == null)
                        {
                            skippedAssetCount++;
                            continue;
                        }

                        downloadedAssetUrls.Add(stored.Url);

                        assets.Add(new LegacyProcessAssetImport(
                            ProcessAssetType.Image,
                            image.DisplayName,
                            stored.Url,
                            stored.OriginalFileName,
                            stored.ContentType,
                            stored.FileSizeBytes,
                            i));
                        importedImageCount++;
                    }

                    for (var i = 0; i < process.Files.Count; i++)
                    {
                        var file = process.Files[i];
                        var stored = await TryDownloadAssetAsync(
                            client,
                            file.Url,
                            file.DisplayName,
                            ProcessAssetType.File,
                            cancellationToken);
                        if (stored == null)
                        {
                            skippedAssetCount++;
                            continue;
                        }

                        downloadedAssetUrls.Add(stored.Url);

                        assets.Add(new LegacyProcessAssetImport(
                            ProcessAssetType.File,
                            file.DisplayName,
                            stored.Url,
                            stored.OriginalFileName,
                            stored.ContentType,
                            stored.FileSizeBytes,
                            i));
                        importedFileCount++;
                    }

                    importedProcesses.Add(new LegacyProcessDocumentImport(process.Title, process.Code, assets));
                }

                importedGroups.Add(new LegacyProcessGroupImport(group.GroupKey, importedProcesses));
            }

            await processService.ReplaceGroupDataAsync(importedGroups, cancellationToken);
        }
        catch
        {
            foreach (var assetUrl in downloadedAssetUrls)
            {
                assetStorage.DeleteIfManagedAsset(assetUrl);
            }

            throw;
        }

        return new LegacyProcessImportResponse
        {
            DryRun = false,
            Groups = importedGroups.Count,
            Processes = processCount,
            Images = importedImageCount,
            Files = importedFileCount,
            SkippedAssets = skippedAssetCount,
            Message = skippedAssetCount == 0
                ? "Legacy process import completed. Verify admin pages and keep a backup of wwwroot/process-assets."
                : $"Legacy process import completed with {skippedAssetCount} skipped assets. Verify admin pages and upload skipped files manually if needed.",
        };
    }

    public static IReadOnlyList<ParsedLegacyProcess> ParseLegacyPage(
        string groupKey,
        string pageName,
        string html)
    {
        var titleMatches = ShowHideTitleRegex().Matches(html);
        var processes = new List<ParsedLegacyProcess>();

        if (titleMatches.Count > 0)
        {
            for (var i = 0; i < titleMatches.Count; i++)
            {
                var title = CleanText(titleMatches[i].Groups[1].Value);
                var start = titleMatches[i].Index + titleMatches[i].Length;
                var end = i + 1 < titleMatches.Count ? titleMatches[i + 1].Index : html.Length;
                var segment = html[start..end];
                processes.Add(ParseProcessSegment(title, segment));
            }
        }
        else
        {
            var heading = H2Regex().Match(html);
            var title = heading.Success ? CleanText(heading.Groups[1].Value) : pageName;
            var start = heading.Success ? heading.Index + heading.Length : 0;
            var footerIndex = html.IndexOf("<div class=\"main-footer", start, StringComparison.OrdinalIgnoreCase);
            var end = footerIndex >= 0 ? footerIndex : html.Length;
            processes.Add(ParseProcessSegment(title, html[start..end]));
        }

        return processes
            .Where(p => p.Images.Count > 0 || p.Files.Count > 0)
            .Select(p => p with { Code = ExtractCode(groupKey, p.Title) })
            .ToList();
    }

    private static ParsedLegacyProcess ParseProcessSegment(string title, string segment)
    {
        var images = ImageRegex().Matches(segment)
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .Where(IsDownloadableLegacyAssetUrl)
            .Where(src => src.Contains("/Themes/Nicon/Content/file/", StringComparison.OrdinalIgnoreCase))
            .Select((src, index) => new ParsedLegacyAsset(
                CleanFileName(Path.GetFileName(src), $"process-image-{index + 1}.jpg"),
                src,
                index))
            .ToList();

        var files = new List<ParsedLegacyAsset>();
        foreach (Match row in TableRowRegex().Matches(segment))
        {
            var cells = TableCellRegex().Matches(row.Groups[1].Value);
            var href = HrefRegex().Match(row.Groups[1].Value);
            if (cells.Count == 0 || !href.Success)
            {
                continue;
            }

            var url = WebUtility.HtmlDecode(href.Groups[1].Value);
            if (!IsDownloadableLegacyAssetUrl(url))
            {
                continue;
            }

            var displayName = CleanText(cells[0].Groups[1].Value);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = CleanFileName(Path.GetFileName(url), $"process-file-{files.Count + 1}");
            }

            files.Add(new ParsedLegacyAsset(
                displayName,
                url,
                files.Count));
        }

        return new ParsedLegacyProcess(title, null, images, files);
    }

    private static bool IsDownloadableLegacyAssetUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith("/", StringComparison.Ordinal) &&
            !url.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        return !url.StartsWith("#", StringComparison.Ordinal) &&
            !url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractCode(string groupKey, string title)
    {
        if (!string.Equals(groupKey, "tc", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = title.Trim();
        var dotIndex = normalized.IndexOf('.');
        if (dotIndex < 0 || dotIndex > 3)
        {
            return null;
        }

        return normalized[..dotIndex].Trim();
    }

    private async Task LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var loginPage = await client.GetAsync("/login?ReturnUrl=%2fAdmin%2fGeneralProcess", cancellationToken);
        loginPage.EnsureSuccessStatusCode();

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("RememberMe", "true"),
        ]);

        using var response = await client.PostAsync(
            "/login?ReturnUrl=%2fAdmin%2fGeneralProcess",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Contains("html-login-page", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("class=\"login-page\"", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Legacy process login failed.");
        }
    }

    private async Task<StoredProcessAsset> DownloadAssetAsync(
        HttpClient client,
        string relativeOrAbsoluteUrl,
        string displayName,
        ProcessAssetType type,
        CancellationToken cancellationToken)
    {
        var assetUri = Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(client.BaseAddress!, relativeOrAbsoluteUrl.TrimStart('/'));

        using var response = await client.GetAsync(assetUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await assetStorage.SaveLegacyAsync(
            stream,
            displayName,
            response.Content.Headers.ContentType?.MediaType,
            response.Content.Headers.ContentLength,
            type,
            cancellationToken);
    }

    private async Task<StoredProcessAsset?> TryDownloadAssetAsync(
        HttpClient client,
        string relativeOrAbsoluteUrl,
        string displayName,
        ProcessAssetType type,
        CancellationToken cancellationToken)
    {
        try
        {
            var assetUri = ResolveHttpAssetUri(client, relativeOrAbsoluteUrl);
            if (assetUri == null)
            {
                logger.LogWarning(
                    "Skipping unsupported legacy process asset URL {Url}",
                    relativeOrAbsoluteUrl);
                return null;
            }

            return await DownloadAssetAsync(client, assetUri.ToString(), displayName, type, cancellationToken);
        }
        catch (Exception ex) when (ex is NotSupportedException or HttpRequestException or IOException)
        {
            logger.LogWarning(
                ex,
                "Skipping legacy process asset {DisplayName} from {Url}",
                displayName,
                relativeOrAbsoluteUrl);
            return null;
        }
    }

    private static Uri? ResolveHttpAssetUri(HttpClient client, string relativeOrAbsoluteUrl)
    {
        if (relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal) &&
            !relativeOrAbsoluteUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return new Uri(client.BaseAddress!, relativeOrAbsoluteUrl);
        }

        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps
                ? absolute
                : null;
        }

        var relativeUrl = relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal)
            ? relativeOrAbsoluteUrl
            : relativeOrAbsoluteUrl.TrimStart('/');
        var uri = new Uri(client.BaseAddress!, relativeUrl);

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;
    }

    private static string CleanText(string value)
    {
        var withoutTags = TagRegex().Replace(value, "");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private static string CleanFileName(string? value, string fallback)
    {
        value = WebUtility.HtmlDecode(value ?? "").Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    [GeneratedRegex("<a\\s+class=\"showhideTable\"[^>]*>\\s*(.*?)\\s*</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ShowHideTitleRegex();

    [GeneratedRegex("<h2[^>]*>\\s*(.*?)\\s*</h2>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H2Regex();

    [GeneratedRegex("<img\\s+[^>]*src=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();

    [GeneratedRegex("<tr[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex("<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableCellRegex();

    [GeneratedRegex("href=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex HrefRegex();

    [GeneratedRegex("<.*?>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}

public record ParsedLegacyProcessGroup(
    string GroupKey,
    string PageName,
    IReadOnlyList<ParsedLegacyProcess> Processes);

public record ParsedLegacyProcess(
    string Title,
    string? Code,
    IReadOnlyList<ParsedLegacyAsset> Images,
    IReadOnlyList<ParsedLegacyAsset> Files);

public record ParsedLegacyAsset(
    string DisplayName,
    string Url,
    int SortOrder);

public record LegacyProcessPage(string GroupKey, string PageName);
