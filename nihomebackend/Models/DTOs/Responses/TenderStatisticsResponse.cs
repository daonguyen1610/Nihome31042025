namespace NihomeBackend.Models.DTOs.Responses;

public class TenderCustomerOptionResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TenderStatisticsResponse
{
    public int SubmittedCount { get; set; }
    public int WonCount { get; set; }
    public int LostCount { get; set; }
    public int PendingCount { get; set; }
    public int CancelledCount { get; set; }
    public decimal? WinRate { get; set; }
    // Scoped records without a first-submission event cannot be assigned to a period.
    public int MissingSubmissionCount { get; set; }
    public DateTime CalculatedAt { get; set; }
}
