using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/reports/sales-funnel")]
[Route("api/v1/reports/sales-funnel")]
[Authorize]
public sealed class SalesFunnelController(SalesFunnelService funnel, IPermissionService permissions) : ControllerBase
{
    /// <summary>Lead → opportunity → won → signed contract counts for records created in [from, to].</summary>
    [HttpGet]
    [RequirePermission("crm.opportunities", "view")]
    public async Task<ActionResult<SalesFunnelResponse>> Get(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId)) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId, "crm.opportunities.view.all", ct);
        try
        {
            return Ok(await funnel.GetAsync(from, to, userId, canSeeAll, ct));
        }
        catch (SalesFunnelOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
