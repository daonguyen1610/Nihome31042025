namespace NihomeBackend.Models.DTOs.Responses;

public class OpportunityActivityResponse
{
    public int Id { get; set; }
    public OpportunityActivityType Type { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OpportunityContractLinkResponse
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int? QuoteId { get; set; }
    public ContractStatus Status { get; set; }
    public DateTime? SignedDate { get; set; }
}

public class OpportunityResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? OperationalProjectId { get; set; }
    public int? DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public decimal EstimatedValue { get; set; }
    public int WinProbability { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public OpportunityStage Stage { get; set; }
    public string? LostReasonCode { get; set; }
    public string? LostNote { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? WonQuoteId { get; set; }
    public int? WonTenderId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<OpportunityActivityResponse> Activities { get; set; } = new();
    public List<OpportunityContractLinkResponse> Contracts { get; set; } = new();
}

public class OpportunityDesignStartResponse
{
    public bool Created { get; set; }
    public DesignProjectResponse DesignProject { get; set; } = new();
}

public class OpportunityListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<OpportunityResponse> Items { get; set; } = new();
}

/// <summary>
/// Grouped shape used by the Kanban pipeline board: opportunities bucketed
/// by <see cref="OpportunityStage"/>, with a running total per column so the
/// UI doesn't have to re-aggregate on the client.
/// </summary>
public class OpportunityPipelineResponse
{
    public List<OpportunityPipelineColumn> Columns { get; set; } = new();
}

public class OpportunityPipelineColumn
{
    public OpportunityStage Stage { get; set; }
    public int Count { get; set; }
    public decimal TotalValue { get; set; }
    public List<OpportunityResponse> Items { get; set; } = new();
}

/// <summary>A salesperson who can own an opportunity (id and name only).</summary>
public class OpportunityOwnerOptionResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// One change recorded against an opportunity. Exposes only what the CRM
/// screen needs, not the network or device details of the full audit log.
/// </summary>
public class OpportunityHistoryItemResponse
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActorName { get; set; }
    public string Status { get; set; } = string.Empty;
}
