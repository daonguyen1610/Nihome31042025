using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models.DTOs.Requests;
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
    [HttpGet("summary")]
    public async Task<ActionResult<DesignApprovalInboxSummaryResponse>> Summary(CancellationToken ct) =>
        CallerId() is { } userId ? Ok(await inbox.GetSummaryAsync(userId, ct)) : Unauthorized();

    [HttpGet("tasks")]
    public async Task<ActionResult<DesignApprovalInboxPageResponse<DesignApprovalInboxTaskResponse>>> Tasks(
        [FromQuery] DesignApprovalInboxQuery query, CancellationToken ct) =>
        CallerId() is { } userId ? Ok(await inbox.ListTasksAsync(userId, query, ct)) : Unauthorized();

    [HttpGet("reviews")]
    public async Task<ActionResult<DesignApprovalInboxPageResponse<DesignApprovalInboxReviewResponse>>> Reviews(
        [FromQuery] DesignApprovalInboxQuery query, CancellationToken ct)
    {
        if (CallerId() is not { } userId) return Unauthorized();
        try
        {
            return Ok(await inbox.ListReviewsAsync(userId, query, ct));
        }
        catch (DesignApprovalInboxOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? CallerId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
