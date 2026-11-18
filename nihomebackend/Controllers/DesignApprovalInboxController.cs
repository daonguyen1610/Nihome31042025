using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;

namespace NihomeBackend.Controllers;

/// <summary>
/// Only returns work addressed to the caller, so it needs no module permission:
/// internal review recipients can reach it without design.projects access.
/// </summary>
[ApiController]
[Route("api/design-approval-inbox")]
[Route("api/v1/design-approval-inbox")]
[Authorize]
public sealed class DesignApprovalInboxController(DesignApprovalInboxService inbox) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DesignApprovalInboxResponse>> Get(CancellationToken ct)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(value, out var userId)) return Unauthorized();
        return Ok(await inbox.GetAsync(userId, ct));
    }
}
