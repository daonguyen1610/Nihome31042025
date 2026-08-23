using OpenPdf.Fonts;

namespace NihomeBackend.Services;

internal static class SimplePdfWriter
{
    private const int LinesPerPage = 48;

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
        if (OperatingSystem.IsLinux())
        {
            return languageCode switch
            {
                "ja" => RequiredFont(
                    "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 0),
                "zh" => RequiredFont(
                    "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", 2),
                _ => RequiredFont(
                    "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf", 0),
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

        throw new InvalidOperationException(
            "PDF export is supported only in the configured Linux container or Windows IIS environment.");
    }

    private static FontSource RequiredFont(string path, int collectionIndex)
    {
        if (File.Exists(path)) return new FontSource(path, collectionIndex);
        throw new InvalidOperationException(
            $"The required PDF font is not installed: {Path.GetFileName(path)}.");
    }

    private sealed record FontSource(string Path, int CollectionIndex);
}