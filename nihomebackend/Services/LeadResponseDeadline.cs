using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>Rules for the lead response deadline ("deadline phản hồi" in NICON's CRM requirements).</summary>
public static class LeadResponseDeadline
{
    /// <summary>Statuses where sales still owes the lead a response.</summary>
    public static readonly LeadStatus[] OpenStatuses = [LeadStatus.New, LeadStatus.Contacted, LeadStatus.Interested];

    public static bool IsOverdue(LeadStatus status, DateOnly? dueDate, DateOnly today) =>
        dueDate.HasValue && dueDate.Value < today && OpenStatuses.Contains(status);
}
