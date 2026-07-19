using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

/// <summary>
/// M2 Drawing Revision endpoints (NIH-117). Slice 1: append-only
/// revision history for BasicDesignDoc and ShopDrawing rows. File
/// upload + diff attaches ship in slice 2.
/// </summary>
[ApiController]
[Route("api/drawing-revisions")]
[Route("api/v1/drawing-revisions")]
[Authorize]
public class DrawingRevisionsController(
    IDrawingRevisionService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("design.revisions", "view")]
    public async Task<ActionResult<DrawingRevisionListResponse>> List(
        [FromQuery] DrawingRevisionListParams parameters, CancellationToken ct)
    {
        try
        {
            var result = await svc.ListAsync(parameters, ct);
            return Ok(result);
        }
        catch (DrawingRevisionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.revisions", "view")]
    public async Task<ActionResult<DrawingRevisionResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    /// <summary>
    /// Append a new revision — auto-numbered, flips the previous latest
    /// to superseded, immutable afterwards. There is intentionally no
    /// PUT / DELETE endpoint (revision history is audit-safe per spec).
    /// </summary>
    [HttpPost]
    [RequirePermission("design.revisions", "manage")]
    public async Task<ActionResult<DrawingRevisionResponse>> Create(
        [FromBody] CreateDrawingRevisionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "drawing-revision.create",
                ResourceType = EntityTypes.DrawingRevision,
                ResourceId = response.Id.ToString(),
                Message = $"Drawing revision R{response.RevisionNumber} created for {response.TargetType} #{response.TargetId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (DrawingRevisionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("diff")]
    [RequirePermission("design.revisions", "view")]
    public async Task<ActionResult<DrawingRevisionDiffResponse>> Diff(
        [FromQuery] DrawingRevisionDiffParams parameters, CancellationToken ct)
    {
        try
        {
            var result = await svc.DiffAsync(parameters.FromId, parameters.ToId, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (DrawingRevisionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
