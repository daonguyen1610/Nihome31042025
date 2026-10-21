using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

/// <summary>Immutable commercial provenance, distinct from amendments to an existing contract.</summary>
public static class ContractContinuationRules
{
    public static async Task ValidateAsync(AppDbContext db, IPermissionService? permissions,
        int? sourceId, string? kind, string? reason, string? version, int customerId,
        int userId, CancellationToken ct)
    {
        if (sourceId is null)
        {
            if (kind is not null || reason is not null || version is not null)
                throw new ContractValidationException("Source contract is required for continuation details.");
            return;
        }
        if (sourceId <= 0 || kind is not ("Renewal" or "Expansion") ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new ContractValidationException("Choose Renewal or Expansion and enter a reason (1–2000 characters).");
        if (permissions is null || !await permissions.HasAsync(userId, "crm.contracts.view", ct))
            throw new ContractValidationException("Source contract is unavailable in your access scope.");
        var all = await permissions.HasAsync(userId, "crm.contracts.view.all", ct);
        var source = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == sourceId &&
            (all || c.OwnerUserId == userId), ct)
            ?? throw new ContractValidationException("Source contract is unavailable in your access scope.");
        CrmConcurrency.EnsureMatches(source.RowVersion, version);
        if (source.Direction != ContractDirection.Upstream || source.CustomerId != customerId || source.SignedDate is null ||
            source.Status is ContractStatus.Draft or ContractStatus.Cancelled)
            throw new ContractValidationException("Source must be a signed, active, paused or completed Upstream contract for the same customer.");
        var resourceId = source.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (await db.HardDeleteOperations.AnyAsync(o => o.ResourceType == "Contract" && o.ResourceId == resourceId &&
            o.Status != HardDeleteOperationStatus.Completed, ct))
            throw new ContractValidationException("Source contract has a pending deletion operation.");
    }
}
