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
/// CRM Survey (Phiếu khảo sát) endpoints — NIH-99 ships list + get. The
/// create endpoint is exposed for the sample-data seed path and NIH-100
/// integration tests; update / delete / detail-page workflow will land in
/// NIH-100 / NIH-101.
/// </summary>
[ApiController]
[Route("api/surveys")]
[Route("api/v1/surveys")]
[Authorize]
public class SurveysController(
    ISurveyService svc,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("crm.surveys", "view")]
    public async Task<ActionResult<SurveyListResponse>> List([FromQuery] SurveyListParams parameters, CancellationToken ct)
    {
        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<ActionResult<SurveyResponse>> Get(int id, CancellationToken ct)
    {
        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<ActionResult<SurveyResponse>> Create([FromBody] CreateSurveyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "survey.create",
                ResourceType = EntityTypes.Survey,
                ResourceId = response.Id.ToString(),
                Message = $"Survey #{response.Id} ({response.Code}) created.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (SurveyOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "survey.create",
                ResourceType = EntityTypes.Survey,
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<ActionResult<SurveyResponse>> Update(int id, [FromBody] UpdateSurveyRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "survey.update",
                ResourceType = EntityTypes.Survey,
                ResourceId = id.ToString(),
                Message = $"Survey #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (SurveyOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "survey.update",
                ResourceType = EntityTypes.Survey,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var removed = await svc.DeleteAsync(id, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "survey.delete",
                ResourceType = EntityTypes.Survey,
                ResourceId = id.ToString(),
                Message = $"Survey #{id} deleted.",
            });
            return NoContent();
        }
        catch (SurveyOperationException ex)
        {
            audit.Log(new AuditEvent
            {
                Action = "survey.delete",
                ResourceType = EntityTypes.Survey,
                ResourceId = id.ToString(),
                Message = ex.Message,
                Status = AuditStatus.Failure,
                FailureReason = ex.Message,
            });
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/timeline")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<ActionResult<List<SurveyTimelineEvent>>> Timeline(
        int id, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var events = await svc.GetTimelineAsync(id, limit, ct);
        return events is null ? NotFound() : Ok(events);
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
