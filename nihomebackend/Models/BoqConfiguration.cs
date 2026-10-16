using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

// This describes entry sheets, not the commercial lifecycle of a BOQ.
public sealed class BoqConfiguration
{
    public int Version { get; set; } = 1;
    public string? SourceFile { get; set; }
    public List<BoqEntrySheet> Sheets { get; set; } = [];
}

public sealed class BoqEntrySheet
{
    public string Key { get; set; } = string.Empty;
    public string NameVi { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameJa { get; set; } = string.Empty;
    public string? SourceRegion { get; set; }
    public bool IsPriced { get; set; } = true;
    public List<BoqEntryField> Fields { get; set; } = [];
}

public sealed class BoqEntryField
{
    public string Key { get; set; } = string.Empty;
    public string NameVi { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameJa { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
    // Code, Name, Unit, Quantity, UnitPrice, or None. No executable formulas.
    public string Binding { get; set; } = "None";
    public bool Required { get; set; }
    public int MaxLength { get; set; } = 300;
    public int DecimalPlaces { get; set; } = 2;
}

public sealed class BoqEntryRow
{
    public string SheetKey { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = [];
}

public sealed class ConfigureBoqRequest
{
    [Required] public DateTime? ExpectedUpdatedAt { get; set; }
    [Required] public BoqConfiguration Configuration { get; set; } = new();
}

public sealed class SaveBoqRowsRequest
{
    [Required] public DateTime? ExpectedUpdatedAt { get; set; }
    [Required] public string ConfigurationHash { get; set; } = string.Empty;
    [Required] public List<BoqEntryRow> Rows { get; set; } = [];
}
