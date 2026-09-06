using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class PaymentRequestUpsertRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int ContractId { get; set; }
    [Range(1, int.MaxValue)] public int VendorId { get; set; }
    [Range(1, int.MaxValue)] public int? ContractPaymentMilestoneId { get; set; }
    [Required, MaxLength(120)] public string SupplierInvoiceNumber { get; set; } = string.Empty;
    [Required] public DateOnly? InvoiceDate { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] public decimal InvoiceAmount { get; set; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "VND";
    [Required] public DateTime? ReceivedAt { get; set; }
    [Range(1, int.MaxValue)] public int AssignedAccountantUserId { get; set; }
    [MaxLength(20)] public List<PaymentAttachmentRequest> Attachments { get; set; } = [];
    public string? RowVersion { get; set; }
}

public sealed class PaymentAttachmentRequest
{
    [Required, MaxLength(260)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string FilePath { get; set; } = string.Empty;
}

public sealed class FinanceTransitionRequest : IConcurrencyRequest
{
    [MaxLength(2000)] public string? Reason { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class FinanceDecisionRequest : IConcurrencyRequest
{
    public bool Approved { get; set; }
    [MaxLength(2000)] public string? Reason { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class AccountingPeriodCreateRequest
{
    [Range(2000, 9999)] public int Year { get; set; }
    [Range(1, 12)] public int Month { get; set; }
}

public sealed class AccountingCorrectionUpsertRequest : IConcurrencyRequest
{
    [Range(1, int.MaxValue)] public int OperationalProjectId { get; set; }
    [Range(1, int.MaxValue)] public int AccountingPeriodId { get; set; }
    [Required, MaxLength(100), RegularExpression("^[A-Za-z][A-Za-z0-9]*$")] public string SourceEntityType { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SourceEntityId { get; set; }
    [Required, MaxLength(80), RegularExpression("^[A-Z][A-Z0-9_]*$")] public string ReasonCode { get; set; } = string.Empty;
    [Range(typeof(decimal), "-9999999999999999.99", "9999999999999999.99")] public decimal OriginalValue { get; set; }
    [Range(typeof(decimal), "-9999999999999999.99", "9999999999999999.99")] public decimal CorrectedValue { get; set; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string Currency { get; set; } = "VND";
    [MaxLength(4000)] public string? Note { get; set; }
    [Range(1, int.MaxValue)] public int ResponsibleAccountantUserId { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class AccountingCorrectionReversalRequest : IConcurrencyRequest
{
    [Required, MinLength(3), MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}