using System.Text.Json.Serialization;
using NihomeBackend.Infrastructure.Serialization;

namespace NihomeBackend.Models.DTOs.Responses;

public sealed class MaterialRateCatalogResponse
{
    public int Id { get; init; }
    public MaterialRateCatalogType CatalogType { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int RevisionCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class MaterialRateRevisionResponse
{
    public int Id { get; init; }
    public int CatalogId { get; init; }
    public string CatalogCode { get; init; } = string.Empty;
    public string CatalogName { get; init; } = string.Empty;
    public MaterialRateCatalogType CatalogType { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int Version { get; init; }
    public MaterialRateRevisionStatus Status { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? Note { get; init; }
    public string? DecisionNote { get; init; }
    public DateTime? DecidedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal TotalRatePerSqm { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal TotalAmount { get; init; }
    public List<MaterialRateLineResponse> Lines { get; init; } = [];
}

public sealed class MaterialRateLineResponse
{
    public int Id { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public string MaterialName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal Quantity { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal NormPerSqm { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal UnitRate { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal WastePercent { get; init; }
    [JsonConverter(typeof(DecimalStringJsonConverter))]
    public decimal AmountPerSqm { get; init; }
    public int SortOrder { get; init; }
}

public sealed class MaterialRateImportResponse
{
    public int ImportedCount { get; init; }
    public List<CsvImportError> Errors { get; init; } = [];
}
