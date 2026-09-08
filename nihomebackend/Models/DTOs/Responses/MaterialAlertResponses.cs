namespace NihomeBackend.Models.DTOs.Responses;

public sealed class MaterialAlertListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = [];
    public List<MaterialAlertResponse> Items { get; set; } = [];
}

public sealed class MaterialAlertResponse
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string OperationalProjectCode { get; set; } = string.Empty;
    public string OperationalProjectName { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? ProjectBoqLineId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal BoqAllowance { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public decimal OnHandQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public string SourceEntityType { get; set; } = string.Empty;
    public int SourceEntityId { get; set; }
    public int AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int? AcknowledgedByUserId { get; set; }
    public string? AcknowledgedByName { get; set; }
    public string? AcknowledgementNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime LastEvaluatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<MaterialRequestContractContextResponse> Contracts { get; set; } = [];
    public List<MaterialAlertEventResponse> Events { get; set; } = [];
}

public sealed class MaterialAlertEventResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}
