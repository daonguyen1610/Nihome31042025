namespace NihomeBackend.Models.DTOs.Responses;

public sealed record RfqListResponse(int Total, int Page, int PageSize, IReadOnlyList<RfqListItemResponse> Items);
public sealed record RfqListItemResponse(int Id, int OperationalProjectId, string Code, string Title,
    string ProjectCode, string ProjectName, string CustomerName, int SourceBoqRevisionId, int BoqRevision,
    RfqStatus Status, int OwnerUserId, string OwnerName, int InvitedCount, int ReceivedCount,
    DateTime? IssuedAt, DateTime DueAt, DateTime UpdatedAt, bool Overdue, string RowVersion);

public sealed record RfqDetailResponse(RfqListItemResponse Header, string Currency, string? Note,
    IReadOnlyList<RfqLineResponse> Lines, IReadOnlyList<RfqVendorResponse> Vendors,
    IReadOnlyList<RfqBidResponse> Bids, IReadOnlyList<RfqEventResponse> Events,
    IReadOnlyList<RfqFileResponse> Documents, int? SelectedBidId, int? ContractId, string? ContractNumber,
    DateTime? AwardedAt, string? AwardedBy, string? AwardReason, string? AwardSnapshotJson,
    RfqScoringResponse? Scoring = null, IReadOnlyList<RfqAwardResponse>? Awards = null,
    IReadOnlyList<RfqMaterialRequestOptionResponse>? MaterialRequests = null);

public sealed record RfqLineResponse(int Id, int ProjectBoqLineId, string ItemCode, string Description,
    string Unit, decimal Quantity, decimal BudgetUnitPrice, decimal? LowestUnitPrice);
public sealed record RfqVendorResponse(int Id, string Name, VendorType Type, bool IsActive,
    bool PortalEnabled = false, DateTime? InvitationSentAt = null, string? InvitationDeliveryError = null);
public sealed record RfqBidResponse(int Id, int VendorId, int Revision, int LeadTimeDays,
    string PaymentTerms, DateTime ValidUntil, string? Note, DateTime SubmittedAt, string SubmittedBy,
    DateTime? WithdrawnAt, bool IsCurrent, bool IsComplete, bool IsEligible, bool IsLowest,
    decimal Total, IReadOnlyList<RfqBidLineResponse> Lines, string Currency = "VND",
    decimal ExchangeRateToVnd = 1m, decimal Subtotal = 0m, decimal FreightAmount = 0m,
    decimal DiscountPercent = 0m, decimal DiscountAmount = 0m, decimal VatPercent = 0m,
    decimal TotalOriginal = 0m, decimal? PriceScore = null, decimal? LeadTimeScore = null,
    decimal? VendorRatingScore = null, decimal? CommercialScore = null, decimal? WeightedScore = null,
    string? EvaluationNote = null, bool SubmittedViaPortal = false);
public sealed record RfqBidLineResponse(int RfqLineId, decimal UnitPrice, decimal Amount,
    decimal UnitPriceVnd = 0m, decimal AmountVnd = 0m, decimal? PriceScore = null,
    decimal? LeadTimeScore = null, decimal? VendorRatingScore = null, decimal? WeightedScore = null);
public sealed record RfqEventResponse(string Action, string Actor, DateTime At, string? Reason);
public sealed record RfqFileResponse(long Id, int? BidId, string Name);
public sealed record RfqUserOption(int Id, string Name);
public sealed record RfqBoqOption(int Id, int Revision, string Currency, IReadOnlyList<RfqBoqLineOption> Lines);
public sealed record RfqBoqLineOption(int Id, string ItemCode, string Description, string Unit, decimal Quantity);
public sealed record RfqReferenceResponse(IReadOnlyList<RfqBoqOption> Revisions,
    IReadOnlyList<RfqVendorResponse> Vendors, IReadOnlyList<RfqUserOption> Owners,
    IReadOnlyList<RfqFileResponse> Documents);

public sealed record RfqScoringResponse(decimal PriceWeight, decimal LeadTimeWeight,
    decimal VendorRatingWeight, decimal CommercialWeight);
public sealed record RfqAwardResponse(int Id, int BidId, int VendorId, string VendorName,
    int ContractId, string ContractNumber, string Currency, decimal ExchangeRateToVnd,
    decimal OriginalValue, decimal ValueVnd, DateTime AwardedAt,
    IReadOnlyList<RfqAwardLineResponse> Lines);
public sealed record RfqAwardLineResponse(int RfqLineId, decimal Quantity, decimal UnitPrice,
    decimal AmountOriginal, decimal AmountVnd,
    IReadOnlyList<RfqMaterialRequestAllocationResponse> MaterialRequestAllocations);
public sealed record RfqMaterialRequestAllocationResponse(int MaterialRequestLineId,
    string MaterialRequestCode, decimal Quantity);
public sealed record RfqMaterialRequestOptionResponse(int LineId, int MaterialRequestId,
    string MaterialRequestCode, int ProjectBoqLineId, decimal RequestedQuantity,
    decimal AlreadyAllocatedQuantity, decimal RemainingQuantity);
public sealed record VendorPortalRfqResponse(string Code, string Title, DateTime DueAt,
    string VendorName, IReadOnlyList<VendorPortalRfqLineResponse> Lines,
    IReadOnlyList<VendorPortalRfqDocumentResponse> Documents, bool CanSubmit);
public sealed record VendorPortalRfqLineResponse(int Id, string ItemCode, string Description,
    string Unit, decimal Quantity);
public sealed record VendorPortalRfqDocumentResponse(long Id, string Name);
