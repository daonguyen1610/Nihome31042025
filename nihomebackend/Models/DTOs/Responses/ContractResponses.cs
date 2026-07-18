using NihomeBackend.Models;

namespace NihomeBackend.Models.DTOs.Responses;

public class ContractResponse
{
    public int Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? OpportunityId { get; set; }
    public string? OpportunityTitle { get; set; }

    public int? QuoteId { get; set; }
    public string? QuoteCode { get; set; }

    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }

    public ContractStatus Status { get; set; }

    public DateTime? SignedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public decimal Value { get; set; }

    public string? ScopeOfWork { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ContractPaymentMilestoneResponse> PaymentMilestones { get; set; } = new();
}

public class ContractPaymentMilestoneResponse
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PercentValue { get; set; }
    /// <summary>Server-derived: Contract.Value × PercentValue / 100.</summary>
    public decimal Amount { get; set; }
    public DateTime? DueDate { get; set; }
    public PaymentMilestoneStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ContractListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ContractResponse> Items { get; set; } = new();
}
