using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class RfqListQuery : IValidatableObject
{
    [MaxLength(200)] public string? Search { get; set; }
    [EnumDataType(typeof(RfqStatus))] public RfqStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int? OwnerUserId { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public bool Overdue { get; set; }
    [RegularExpression("^(updatedAt|dueAt|code)$")] public string SortBy { get; set; } = "updatedAt";
    [RegularExpression("^(asc|desc)$")] public string SortDirection { get; set; } = "desc";
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DueFrom > DueTo) yield return new ValidationResult("DueFrom must not follow DueTo.", [nameof(DueFrom)]);
    }
}

public sealed class RfqUpsertRequest : IConcurrencyRequest
{
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SourceBoqRevisionId { get; set; }
    [Range(1, int.MaxValue)] public int OwnerUserId { get; set; }
    public DateTime DueAt { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<int> VendorIds { get; set; } = [];
    [Required, MinLength(1), MaxLength(500)] public List<RfqLineRequest> Lines { get; set; } = [];
    [Range(typeof(decimal), "0", "100")] public decimal PriceWeight { get; set; } = 50m;
    [Range(typeof(decimal), "0", "100")] public decimal LeadTimeWeight { get; set; } = 20m;
    [Range(typeof(decimal), "0", "100")] public decimal VendorRatingWeight { get; set; } = 20m;
    [Range(typeof(decimal), "0", "100")] public decimal CommercialWeight { get; set; } = 10m;
    public string? RowVersion { get; set; }
}

public sealed class RfqLineRequest
{
    [Range(1, int.MaxValue)] public int ProjectBoqLineId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
}

public sealed class RfqBidRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int VendorId { get; set; }
    [Range(0, 3650)] public int LeadTimeDays { get; set; }
    [Required, MaxLength(1000)] public string PaymentTerms { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
    [Required, MinLength(1), MaxLength(500)] public List<RfqBidLineRequest> Lines { get; set; } = [];
    [Required, MaxLength(20)] public List<long> DocumentIds { get; set; } = [];
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "VND";
    [Range(typeof(decimal), "0.00000001", "999999999999.99999999")] public decimal ExchangeRateToVnd { get; set; } = 1m;
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal FreightAmount { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal DiscountPercent { get; set; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal DiscountAmount { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal VatPercent { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class RfqBidLineRequest
{
    [Range(1, int.MaxValue)] public int RfqLineId { get; set; }
    [Range(typeof(decimal), "0", "999999999999.9999")] public decimal UnitPrice { get; set; }
}

public sealed class RfqAwardRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int BidId { get; set; }
    [Required, MinLength(3), MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [EnumDataType(typeof(ContractType))] public ContractType ContractType { get; set; } = ContractType.Supply;
    public string? RowVersion { get; set; }
}

public sealed class RfqBidEvaluationRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int BidId { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal CommercialScore { get; set; }
    [Required, MinLength(3), MaxLength(2000)] public string Note { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public sealed class RfqBatchAwardRequest : IConcurrencyRequest
{
    [Required, MinLength(3), MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(2000)] public string? OverrideReason { get; set; }
    [Required, MinLength(1), MaxLength(500)] public List<RfqBatchAwardLineRequest> Lines { get; set; } = [];
    public string? RowVersion { get; set; }
}

public sealed class RfqBatchAwardLineRequest
{
    [Range(1, int.MaxValue)] public int BidId { get; set; }
    [Range(1, int.MaxValue)] public int RfqLineId { get; set; }
    [EnumDataType(typeof(ContractType))] public ContractType ContractType { get; set; } = ContractType.Supply;
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
    [MaxLength(100)] public List<RfqMaterialRequestAllocationRequest> MaterialRequestAllocations { get; set; } = [];
}

public sealed class RfqMaterialRequestAllocationRequest
{
    [Range(1, int.MaxValue)] public int MaterialRequestLineId { get; set; }
    [Range(typeof(decimal), "0.000001", "999999999999.999999")] public decimal Quantity { get; set; }
}

public sealed class VendorPortalBidRequest
{
    [Range(0, 3650)] public int LeadTimeDays { get; set; }
    [Required, MaxLength(1000)] public string PaymentTerms { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    [MaxLength(2000)] public string? Note { get; set; }
    [Required, MinLength(1), MaxLength(500)] public List<RfqBidLineRequest> Lines { get; set; } = [];
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "VND";
    [Range(typeof(decimal), "0.00000001", "999999999999.99999999")] public decimal ExchangeRateToVnd { get; set; } = 1m;
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal FreightAmount { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal DiscountPercent { get; set; }
    [Range(typeof(decimal), "0", "99999999999999.9999")] public decimal DiscountAmount { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal VatPercent { get; set; }
}

public sealed class RfqAttachDocumentRequest : IConcurrencyRequest
{
    [Range(1, long.MaxValue)] public long DocumentId { get; set; }
    public string? RowVersion { get; set; }
}
