namespace NihomeBackend.Models.DTOs.Responses;

public class QuoteItemResponse
{
    public int Id { get; set; }
    public string? ItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

public class QuoteApprovalLogResponse
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int? ByUserId { get; set; }
    public string? ByUserName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuoteVersionResponse
{
    public int Version { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public string? PackageDescription { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal VatPercent { get; set; }
    public decimal GrandTotal { get; set; }
    public List<QuoteItemResponse> Items { get; set; } = new();
    public DateTime CapturedAt { get; set; }
    public bool IsCurrent { get; set; }
}

public class QuoteResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public string Method { get; set; } = string.Empty;
    public int Version { get; set; }

    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public string? PackageDescription { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal VatPercent { get; set; }
    public decimal GrandTotal { get; set; }
    public string GrandTotalInWords { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    /// <summary>Effective flag — true when Status non-terminal and ValidUntil &lt; now.</summary>
    public bool IsExpired { get; set; }
    public string? Note { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<QuoteItemResponse> Items { get; set; } = new();
    public List<QuoteApprovalLogResponse> ApprovalLogs { get; set; } = new();
}

public class QuoteListItemResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public string? CustomerName { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public int Version { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    public bool IsExpiringSoon { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class QuoteListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<QuoteListItemResponse> Items { get; set; } = new();
}

public class QuoteVersionsResponse
{
    public int QuoteId { get; set; }
    public List<QuoteVersionResponse> Versions { get; set; } = new();
}
