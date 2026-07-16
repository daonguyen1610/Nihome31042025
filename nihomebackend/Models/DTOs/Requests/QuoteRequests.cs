using System.ComponentModel.DataAnnotations;

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
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public int SortOrder { get; set; }
}

public class CreateQuoteRequest
{
    [Required]
    public int OpportunityId { get; set; }

    public int? OwnerUserId { get; set; }

    [Required]
    public QuoteMethod Method { get; set; } = QuoteMethod.UnitCost;

    // --- Unit-cost mode (required when Method=UnitCost) ---
    [Range(0, double.MaxValue)]
    public decimal? AreaSqm { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? UnitPricePerSqm { get; set; }

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

public class UpdateQuoteRequest
{
    public int? OwnerUserId { get; set; }

    // Method is immutable after create — spec NIH-93 step 1.
    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }

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

public class QuoteWorkflowRequest
{
    [StringLength(2000)]
    public string? Note { get; set; }
}

public class ExtendQuoteValidityRequest
{
    [Required]
    public DateTime NewValidUntil { get; set; }

    [StringLength(2000)]
    public string? Note { get; set; }
}
