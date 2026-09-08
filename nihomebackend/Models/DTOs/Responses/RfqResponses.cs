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
    DateTime? AwardedAt, string? AwardedBy, string? AwardReason, string? AwardSnapshotJson);

public sealed record RfqLineResponse(int Id, int ProjectBoqLineId, string ItemCode, string Description,
    string Unit, decimal Quantity, decimal BudgetUnitPrice, decimal? LowestUnitPrice);
public sealed record RfqVendorResponse(int Id, string Name, VendorType Type, bool IsActive);
public sealed record RfqBidResponse(int Id, int VendorId, int Revision, int LeadTimeDays,
    string PaymentTerms, DateTime ValidUntil, string? Note, DateTime SubmittedAt, string SubmittedBy,
    DateTime? WithdrawnAt, bool IsCurrent, bool IsComplete, bool IsEligible, bool IsLowest,
    decimal Total, IReadOnlyList<RfqBidLineResponse> Lines);
public sealed record RfqBidLineResponse(int RfqLineId, decimal UnitPrice, decimal Amount);
public sealed record RfqEventResponse(string Action, string Actor, DateTime At, string? Reason);
public sealed record RfqFileResponse(long Id, int? BidId, string Name);
public sealed record RfqUserOption(int Id, string Name);
public sealed record RfqBoqOption(int Id, int Revision, string Currency, IReadOnlyList<RfqBoqLineOption> Lines);
public sealed record RfqBoqLineOption(int Id, string ItemCode, string Description, string Unit, decimal Quantity);
public sealed record RfqReferenceResponse(IReadOnlyList<RfqBoqOption> Revisions,
    IReadOnlyList<RfqVendorResponse> Vendors, IReadOnlyList<RfqUserOption> Owners,
    IReadOnlyList<RfqFileResponse> Documents);
