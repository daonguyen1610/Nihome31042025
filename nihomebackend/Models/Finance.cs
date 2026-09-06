namespace NihomeBackend.Models;

public sealed class PaymentRequest : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; } = null!;
    public int? ContractPaymentMilestoneId { get; set; }
    public ContractPaymentMilestone? ContractPaymentMilestone { get; set; }
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public decimal InvoiceAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public PaymentRequestStatus Status { get; set; } = PaymentRequestStatus.Draft;
    public DateTime ReceivedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public int? ValidatedByUserId { get; set; }
    public ApplicationUser? ValidatedBy { get; set; }
    public int AssignedAccountantUserId { get; set; }
    public ApplicationUser AssignedAccountant { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? PaidByUserId { get; set; }
    public ApplicationUser? PaidBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public ApplicationUser? CancelledBy { get; set; }
    public string? DecisionReason { get; set; }
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<PaymentRequestAttachment> Attachments { get; set; } = [];
    public List<PaymentRequestEvent> Events { get; set; } = [];
}

public sealed class PaymentRequestEvent
{
    public long Id { get; set; }
    public int PaymentRequestId { get; set; }
    public PaymentRequest PaymentRequest { get; set; } = null!;
    public PaymentRequestStatus? FromStatus { get; set; }
    public PaymentRequestStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedByUser { get; set; } = null!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PaymentRequestAttachment
{
    public int Id { get; set; }
    public int PaymentRequestId { get; set; }
    public PaymentRequest PaymentRequest { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AccountingPeriod : IConcurrencyTracked
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public AccountingPeriodStatus Status { get; set; } = AccountingPeriodStatus.Open;
    public DateTime? ClosingAt { get; set; }
    public int? ClosingByUserId { get; set; }
    public ApplicationUser? ClosingBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public ApplicationUser? ClosedBy { get; set; }
    public string? CloseReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<AccountingCorrection> Corrections { get; set; } = [];
}

public sealed class AccountingCorrection : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int AccountingPeriodId { get; set; }
    public AccountingPeriod AccountingPeriod { get; set; } = null!;
    public string SourceEntityType { get; set; } = string.Empty;
    public int SourceEntityId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public decimal OriginalValue { get; set; }
    public decimal CorrectedValue { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public int ResponsibleAccountantUserId { get; set; }
    public ApplicationUser ResponsibleAccountant { get; set; } = null!;
    public AccountingCorrectionStatus Status { get; set; } = AccountingCorrectionStatus.Draft;
    public int RecordedByUserId { get; set; }
    public ApplicationUser RecordedBy { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedByUserId { get; set; }
    public ApplicationUser? RejectedBy { get; set; }
    public string? DecisionReason { get; set; }
    public int? ReversalOfCorrectionId { get; set; }
    public AccountingCorrection? ReversalOfCorrection { get; set; }
    public DateTime? ReversedAt { get; set; }
    public int? ReversedByUserId { get; set; }
    public ApplicationUser? ReversedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public enum PaymentRequestStatus
{
    Draft = 0,
    UnderValidation = 1,
    ReadyForApproval = 2,
    Approved = 3,
    Paid = 4,
    Rejected = 5,
    Cancelled = 6,
}

public enum AccountingPeriodStatus
{
    Open = 0,
    Closing = 1,
    Closed = 2,
}

public enum AccountingCorrectionStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Reversed = 4,
}