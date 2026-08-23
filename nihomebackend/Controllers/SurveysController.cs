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
/// CRM survey endpoints, including private field media, checklist results,
/// durable Drive sync controls, and report export.
/// </summary>
[ApiController]
[Route("api/surveys")]
[Route("api/v1/surveys")]
[Authorize]
public class SurveysController(
    ISurveyService svc,
    ISurveyMediaService mediaSvc,
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

    [HttpPost("{id:int}/media")]
    [RequirePermission("crm.surveys", "manage")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(106 * 1024 * 1024)]
    public async Task<ActionResult<SurveyMediaResponse>> AddMedia(
        int id, [FromForm] SurveyMediaUploadRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var media = await mediaSvc.AddAsync(id, request, userId.Value, ct);
            if (media is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "survey.media.add",
                ResourceType = EntityTypes.Survey,
                ResourceId = id.ToString(),
                Message = $"Media #{media.Id} added to survey #{id}.",
                NewValue = media,
            });
            return CreatedAtAction(nameof(GetMediaContent), new { id, mediaId = media.Id }, media);
        }
        catch (SurveyMediaValidationException exception)
        {
            AuditMediaFailure("survey.media.add", id, exception.Message);
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/media/{mediaId:long}/content")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<IActionResult> GetMediaContent(int id, long mediaId, CancellationToken ct)
    {
        var content = await mediaSvc.GetContentAsync(id, mediaId, ct);
        return content is null
            ? NotFound()
            : PhysicalFile(content.FullPath, content.ContentType, content.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:int}/media/{mediaId:long}")]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<IActionResult> DeleteMedia(int id, long mediaId, CancellationToken ct)
    {
        try
        {
            if (!await mediaSvc.DeleteAsync(id, mediaId, ct)) return NotFound();
            audit.Log("survey.media.remove", EntityTypes.Survey, id.ToString(),
                $"Media #{mediaId} removed from survey #{id}.");
            return NoContent();
        }
        catch (SurveyMediaConflictException exception)
        {
            AuditMediaFailure("survey.media.remove", id, exception.Message);
            return Conflict(new { message = exception.Message });
        }
        catch (SurveyMediaValidationException exception)
        {
            AuditMediaFailure("survey.media.remove", id, exception.Message);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/media/{mediaId:long}/retry-sync")]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<ActionResult<SurveyMediaResponse>> RetryMediaSync(
        int id, long mediaId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var media = await mediaSvc.RetryAsync(id, mediaId, userId.Value, ct);
            if (media is null) return NotFound();
            audit.Log("survey.media.retry", EntityTypes.Survey, id.ToString(),
                $"Media #{mediaId} queued for another Drive sync attempt.");
            return Ok(media);
        }
        catch (SurveyMediaValidationException exception)
        {
            AuditMediaFailure("survey.media.retry", id, exception.Message);
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}/checklist/{resultId:long}")]
    [RequirePermission("crm.surveys", "manage")]
    public async Task<ActionResult<SurveyChecklistResultResponse>> UpdateChecklist(
        int id, long resultId, [FromBody] UpdateSurveyChecklistResultRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await mediaSvc.UpdateChecklistAsync(id, resultId, request, userId.Value, ct);
            if (result is null) return NotFound();
            audit.Log("survey.checklist.update", EntityTypes.Survey, id.ToString(),
                $"Checklist result #{resultId} updated.");
            return Ok(result);
        }
        catch (SurveyMediaValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/sync-log")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<ActionResult<List<SurveySyncLogResponse>>> SyncLog(int id, CancellationToken ct)
    {
        var log = await mediaSvc.GetSyncLogAsync(id, ct);
        return log is null ? NotFound() : Ok(log);
    }

    [HttpGet("drive-connection")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<ActionResult<SurveyDriveConnectionStatusResponse>> DriveConnection(CancellationToken ct)
    {
        return Ok(await mediaSvc.GetDriveConnectionStatusAsync(ct));
    }

    [HttpGet("{id:int}/export.pdf")]
    [RequirePermission("crm.surveys", "view")]
    public async Task<IActionResult> ExportPdf(
        int id, [FromQuery] string lang = "vi", CancellationToken ct = default)
    {
        try
        {
            var pdf = await mediaSvc.ExportPdfAsync(id, lang, ct);
            return pdf is null ? NotFound() : File(pdf, "application/pdf", $"survey-{id}.pdf");
        }
        catch (SurveyMediaValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private void AuditMediaFailure(string action, int surveyId, string message) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = EntityTypes.Survey,
        ResourceId = surveyId.ToString(),
        Message = message,
        Status = AuditStatus.Failure,
        FailureReason = message,
    });
}
