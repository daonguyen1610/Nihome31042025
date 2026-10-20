using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class CmPortfolioService(AppDbContext db, IPermissionService permissions)
{
    public async Task<Dictionary<int, CmPortfolioResponse>> GetAsync(
        IReadOnlyCollection<int> projectIds, int userId, CancellationToken ct)
    {
        var grants = await permissions.GetForUserAsync(userId, ct);
        var canQs = grants.Contains("construction.qs.view");
        var canQa = grants.Contains("construction.qa-qc.view");
        var canHse = grants.Contains("construction.hse.view");
        var qs = canQs ? await db.ConstructionQsBatches.AsNoTracking()
            .Where(x => projectIds.Contains(x.OperationalProjectId) && x.Status == ConstructionReviewStatus.CmApproved &&
                x.RecordState == ConstructionQsRecordState.Active && x.IsActiveLineage)
            .Select(x => new
            {
                x.Id,
                x.OperationalProjectId,
                x.Code,
                x.Direction,
                x.Status,
                x.CertifiedAmount,
                x.PeriodFrom,
                x.PeriodTo
            }).ToListAsync(ct) : [];
        var qa = canQa ? await db.QaQcDossiers.AsNoTracking().Where(x => projectIds.Contains(x.OperationalProjectId))
            .Select(x => new { x.Id, x.OperationalProjectId, x.Code, x.Status }).ToListAsync(ct) : [];
        var hse = canHse ? await db.HseViolations.AsNoTracking().Where(x => projectIds.Contains(x.OperationalProjectId))
            .Select(x => new { x.Id, x.OperationalProjectId, x.Code, x.Status }).ToListAsync(ct) : [];
        var result = new Dictionary<int, CmPortfolioResponse>();
        var generated = DateTime.UtcNow;
        foreach (var projectId in projectIds)
        {
            CmPortfolioSection Qs(ConstructionQsDirection direction) => Section(canQs, qs
                .Where(x => x.OperationalProjectId == projectId && x.Direction == direction)
                .Select(x => new CmPortfolioSource(x.Id, x.Code, x.Status.ToString(),
                    $"/admin/construction/bch/qs?projectId={projectId}#qs-{x.Id}",
                    x.CertifiedAmount, x.PeriodFrom, x.PeriodTo)));
            result[projectId] = new(generated, "CurrentState", Qs(ConstructionQsDirection.Upstream),
                Qs(ConstructionQsDirection.Downstream),
                Section(canQa, qa.Where(x => x.OperationalProjectId == projectId).Select(x => new CmPortfolioSource(
                    x.Id, x.Code, x.Status.ToString(), $"/admin/construction/bch/qa-qc?projectId={projectId}#qa-qc-{x.Id}"))),
                Section(canHse, hse.Where(x => x.OperationalProjectId == projectId).Select(x => new CmPortfolioSource(
                    x.Id, x.Code, x.Status.ToString(), $"/admin/construction/hse?projectId={projectId}&violationId={x.Id}"))),
                new("Unavailable", "ACTUAL_FINANCE_LEDGER_UNAVAILABLE", null, []));
        }
        return result;
    }

    private static CmPortfolioSection Section(bool allowed, IEnumerable<CmPortfolioSource> source)
    {
        if (!allowed) return new("Unavailable", "SOURCE_PERMISSION_REQUIRED", null, []);
        var items = source.OrderBy(x => x.Id).ToList();
        return new("Available", null, items.Count, items);
    }
}
