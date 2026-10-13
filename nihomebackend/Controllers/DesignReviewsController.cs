using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Controllers;

[ApiController]
[Route("api/design-projects/{projectId:int}/reviews")]
[Route("api/v1/design-projects/{projectId:int}/reviews")]
[Authorize]
public sealed class DesignReviewsController(
    IDesignReviewService service,
    IProjectDocumentService documents,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet("recipient-candidates")]
    [RequirePermission("design.projects", "manage")]
    public async Task<ActionResult<IReadOnlyList<DesignReviewRecipientResponse>>> RecipientCandidates(
        int projectId, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            return Ok(await service.ListRecipientCandidatesAsync(projectId, userId.Value, ct));
        }
        catch (DesignReviewOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{reviewId:int}/documents/{documentId:long}/content")]
    public async Task<IActionResult> DownloadDocument(
        int projectId, int reviewId, long documentId, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        var operationalProjectId = await service.ResolveAccessibleDocumentProjectAsync(
            projectId, reviewId, documentId, userId.Value, ct);
        if (!operationalProjectId.HasValue) return NotFound();
        var content = await documents.DownloadAsync(
            operationalProjectId.Value, documentId, userId.Value, canSeeAll: true, ct);
        if (content is null) return NotFound();
        Response.ContentType = content.ContentType;
        Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = content.OriginalFileName,
        }.ToString();
        await content.WriteToAsync(Response.Body, ct);
        return new EmptyResult();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DesignReviewResponse>>> List(int projectId, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        var result = await service.ListAsync(projectId, userId.Value, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("design.projects", "manage")]
    [Idempotency("design.reviews.create", requireKey: true)]
    public async Task<ActionResult<DesignReviewResponse>> Create(
        int projectId, [FromBody] CreateDesignReviewRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.CreateAsync(projectId, request, userId.Value, ct);
            Log("design-review.create", result);
            return Created($"/api/design-projects/{projectId}/reviews", result);
        }
        catch (DesignReviewOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("{reviewId:int}/submit-internal")]
    [RequirePermission("design.projects", "manage")]
    [Idempotency("design.reviews.submit-internal", requireKey: true)]
    public Task<ActionResult<DesignReviewResponse>> SubmitInternal(
        int projectId, int reviewId, [FromBody] SubmitDesignReviewRequest request, CancellationToken ct) =>
        Mutate(projectId, reviewId, request.RowVersion,
            (userId, version) => service.SubmitInternalAsync(projectId, reviewId, version, userId, ct),
            "design-review.submit-internal");

    [HttpPost("{reviewId:int}/comments")]
    [Idempotency("design.reviews.comment", requireKey: true)]
    public async Task<ActionResult<DesignReviewResponse>> Comment(
        int projectId, int reviewId, [FromBody] AddDesignReviewCommentRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await service.AddCommentAsync(projectId, reviewId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Log("design-review.comment", result);
            return Ok(result);
        }
        catch (DesignReviewOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("{reviewId:int}/consolidate")]
    [RequirePermission("design.projects", "manage")]
    [Idempotency("design.reviews.consolidate", requireKey: true)]
    public async Task<ActionResult<DesignReviewResponse>> Consolidate(
        int projectId, int reviewId, [FromBody] ConsolidateDesignReviewRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.ConsolidateAsync(projectId, reviewId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Log("design-review.consolidate", result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (DesignReviewOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPost("{reviewId:int}/submit-client")]
    [RequirePermission("design.projects", "manage")]
    [Idempotency("design.reviews.submit-client", requireKey: true)]
    public Task<ActionResult<DesignReviewResponse>> SubmitClient(
        int projectId, int reviewId, [FromBody] SubmitDesignReviewRequest request, CancellationToken ct) =>
        Mutate(projectId, reviewId, request.RowVersion,
            (userId, version) => service.SubmitClientAsync(projectId, reviewId, version, userId, ct),
            "design-review.submit-client");

    [HttpPost("{reviewId:int}/client-decision")]
    [RequirePermission("design.projects", "manage")]
    [Idempotency("design.reviews.client-decision", requireKey: true)]
    public async Task<ActionResult<DesignReviewResponse>> ClientDecision(
        int projectId, int reviewId, [FromBody] RecordDesignClientDecisionRequest request, CancellationToken ct)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        request.RowVersion = CrmConcurrency.ResolveRequiredRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.RecordClientDecisionAsync(projectId, reviewId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Log("design-review.client-decision", result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (DesignReviewOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private async Task<ActionResult<DesignReviewResponse>> Mutate(
        int projectId, int reviewId, string? token,
        Func<int, string, Task<DesignReviewResponse?>> action, string auditAction)
    {
        var userId = UserId();
        if (userId is null) return Unauthorized();
        var version = CrmConcurrency.ResolveRequiredRequestToken(Request, token);
        try
        {
            var result = await action(userId.Value, version);
            if (result is null) return NotFound();
            Log(auditAction, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (DesignReviewOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private void Log(string action, DesignReviewResponse value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = "DesignReview",
        ResourceId = value.Id.ToString(),
        Message = $"Design review #{value.Id} changed.",
        NewValue = value,
    });

    private int? UserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
