namespace NihomeBackend.Models;

/// <summary>Immutable provenance for a confirmed preliminary quotation BOQ import.</summary>
public sealed class QuoteBoqImport
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;
    public int QuoteVersion { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string MappingJson { get; set; } = "[]";
    public string OutcomeJson { get; set; } = "[]";
    public int ImportedItemCount { get; set; }
    public DateTime ImportedAt { get; set; }
    public int ImportedByUserId { get; set; }
    public ApplicationUser ImportedBy { get; set; } = null!;
}
