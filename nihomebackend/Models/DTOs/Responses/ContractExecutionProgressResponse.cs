namespace NihomeBackend.Models.DTOs.Responses;

public sealed record ContractProgressSource(int Id, string Code, string Name, string Status);
public sealed record ContractWorkProgress(int PackageCount, int CancelledPackageCount, int TaskCount,
    int CompletedTaskCount, int VerifiedTaskCount, int CancelledTaskCount, List<ContractProgressSource> Sources);
public sealed record ContractQsProgress(int DossierCount, int ActiveApprovedCount, int PendingReviewCount,
    List<ContractProgressSource> Sources);
public sealed record ContractPaymentProgress(int MilestoneCount, int PaidCount, int RequestedCount,
    decimal ScheduledAmount, decimal PaidScheduledAmount);
public sealed class ContractExecutionProgressResponse
{
    public DateTime CalculatedAt { get; set; }
    public ContractWorkProgress? Work { get; set; }
    public ContractQsProgress? Qs { get; set; }
    public ContractPaymentProgress Payment { get; set; } = new(0, 0, 0, 0, 0);
}
