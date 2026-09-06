using OpenPdf.Fonts;

namespace NihomeBackend.Services;

internal static class SimplePdfWriter
{
    private const int LinesPerPage = 48;

    internal static void ValidateFonts()
    {
        foreach (var languageCode in new[] { "vi", "ja", "zh" })
        {
            var font = ResolveFont(languageCode);
            _ = TrueTypeFont.Load(font.Path, font.CollectionIndex);
        }
    }

    public static byte[] Create(IEnumerable<string> sourceLines, string languageCode)
    {
        var lines = sourceLines.ToList();
        if (lines.Count == 0) lines.Add(string.Empty);
        var fontSource = ResolveFont(languageCode);

        using var stream = new MemoryStream();
        using var document = OpenPdf.Document.PdfDocument.Create(stream);
        foreach (var pageLines in lines.Chunk(LinesPerPage))
        {
            var page = document.AddPage(595, 842);
            var font = page.AddTrueTypeFont(TrueTypeFont.Load(fontSource.Path, fontSource.CollectionIndex));
            var y = 790d;
            foreach (var line in pageLines)
            {
                page.DrawText(font, 10, 50, y, line);
                y -= 15;
            }
        }
        document.Save();
        return stream.ToArray();
    }

    private static FontSource ResolveFont(string languageCode)
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
                "ja" => RequiredFont(Path.Combine(fonts, "meiryo.ttc"), 0),
                "zh" => RequiredFont(Path.Combine(fonts, "msyh.ttc"), 0),
                _ => RequiredFont(Path.Combine(fonts, "arial.ttf"), 0),
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