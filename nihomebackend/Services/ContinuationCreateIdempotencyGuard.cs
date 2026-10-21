using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>Recheck source visibility before replaying a cached continuation response.</summary>
public sealed class ContinuationCreateIdempotencyGuard(AppDbContext db, IPermissionService permissions) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return new UnauthorizedResult();
        context.Request.EnableBuffering();
        int? sourceId = null;
        int? opportunityId = null;
        int? quoteId = null;
        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
            if (body.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var property in body.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var id)) continue;
                if (property.Name.Equals("sourceContractId", StringComparison.OrdinalIgnoreCase)) sourceId = id;
                if (property.Name.Equals("opportunityId", StringComparison.OrdinalIgnoreCase)) opportunityId = id;
                if (property.Name.Equals("quoteId", StringComparison.OrdinalIgnoreCase)) quoteId = id;
            }
        }
        catch (JsonException) { return null; }
        finally { context.Request.Body.Position = 0; }
        if (!opportunityId.HasValue && quoteId.HasValue && context.Request.Path.Value!.Contains("/contracts", StringComparison.OrdinalIgnoreCase))
            opportunityId = await db.Quotes.AsNoTracking().Where(x => x.Id == quoteId).Select(x => (int?)x.OpportunityId).SingleOrDefaultAsync(ct);
        if (opportunityId.HasValue && context.Request.Path.Value!.Contains("/contracts", StringComparison.OrdinalIgnoreCase))
        {
            var opportunity = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == opportunityId, ct);
            if (opportunity?.SourceContractId is not null)
            {
                if (!await permissions.HasAsync(userId, "crm.opportunities.view", ct) ||
                    opportunity.OwnerUserId != userId && !await permissions.HasAsync(userId, "crm.opportunities.view.all", ct))
                    return new NotFoundResult();
                sourceId = opportunity.SourceContractId;
            }
        }
        if (!sourceId.HasValue) return null;
        if (!await permissions.HasAsync(userId, "crm.contracts.view", ct)) return new ForbidResult();
        var all = await permissions.HasAsync(userId, "crm.contracts.view.all", ct);
        return await db.Contracts.AsNoTracking().AnyAsync(x => x.Id == sourceId && (all || x.OwnerUserId == userId), ct)
            ? null : new NotFoundResult();
    }
}
