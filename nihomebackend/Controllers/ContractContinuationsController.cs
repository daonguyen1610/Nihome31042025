using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Authorization;
using NihomeBackend.Data;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController, Authorize, Route("api/contracts/{id:int}/continuations")]
public sealed class ContractContinuationsController(AppDbContext db, IPermissionService permissions) : ControllerBase
{
    [HttpGet, RequirePermission("crm.contracts", "view")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var all = await permissions.HasAsync(userId, "crm.contracts.view.all", ct);
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && (all || x.OwnerUserId == userId), ct);
        if (contract is null) return NotFound();
        var contracts = await db.Contracts.AsNoTracking().Where(x => (x.SourceContractId == id ||
                x.Id == contract.SourceContractId) && (all || x.OwnerUserId == userId))
            .OrderByDescending(x => x.Id).Take(100)
            .Select(x => new
            {
                x.Id,
                x.ContractNumber,
                x.Status,
                x.ContinuationKind,
                x.ContinuationReason,
                IsSource = x.Id == contract.SourceContractId
            }).ToListAsync(ct);
        var canViewOpportunities = await permissions.HasAsync(userId, "crm.opportunities.view", ct);
        var allOpportunities = await permissions.HasAsync(userId, "crm.opportunities.view.all", ct);
        var opportunities = await db.Opportunities.AsNoTracking().Where(x => canViewOpportunities &&
                x.SourceContractId == id && (allOpportunities || x.OwnerUserId == userId))
            .OrderByDescending(x => x.Id).Take(100)
            .Select(x => new { x.Id, x.Name, x.Stage, x.ContinuationKind, x.ContinuationReason, x.LostReasonCode, x.LostNote }).ToListAsync(ct);
        return Ok(new { contracts, opportunities, kinds = new[] { "Renewal", "Expansion" } });
    }
}
