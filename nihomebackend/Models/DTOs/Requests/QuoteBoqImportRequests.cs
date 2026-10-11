using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class QuoteBoqSheetMappingInput
{
    [Required, StringLength(100)]
    public string SheetName { get; set; } = string.Empty;

    [Required, RegularExpression("^(Ignored|Summary|Detail)$")]
    public string Classification { get; set; } = "Ignored";

    [Range(1, 1000)] public int? HeaderRow { get; set; }
    [Range(1, 500)] public int? ItemCodeColumn { get; set; }
    [Range(1, 500)] public int? DescriptionColumn { get; set; }
    [Range(1, 500)] public int? UnitColumn { get; set; }
    [Range(1, 500)] public int? QuantityColumn { get; set; }
    [Range(1, 500)] public int? UnitPriceColumn { get; set; }
}

public sealed class ConfirmQuoteBoqImportRequest : IConcurrencyRequest
{
    [Required, StringLength(64, MinimumLength = 64)]
    public string SourceHash { get; set; } = string.Empty;

    [Required]
    public string PreviewToken { get; set; } = string.Empty;

    [Required]
    public string? RowVersion { get; set; }

    [MinLength(1), MaxLength(100)]
    public List<QuoteBoqSheetMappingInput> Sheets { get; set; } = [];
}
