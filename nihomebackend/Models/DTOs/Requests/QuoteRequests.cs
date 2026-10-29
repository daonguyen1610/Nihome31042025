using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NihomeBackend.Models.DTOs.Requests;

public class QuoteItemInput
{
    [StringLength(60)]
    public string? ItemCode { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string Unit { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal UnitPrice { get; set; }

    public int SortOrder { get; set; }
}

public class CreateQuoteRequest
{
    /// <summary>Create an unpriced draft for the Procurement handoff.</summary>
    public bool DraftOnly { get; set; }

    [Required]
    public int OpportunityId { get; set; }

    public int? OwnerUserId { get; set; }

    // Basic drafts omit the output method; legacy priced creates default to UnitCost.
    public QuoteMethod? Method { get; set; }

    // --- Unit-cost mode (required when Method=UnitCost) ---
    [Range(0, double.MaxValue)]
    public decimal? AreaSqm { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? UnitPricePerSqm { get; set; }

    public int? MaterialRateCatalogId { get; set; }
    public int? InputPriceOfferId { get; set; }
    public DateOnly? PricingEffectiveDate { get; set; }

    [StringLength(500, MinimumLength = 10)]
    public string? RateOverrideReason { get; set; }

    [StringLength(2000)]
    public string? PackageDescription { get; set; }

    // --- BOQ mode (required when Method=Boq) ---
    public List<QuoteItemInput> Items { get; set; } = new();

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 100)]
    public decimal VatPercent { get; set; } = 8m;

    /// <summary>Optional; defaults to CreatedAt + 30 days when omitted.</summary>
    public DateTime? ValidUntil { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class UpdateQuoteRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    public int? OwnerUserId { get; set; }

    // A basic, unpriced draft selects its output method on the first pricing update.
    // Once priced, the method remains immutable for compatibility with quote versions.
    public QuoteMethod? Method { get; set; }

    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public int? MaterialRateCatalogId { get; set; }
    public int? InputPriceOfferId { get; set; }
    public DateOnly? PricingEffectiveDate { get; set; }

    [StringLength(500, MinimumLength = 10)]
    public string? RateOverrideReason { get; set; }

    [StringLength(2000)]
    public string? PackageDescription { get; set; }

    public List<QuoteItemInput> Items { get; set; } = new();

    [Range(0, 100)]
    public decimal DiscountPercent { get; set; }

    [Range(0, 100)]
    public decimal VatPercent { get; set; } = 8m;

    public DateTime? ValidUntil { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class QuoteWorkflowRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
    public int? ApproverUserId { get; set; }
    public string? PreviewToken { get; set; }
}

public sealed class QuoteApprovalPreviewRequest
{
    [Required] public int? ApproverUserId { get; set; }
    public string? RowVersion { get; set; }
}

public class SendQuoteEmailRequest : QuoteWorkflowRequest
{
    [Required, StringLength(150)]
    public string ToEmail { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(8000)]
    public string Body { get; set; } = string.Empty;

    [RegularExpression("^(vi|en|zh|ja)$")]
    public string LanguageCode { get; set; } = "vi";
}

public class ExtendQuoteValidityRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    public DateTime NewValidUntil { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}
