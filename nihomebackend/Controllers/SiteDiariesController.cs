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
/// M4 Site Diary (Nhật ký công trình / NIH-142) endpoints — CRUD on
/// <see cref="Models.SiteDiary"/> plus the Draft → Submitted → Confirmed
/// workflow. Guarded by <c>construction.diary.view</c> /
/// <c>construction.diary.manage</c>; the confirm action needs the
/// stricter <c>construction.diary.confirm</c> permission.
/// </summary>
[ApiController]
[Route("api/site-diaries")]
[Route("api/v1/site-diaries")]
[Authorize]
public class SiteDiariesController(
    ISiteDiaryService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("construction.diary", "view")]
    public async Task<ActionResult<SiteDiaryListResponse>> List(
        [FromQuery] SiteDiaryListParams parameters, CancellationToken ct)
    {
        return Ok(await svc.ListAsync(parameters, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("construction.diary", "view")]
    public async Task<ActionResult<SiteDiaryResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("construction.diary", "manage")]
    public async Task<ActionResult<SiteDiaryResponse>> Create(
        [FromBody] CreateSiteDiaryRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "site-diary.create",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = response.Id.ToString(),
                Message = $"Site diary #{response.Id} created for project {response.DesignProjectId} on {response.DiaryDate:yyyy-MM-dd}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("construction.diary", "manage")]
    public async Task<ActionResult<SiteDiaryResponse>> Update(
        int id, [FromBody] UpdateSiteDiaryRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "site-diary.update",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = id.ToString(),
                Message = $"Site diary #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/submit")]
    [RequirePermission("construction.diary", "manage")]
    public async Task<ActionResult<SiteDiaryResponse>> Submit(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.SubmitAsync(id, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "site-diary.submit",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = id.ToString(),
                Message = $"Site diary #{id} submitted for confirmation.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/confirm")]
    [RequirePermission("construction.diary", "confirm")]
    public async Task<ActionResult<SiteDiaryResponse>> Confirm(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ConfirmAsync(id, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "site-diary.confirm",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = id.ToString(),
                Message = $"Site diary #{id} confirmed.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/reopen")]
    [RequirePermission("construction.diary", "confirm")]
    public async Task<ActionResult<SiteDiaryResponse>> Reopen(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.ReopenAsync(id, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "site-diary.reopen",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = id.ToString(),
                Message = $"Site diary #{id} reopened to Draft.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("construction.diary", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "site-diary.delete",
                ResourceType = EntityTypes.SiteDiary,
                ResourceId = id.ToString(),
                Message = $"Site diary #{id} deleted.",
            });
            return NoContent();
        }
        catch (SiteDiaryOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    [RequirePermission("construction.diary", "manage")]
    public async Task<ActionResult<SiteDiaryBulkDeleteResponse>> BulkDelete(
        [FromBody] BulkDeleteSiteDiariesRequest request, CancellationToken ct)
    {
        try
        {
            var result = await svc.BulkDeleteAsync(request.Ids ?? new List<int>(), ct);
            audit.Log(new AuditEvent
            {
                Action = "site-diary.bulk-delete",
                ResourceType = EntityTypes.SiteDiary,
                Message = $"Bulk delete requested={result.Requested} deleted={result.Deleted} failed={result.Failures.Count}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (SiteDiaryOperationException ex)
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
