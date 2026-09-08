namespace NihomeBackend.Models.DTOs.Responses;

public sealed class PaymentRequestResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int? ContractPaymentMilestoneId { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public decimal InvoiceAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public int AssignedAccountantUserId { get; set; }
    public string? AssignedAccountantName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? PaidByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? DecisionReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<PaymentAttachmentResponse> Attachments { get; set; } = [];
    public List<PaymentRequestEventResponse> Events { get; set; } = [];
}

public sealed class PaymentRequestEventResponse
{
    public long Id { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}

public sealed class PaymentAttachmentResponse
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

public sealed class AccountingPeriodResponse
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ClosingAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AccountingCorrectionResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OperationalProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int AccountingPeriodId { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public int SourceEntityId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public decimal OriginalValue { get; set; }
    public decimal CorrectedValue { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int ResponsibleAccountantUserId { get; set; }
    public string? ResponsibleAccountantName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RecordedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? DecisionReason { get; set; }
    public int? ReversalOfCorrectionId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceWorkspaceResponse
{
    public List<PaymentRequestResponse> PaymentRequests { get; set; } = [];
    public List<AccountingPeriodResponse> Periods { get; set; } = [];
    public List<AccountingCorrectionResponse> Corrections { get; set; } = [];
}
// Only identities needed to prepare a payment; no general contract/user data.
public sealed record PaymentReferencesResponse(
    IReadOnlyList<PaymentContractOption> Contracts,
    IReadOnlyList<PaymentVendorOption> Vendors,
    IReadOnlyList<PaymentAccountantOption> Accountants);
public sealed record PaymentContractOption(int Id, string ContractNumber, int VendorId,
    string VendorName, IReadOnlyList<PaymentMilestoneOption> PaymentMilestones);
public sealed record PaymentMilestoneOption(int Id, int Order, string Name);
public sealed record PaymentVendorOption(int Id, string VendorCode, string CompanyName);
public sealed record PaymentAccountantOption(int Id, string? FullName);
