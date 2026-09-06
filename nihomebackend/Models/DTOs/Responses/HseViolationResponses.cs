namespace NihomeBackend.Models.DTOs.Responses;

public sealed class HseViolationListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<HseViolationResponse> Items { get; set; } = [];
    public Dictionary<string, int> StatusCounts { get; set; } = [];
}

public sealed class HseViolationResponse
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public string OperationalProjectCode { get; set; } = string.Empty;
    public string OperationalProjectName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string OfflineClientId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RegulatoryReference { get; set; }
    public List<string> EvidenceDocuments { get; set; } = [];
    public int ResponsibleSiteUserId { get; set; }
    public string? ResponsibleSiteUserName { get; set; }
    public int? RemediationOwnerUserId { get; set; }
    public string? RemediationOwnerUserName { get; set; }
    public DateTime? RemediationDeadline { get; set; }
    public string? RemediationNote { get; set; }
    public string? PenaltyReference { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ReportedAt { get; set; }
    public int? ReportedByUserId { get; set; }
    public string? ReportedByName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? ClosedByName { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<HseViolationEventResponse> Events { get; set; } = [];
}

public sealed class HseViolationEventResponse
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}