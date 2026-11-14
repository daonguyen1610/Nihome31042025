using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace NihomeBackend.Services;

public sealed class BoqTemplateSheet
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = [];
    public int FirstRow { get; set; }
    public string SttColumn { get; set; } = string.Empty;
    public string DescriptionColumn { get; set; } = string.Empty;
    // Use only the first line as the item name; the scope text below it stays in the stored file.
    public bool DescriptionFirstLine { get; set; }
    public string UnitColumn { get; set; } = string.Empty;
    public string QuantityColumn { get; set; } = string.Empty;
    public string UnitPriceColumn { get; set; } = string.Empty;
    public string AmountColumn { get; set; } = string.Empty;
    public string SubtotalPrefix { get; set; } = string.Empty;
    public string TotalLabel { get; set; } = string.Empty;
    // A fixed total cell above the rows (GNF), instead of a total label that ends the sheet.
    public string? TotalCell { get; set; }
    // Excel ROUND digits the sheet applies to its total (GNF C4 rounds to 100,000).
    public int? TotalDecimals { get; set; }
    // The layout numbers headings and leaves items unnumbered (GNF): a row is an item when its
    // price or amount cell is filled in and it has a unit or a quantity.
    public bool ItemsWithoutStt { get; set; }
    // Unit-price columns added to the first (ME base: labour and machines; Trọn gói: labour, then machines).
    public List<string> ExtraPriceColumns { get; set; } = [];
    // Text appended to the item name, such as the pipe size in the ME base's PCCC sheet.
    public string? SpecColumn { get; set; }
    // Unnumbered rows with a quantity break down the item above them (count × length × width × height):
    // they are not items or headings, and a price on one is an error.
    public bool MeasurementRows { get; set; }
    // The column measurement rows fill in, when it is not the quantity column (Trọn gói: KL beside KL TỔNG).
    public string? MeasurementColumn { get; set; }
}

public sealed class BoqTemplate
{
    public string Code { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal AmountTolerance { get; set; }
    // Sheets kept in the form but not read, such as the cover and the summary.
    public List<string> IgnoredSheets { get; set; } = [];
    public List<BoqTemplateSheet> Sheets { get; set; } = [];
}

// Each template is an edited copy of a customer workbook; templates.json says how to read it.
public static class ProjectBoqTemplateCatalog
{
    private const string ResourcePrefix = "nihomebackend.Data.BoqTemplates.";
    private static readonly Lazy<IReadOnlyList<BoqTemplate>> Templates = new(Load);

    public static IReadOnlyList<BoqTemplate> All => Templates.Value;

    public static BoqTemplate? Find(string? code) =>
        All.FirstOrDefault(template => string.Equals(template.Code, code, StringComparison.OrdinalIgnoreCase));

    // The download is the stored file itself, so it keeps the customer's exact formatting.
    public static byte[] ReadFile(BoqTemplate template)
    {
        using var stream = Resource(template.File);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    // Templates never change at runtime, so each outline is read once.
    public static IReadOnlyList<BoqOutlineSheet> Outline(BoqTemplate template) =>
        Outlines.GetOrAdd(template.Code, _ => ProjectBoqWorkbookParser.Outline(ReadFile(template), template));

    private static readonly ConcurrentDictionary<string, IReadOnlyList<BoqOutlineSheet>> Outlines = new(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<BoqTemplate> Load()
    {
        using var stream = Resource("templates.json");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("templates").Deserialize<List<BoqTemplate>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static Stream Resource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + name)
        ?? throw new InvalidOperationException($"Missing BOQ template resource {name}.");
}
