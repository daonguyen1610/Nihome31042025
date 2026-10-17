using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>One submitted tender per pursuit, shared by reporting and KPI.</summary>
public static class TenderReporting
{
    public static IQueryable<TenderSubmission> FirstSubmissions(
        IQueryable<TenderSubmission> submissions, DateTime? startUtc = null, DateTime? endUtc = null)
    {
        var query = submissions.Where(item => item.Sequence == 1);
        if (startUtc.HasValue) query = query.Where(item => item.SubmittedAt >= startUtc.Value);
        if (endUtc.HasValue) query = query.Where(item => item.SubmittedAt < endUtc.Value);
        return query;
    }
}
