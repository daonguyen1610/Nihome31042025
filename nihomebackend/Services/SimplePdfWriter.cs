using OpenPdf.Fonts;
using Microsoft.Extensions.Logging;

namespace NihomeBackend.Services;

internal static class SimplePdfWriter
{
    private const int LinesPerPage = 48;

    public static byte[] Create(
        IEnumerable<string> sourceLines,
        string languageCode,
        ILogger? logger = null)
    {
        var lines = sourceLines.ToList();
        if (lines.Count == 0) lines.Add(string.Empty);
        var normalizedLanguage = languageCode?.Trim().ToLowerInvariant() ?? string.Empty;
        var supportedLanguage = normalizedLanguage is "vi" or "en" or "ja" or "zh";
        if (!supportedLanguage)
        {
            logger?.LogWarning(
                "Unsupported PDF language code {LanguageCode}; using the default font.",
                languageCode);
            normalizedLanguage = "en";
        }

        var fontSource = ResolveFont(normalizedLanguage, logger);

        using var stream = new MemoryStream();
        using var document = OpenPdf.Document.PdfDocument.Create(stream);
        foreach (var pageLines in lines.Chunk(LinesPerPage))
        {
            var page = document.AddPage(595, 842);
            var cjkFont = page.AddTrueTypeFont(TrueTypeFont.Load(fontSource.Path, fontSource.CollectionIndex));
            var latinSource = ResolveFont("en", logger);
            var latinFont = page.AddTrueTypeFont(TrueTypeFont.Load(latinSource.Path, latinSource.CollectionIndex));
            var y = 790d;
            foreach (var line in pageLines)
            {
                if (line.Length > 0)
                {
                    var start = 0;
                    var useCjk = IsCjk(line[0]);
                    var x = 50d;
                    for (var index = 1; index <= line.Length; index++)
                    {
                        if (index < line.Length && IsCjk(line[index]) == useCjk) continue;

                        var run = line[start..index];
                        page.DrawText(useCjk ? cjkFont : latinFont, 10, x, y, run);
                        x += run.Length * (useCjk ? 10d : 5.5d);
                        if (index < line.Length)
                        {
                            start = index;
                            useCjk = IsCjk(line[index]);
                        }
                    }
                }
                y -= 15;
            }
        }
        document.Save();
        return stream.ToArray();
    }

    private static bool IsCjk(char character) =>
        character is >= '\u2E80' and <= '\u9FFF' or >= '\uAC00' and <= '\uD7AF';

    private static FontSource ResolveFont(string languageCode, ILogger? logger)
    {
        var configured = ResolveConfiguredFont(languageCode);
        if (configured is not null) return configured;

        if (OperatingSystem.IsLinux())
        {
            return languageCode switch
            {
                "ja" => FirstAvailableFont(
                    new("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 0),
                    new("/usr/share/fonts/opentype/noto/NotoSansCJKjp-Regular.otf", 0)),
                "zh" => FirstAvailableFont(
                    new("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 2),
                    new("/usr/share/fonts/opentype/noto/NotoSansCJKsc-Regular.otf", 0)),
                _ => FirstAvailableFont(
                    new("/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf", 0),
                    new("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 0)),
            };
        }

        if (OperatingSystem.IsWindows())
        {
            var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            return languageCode switch
            {
                "ja" => FirstAvailableFont(
                    new(Path.Combine(fonts, "meiryo.ttc"), 0),
                    new(Path.Combine(fonts, "msgothic.ttc"), 0),
                    new(Path.Combine(fonts, "YuGothM.ttc"), 0),
                    DefaultWindowsFont(fonts, logger)),
                "zh" => FirstAvailableFont(
                    new(Path.Combine(fonts, "msyh.ttc"), 0),
                    DefaultWindowsFont(fonts, logger)),
                _ => DefaultWindowsFont(fonts, logger),
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return languageCode switch
            {
                "ja" or "zh" => FirstAvailableFont(
                    new("/System/Library/Fonts/Supplemental/Arial Unicode.ttf", 0),
                    new("/Library/Fonts/Arial Unicode.ttf", 0)),
                _ => FirstAvailableFont(
                    new("/System/Library/Fonts/Supplemental/Arial.ttf", 0),
                    new("/System/Library/Fonts/Supplemental/Arial Unicode.ttf", 0)),
            };
        }

        throw new InvalidOperationException(
            "PDF export requires a configured TrueType font on this operating system.");
    }

    private static FontSource DefaultWindowsFont(string fonts, ILogger? logger)
    {
        var defaultFont = new FontSource(Path.Combine(fonts, "arial.ttf"), 0);
        if (!File.Exists(defaultFont.Path))
        {
            logger?.LogWarning(
                "The default PDF font was not found at {FontPath}; PDF export may fail.",
                defaultFont.Path);
        }

        return defaultFont;
    }

    private static FontSource? ResolveConfiguredFont(string languageCode)
    {
        var suffix = languageCode switch
        {
            "ja" => "JA",
            "zh" => "ZH",
            _ => "LATIN",
        };
        var pathVariable = $"NIHOME_PDF_FONT_{suffix}_PATH";
        var path = Environment.GetEnvironmentVariable(pathVariable);
        if (string.IsNullOrWhiteSpace(path)) return null;

        var indexVariable = $"NIHOME_PDF_FONT_{suffix}_INDEX";
        var rawIndex = Environment.GetEnvironmentVariable(indexVariable);
        var collectionIndex = 0;
        if (!string.IsNullOrWhiteSpace(rawIndex) &&
            (!int.TryParse(rawIndex, out collectionIndex) || collectionIndex < 0))
        {
            throw new InvalidOperationException($"{indexVariable} must be a non-negative integer.");
        }

        return RequiredFont(path, collectionIndex);
    }

    private static FontSource FirstAvailableFont(params FontSource[] candidates)
    {
        var font = candidates.FirstOrDefault(candidate => File.Exists(candidate.Path));
        if (font is not null) return font;

        throw new InvalidOperationException(
            $"No supported PDF font is installed. Checked: {string.Join(", ", candidates.Select(candidate => candidate.Path))}.");
    }

    private static FontSource RequiredFont(string path, int collectionIndex)
    {
        if (File.Exists(path)) return new FontSource(path, collectionIndex);
        throw new InvalidOperationException(
            $"The required PDF font is not installed: {Path.GetFileName(path)}.");
    }

    private sealed record FontSource(string Path, int CollectionIndex);
}