namespace NihomeBackend.Models.DTOs.Responses;

public sealed class QuoteBoqImportPreviewResponse
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string PreviewToken { get; set; } = string.Empty;
    public List<QuoteBoqSheetPreviewResponse> Sheets { get; set; } = [];
    public int SuggestedItemCount { get; set; }
    public List<string> WorkbookWarnings { get; set; } = [];
}

public sealed class QuoteBoqSheetPreviewResponse
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public string SuggestedClassification { get; set; } = "Ignored";
    public int? HeaderRow { get; set; }
    public int? ItemCodeColumn { get; set; }
    public int? DescriptionColumn { get; set; }
    public int? UnitColumn { get; set; }
    public int? QuantityColumn { get; set; }
    public int? UnitPriceColumn { get; set; }
    public int SuggestedItemCount { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class QuoteBoqImportResponse
{
    public int ImportId { get; set; }
    public int QuoteId { get; set; }
    public int QuoteVersion { get; set; }
    public int ImportedItemCount { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public QuoteResponse Quote { get; set; } = new();
}
