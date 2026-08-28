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
}

public class OperationalProjectQuoteResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal GrandTotal { get; set; }
}

public class OperationalProjectContractResponse
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
