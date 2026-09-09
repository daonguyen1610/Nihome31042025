namespace NihomeBackend.Models;

public enum RfqStatus { Draft, Issued, UnderEvaluation, Awarded, Closed, Cancelled }

public sealed class Rfq : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int SourceBoqRevisionId { get; set; }
    public ProjectBoqRevision SourceBoqRevision { get; set; } = null!;
    public int OwnerUserId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;
    public DateTime DueAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public decimal PriceWeight { get; set; } = 50m;
    public decimal LeadTimeWeight { get; set; } = 20m;
    public decimal VendorRatingWeight { get; set; } = 20m;
    public decimal CommercialWeight { get; set; } = 10m;
    public RfqStatus Status { get; set; }
    public int? SelectedBidId { get; set; }
    public RfqBid? SelectedBid { get; set; }
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public DateTime? AwardedAt { get; set; }
    public int? AwardedByUserId { get; set; }
    public ApplicationUser? AwardedBy { get; set; }
    public string? AwardReason { get; set; }
    public string? AwardSnapshotJson { get; set; }
    public DateTime? OverdueNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<RfqLine> Lines { get; set; } = [];
    public List<RfqInvitation> Invitations { get; set; } = [];
    public List<RfqBid> Bids { get; set; } = [];
    public List<RfqAward> Awards { get; set; } = [];
    public List<RfqEvent> Events { get; set; } = [];
    public List<RfqDocument> Documents { get; set; } = [];
}

public sealed class RfqLine
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public int ProjectBoqLineId { get; set; }
    public ProjectBoqLine ProjectBoqLine { get; set; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BudgetUnitPrice { get; set; }
}

public sealed class RfqInvitation
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public string VendorName { get; set; } = string.Empty;
    public string? PortalTokenHash { get; set; }
    public DateTime? PortalTokenExpiresAt { get; set; }
    public DateTime? InvitationSentAt { get; set; }
    public string? InvitationDeliveryError { get; set; }
    public DateTime? LastPortalAccessAt { get; set; }
}

// Each submission is a new, immutable revision; withdrawal is recorded separately.
public sealed class RfqBid
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int Revision { get; set; }
    public int LeadTimeDays { get; set; }
    public string PaymentTerms { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    public string? Note { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int SubmittedByUserId { get; set; }
    public ApplicationUser SubmittedBy { get; set; } = null!;
    public DateTime? WithdrawnAt { get; set; }
    public string Currency { get; set; } = "VND";
    public decimal ExchangeRateToVnd { get; set; } = 1m;
    public decimal Subtotal { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatPercent { get; set; }
    public decimal TotalOriginal { get; set; }
    public decimal Total { get; set; }
    public decimal? CommercialScore { get; set; }
    public string? EvaluationNote { get; set; }
    public int? EvaluatedByUserId { get; set; }
    public ApplicationUser? EvaluatedBy { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public bool SubmittedViaPortal { get; set; }
    public List<RfqBidLine> Lines { get; set; } = [];
}

public sealed class RfqBidLine
{
    public int Id { get; set; }
    public int RfqBidId { get; set; }
    public RfqBid RfqBid { get; set; } = null!;
    public int RfqLineId { get; set; }
    public RfqLine RfqLine { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public sealed class RfqEvent
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public ApplicationUser Actor { get; set; } = null!;
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

public sealed class RfqAward
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public int RfqBidId { get; set; }
    public RfqBid RfqBid { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string Currency { get; set; } = "VND";
    public decimal ExchangeRateToVnd { get; set; } = 1m;
    public decimal OriginalValue { get; set; }
    public decimal ValueVnd { get; set; }
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    public int AwardedByUserId { get; set; }
    public ApplicationUser AwardedBy { get; set; } = null!;
    public List<RfqAwardLine> Lines { get; set; } = [];
}

public sealed class RfqAwardLine
{
    public int Id { get; set; }
    public int RfqAwardId { get; set; }
    public RfqAward RfqAward { get; set; } = null!;
    public int RfqLineId { get; set; }
    public RfqLine RfqLine { get; set; } = null!;
    public int RfqBidLineId { get; set; }
    public RfqBidLine RfqBidLine { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal AmountOriginal { get; set; }
    public decimal AmountVnd { get; set; }
    public List<RfqAwardMaterialRequestAllocation> MaterialRequestAllocations { get; set; } = [];
}

public sealed class RfqAwardMaterialRequestAllocation
{
    public int Id { get; set; }
    public int RfqAwardLineId { get; set; }
    public RfqAwardLine RfqAwardLine { get; set; } = null!;
    public int MaterialRequestLineId { get; set; }
    public MaterialRequestLine MaterialRequestLine { get; set; } = null!;
    public decimal Quantity { get; set; }
}

public sealed class RfqDocument
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public Rfq Rfq { get; set; } = null!;
    public int? RfqBidId { get; set; }
    public RfqBid? RfqBid { get; set; }
    public long ProjectDocumentId { get; set; }
    public ProjectDocument ProjectDocument { get; set; } = null!;
}
