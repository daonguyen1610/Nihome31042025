namespace NihomeBackend.Models.DTOs.Responses;

public static class ProjectReportAvailability
{
    public const string Available = "Available";
    public const string Unavailable = "Unavailable";
}

public static class ProjectReportUnavailableReasons
{
    public const string BaselineIncomplete = "DESIGN_BASELINE_INCOMPLETE";
    public const string SourceNotConfigured = "SOURCE_NOT_CONFIGURED";
    public const string HistoricalSnapshotsUnavailable = "HISTORICAL_SNAPSHOTS_UNAVAILABLE";
    public const string ConstructionWeightsUnavailable = "CONSTRUCTION_WEIGHTS_UNAVAILABLE";
    public const string AcceptanceOutcomeDataUnavailable = "ACCEPTANCE_OUTCOME_DATA_UNAVAILABLE";
    public const string ActualFinanceLedgerUnavailable = "ACTUAL_FINANCE_LEDGER_UNAVAILABLE";
    public const string InventoryLedgerUnavailable = "INVENTORY_LEDGER_UNAVAILABLE";
    public const string BoqUsageDataUnavailable = "BOQ_USAGE_DATA_UNAVAILABLE";
    public const string VendorPerformanceDataUnavailable = "VENDOR_PERFORMANCE_DATA_UNAVAILABLE";
}

public sealed class ProjectReportResponse
{
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime AsOfUtc { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? ProjectId { get; set; }
    public List<ProjectOperationalReportResponse> Projects { get; set; } = new();
}

public sealed class ProjectOperationalReportResponse
{
    public ProjectReportIdentityResponse Project { get; set; } = new();
    public ProjectReportDesignResponse Design { get; set; } = new();
    public ProjectReportConstructionResponse Construction { get; set; } = new();
    public ProjectReportAcceptanceResponse Acceptance { get; set; } = new();
    public ProjectReportPermitResponse Permits { get; set; } = new();
    public ProjectReportContractualFinanceResponse ContractualFinance { get; set; } = new();
    public List<ProjectReportUnavailableMetricResponse> UnavailableMetrics { get; set; } = new();
}

public sealed class ProjectReportIdentityResponse
{
    public int OperationalProjectId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ProjectReportDrillDownResponse DrillDown { get; set; } = new();
}

public sealed class ProjectReportDesignResponse
{
    public string Availability { get; set; } = ProjectReportAvailability.Unavailable;
    public string? ReasonCode { get; set; }
    public decimal? WeightedProgressPercent { get; set; }
    public string RollupPolicyVersion { get; set; } = "design-schedule-weighted-v1";
    public List<ProjectReportDesignSourceResponse> Sources { get; set; } = new();
}

public sealed class ProjectReportDesignSourceResponse
{
    public int PhaseId { get; set; }
    public int Weight { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal WeightedValue { get; set; }
}

public sealed class ProjectReportConstructionResponse
{
    public string Availability { get; set; } = ProjectReportAvailability.Available;
    public string? ReasonCode { get; set; }
    public int TotalCount { get; set; }
    public Dictionary<string, int> CountsByStatus { get; set; } = new();
    public int OverdueCount { get; set; }
    public List<ProjectReportConstructionTaskResponse> OverdueItems { get; set; } = new();
}

public sealed class ProjectReportConstructionTaskResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly PlannedEnd { get; set; }
    public ProjectReportDrillDownResponse DrillDown { get; set; } = new();
}

public sealed class ProjectReportAcceptanceResponse
{
    public string Availability { get; set; } = ProjectReportAvailability.Available;
    public string? ReasonCode { get; set; }
    public int TotalCount { get; set; }
    public Dictionary<string, int> CountsByStatus { get; set; } = new();
    public Dictionary<int, int> CountsByRevision { get; set; } = new();
    public int OverdueCount { get; set; }
}

public sealed class ProjectReportPermitResponse
{
    public string Availability { get; set; } = ProjectReportAvailability.Available;
    public string? ReasonCode { get; set; }
    public int OverdueCount { get; set; }
    public int DueSoonCount { get; set; }
    public int ExpiringCount { get; set; }
    public int DueSoonWindowDays { get; set; } = 30;
    public List<ProjectReportPermitItemResponse> Items { get; set; } = new();
}

public sealed class ProjectReportPermitItemResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string PermitTypeCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? TargetDeadline { get; set; }
    public DateOnly? ExpiresAt { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsDueSoon { get; set; }
    public bool IsExpiring { get; set; }
    public ProjectReportDrillDownResponse DrillDown { get; set; } = new();
}

public sealed class ProjectReportContractualFinanceResponse
{
    public string Availability { get; set; } = ProjectReportAvailability.Available;
    public string Label { get; set; } = "Contractual finance summary";
    public decimal QuoteGrandTotal { get; set; }
    public Dictionary<string, decimal> QuoteTotalsByStatus { get; set; } = new();
    public decimal ContractBaseValue { get; set; }
    public decimal ApprovedVariationOrderDelta { get; set; }
    public decimal ContractCurrentValue { get; set; }
    public string MilestoneLabel { get; set; } = "Contractual schedule";
    public Dictionary<string, decimal> MilestoneScheduledValuesByStatus { get; set; } = new();
}

public sealed class ProjectReportUnavailableMetricResponse
{
    public string MetricCode { get; set; } = string.Empty;
    public string Availability { get; set; } = ProjectReportAvailability.Unavailable;
    public string ReasonCode { get; set; } = string.Empty;
}

public sealed class ProjectReportDrillDownResponse
{
    public string Route { get; set; } = string.Empty;
    public string? Query { get; set; }
}
