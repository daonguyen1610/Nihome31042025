namespace NihomeBackend.Models.DTOs.Responses;

public class OperationalProjectListItemResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int OpportunityCount { get; set; }
    public int QuoteCount { get; set; }
    public int ContractCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OperationalProjectListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<OperationalProjectListItemResponse> Items { get; set; } = new();
}

public class OperationalProjectResponse : OperationalProjectListItemResponse
{
    public string? Note { get; set; }
    public int? DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OperationalProjectOpportunityResponse> Opportunities { get; set; } = new();
    public List<OperationalProjectQuoteResponse> Quotes { get; set; } = new();
    public List<OperationalProjectContractResponse> Contracts { get; set; } = new();
}

public class OperationalProjectOpportunityResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public int WinProbability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? CustomerName { get; set; }
    public string? OwnerName { get; set; }
    public string? LostReasonCode { get; set; }
}

public class OperationalProjectQuoteResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int Version { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? UnitPricePerSqm { get; set; }
    public string? PackageDescription { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal VatPercent { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsExpired { get; set; }
    public string? Note { get; set; }
    public string? CustomerName { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OperationalProjectQuoteItemResponse> Items { get; set; } = new();
}

public class OperationalProjectQuoteItemResponse
{
    public int Id { get; set; }
    public string? ItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class OperationalProjectContractResponse
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? SignedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ScopeOfWork { get; set; }
    public string? Note { get; set; }
    public string? CustomerName { get; set; }
    public string? OwnerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OperationalProjectContractAttachmentResponse
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OperationalProjectPaymentMilestoneResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PercentValue { get; set; }
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
