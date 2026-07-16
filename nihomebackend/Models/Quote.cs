namespace NihomeBackend.Models;

/// <summary>
/// CRM Quote (báo giá trực tiếp) — a priced proposal against an existing
/// <see cref="Opportunity"/>. Sits between Opportunity and Contract in the
/// sales funnel. Supports two pricing methods: <see cref="QuoteMethod.UnitCost"/>
/// (suất đầu tư: area × unit price) and <see cref="QuoteMethod.Boq"/>
/// (bill of quantities: itemised lines).
/// </summary>
public class Quote
{
    public int Id { get; set; }

    /// <summary>Human-friendly code (e.g. QT-2026-0001) — unique, auto-generated on create.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Owning opportunity — hard FK, quote cannot exist without it.</summary>
    public int OpportunityId { get; set; }
    public Opportunity Opportunity { get; set; } = null!;

    /// <summary>Sales owner. Nullable while unassigned; SetNull on user delete.</summary>
    public int? OwnerUserId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public QuoteMethod Method { get; set; } = QuoteMethod.UnitCost;

    /// <summary>Version number; starts at 1, bumps whenever the quote is edited after Approved.</summary>
    public int Version { get; set; } = 1;

    // --- Unit-cost mode fields (nullable when Method=Boq) ---
    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public string? PackageDescription { get; set; }

    // --- Totals (always populated, derived from Method) ---
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal VatPercent { get; set; } = 8m;
    public decimal GrandTotal { get; set; }

    public QuoteStatus Status { get; set; } = QuoteStatus.Draft;

    /// <summary>Expiry timestamp; when passed and not terminal, quote surfaces as Expired.</summary>
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(30);

    public string? Note { get; set; }

    // --- Workflow timestamps ---
    public DateTime? SubmittedAt { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? SentAt { get; set; }
    public int? SentByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }

    // --- Audit ---
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }

    public List<QuoteItem> Items { get; set; } = new();
    public List<QuoteApprovalLog> ApprovalLogs { get; set; } = new();
    public List<QuoteVersionSnapshot> VersionSnapshots { get; set; } = new();
}

/// <summary>Line item in a Boq quote. Ignored when Method=UnitCost.</summary>
public class QuoteItem
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public string? ItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Immutable audit entry of a workflow action (submit/approve/reject/send/...).</summary>
public class QuoteApprovalLog
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public QuoteWorkflowAction Action { get; set; }
    public QuoteStatus? FromStatus { get; set; }
    public QuoteStatus ToStatus { get; set; }

    public int? ByUserId { get; set; }
    public ApplicationUser? By { get; set; }

    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Frozen snapshot of a quote's headline fields at the moment a new version
/// is spawned (i.e. before an edit-after-approval mutation). Supports the
/// "compare 2 versions" acceptance criterion.
/// </summary>
public class QuoteVersionSnapshot
{
    public int Id { get; set; }
    public int QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public int VersionNumber { get; set; }
    public QuoteMethod Method { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public string? PackageDescription { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal VatPercent { get; set; }
    public decimal GrandTotal { get; set; }

    /// <summary>Serialised list of <see cref="QuoteItem"/> at snapshot time (JSON).</summary>
    public string ItemsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
}

public enum QuoteMethod
{
    UnitCost = 0,
    Boq = 1,
}

public enum QuoteStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    SentToCustomer = 3,
    CustomerApproved = 4,
    Rejected = 5,
    Expired = 6,
    Cancelled = 7,
}

public enum QuoteWorkflowAction
{
    Create = 0,
    Update = 1,
    Submit = 2,
    Approve = 3,
    RejectInternal = 4,
    Send = 5,
    CustomerApprove = 6,
    CustomerReject = 7,
    Cancel = 8,
    ExtendValidity = 9,
    NewVersion = 10,
}
