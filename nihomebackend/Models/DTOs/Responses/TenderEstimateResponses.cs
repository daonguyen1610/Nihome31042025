namespace NihomeBackend.Models.DTOs.Responses;

public class TenderEstimateRevisionResponse
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal VatPercent { get; set; }
    public decimal CostSubtotal { get; set; }
    public decimal BidSubtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandBidTotal { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceSha256 { get; set; } = string.Empty;
    public int ImportedByUserId { get; set; }
    public DateTime ImportedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? Note { get; set; }
    public List<TenderEstimateLineResponse> Lines { get; set; } = [];
}

public class TenderEstimateLineResponse
{
    public int Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal BidUnitPrice { get; set; }
    public decimal CostAmount { get; set; }
    public decimal BidAmount { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

public class TenderEstimateImportResponse
{
    public TenderEstimateRevisionResponse? Revision { get; set; }
    public List<CsvImportError> Errors { get; set; } = [];
}
