namespace NihomeBackend.Models;

public class TenderEstimateRevision
{
    public int Id { get; set; }
    public int TenderId { get; set; }
    public Tender Tender { get; set; } = null!;
    public int VersionNumber { get; set; }
    public TenderEstimateRevisionStatus Status { get; set; } = TenderEstimateRevisionStatus.Draft;
    public string Currency { get; set; } = "VND";
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
    public List<TenderEstimateLine> Lines { get; set; } = [];
}

public class TenderEstimateLine
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public TenderEstimateRevision Revision { get; set; } = null!;
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

public enum TenderEstimateRevisionStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
}
