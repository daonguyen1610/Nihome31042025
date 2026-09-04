using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Authorization;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services;
using NihomeBackend.Services.Audit;
using NihomeBackend.Services.GoogleDrive;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Controllers;

/// <summary>Central NICON project endpoints shared by operational modules.</summary>
[ApiController]
[Route("api/operational-projects")]
[Route("api/v1/operational-projects")]
[Authorize]
public class OperationalProjectsController(
    IOperationalProjectService service,
    IProjectDocumentService documents,
    IPermissionService permissions,
    IAuditLogger audit,
    IGoogleDriveSettingsStore driveSettings) : ControllerBase
{
    [HttpGet("document-categories")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<IReadOnlyList<ProjectDocumentCategoryResponse>>> GetDocumentCategories(
        CancellationToken ct)
    {
        var driveOptions = await driveSettings.GetRuntimeAsync(ct);
        var categories = Enum.GetValues<ProjectDocumentCategory>()
            .Where(category => category != ProjectDocumentCategory.Unclassified)
            .OrderBy(category => (int)category)
            .Select(category => new ProjectDocumentCategoryResponse
            {
                Value = category.ToString(),
                FolderPath = driveOptions.Folders.For(category),
                TranslationKey = $"operationalProjects.documents.category.{category}",
            })
            .ToList();
        return Ok(categories);
    }

    [HttpGet]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<OperationalProjectListResponse>> List(
        [FromQuery] OperationalProjectListParams parameters,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        return Ok(await service.ListAsync(parameters, userId.Value, canSeeAll, ct));
    }

    [HttpGet("{id:int}")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<OperationalProjectResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        var result = await service.GetAsync(id, userId.Value, canSeeAll, ct);
        if (result is null) return NotFound();
        CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
        return Ok(result);
    }

    [HttpGet("{id:int}/timeline")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<IReadOnlyList<OperationalProjectTimelineItemResponse>>> GetTimeline(
        int id,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        var result = await service.GetTimelineAsync(id, userId.Value, canSeeAll, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.create")]
    public async Task<ActionResult<OperationalProjectResponse>> Create(
        [FromBody] CreateOperationalProjectRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        try
        {
            var result = await service.CreateAsync(request, userId.Value, canSeeAll, ct);
            audit.Log(new AuditEvent
            {
                Action = "operational-project.create",
                ResourceType = EntityTypes.OperationalProject,
                ResourceId = result.Id.ToString(),
                Message = $"Operational project #{result.Id} ({result.Code}) created.",
                NewValue = result,
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.update")]
    public async Task<ActionResult<OperationalProjectResponse>> Update(
        int id,
        [FromBody] UpdateOperationalProjectRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateAsync(id, request, userId.Value, canSeeAll, ct);
            if (result is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "operational-project.update",
                ResourceType = EntityTypes.OperationalProject,
                ResourceId = id.ToString(),
                Message = $"Operational project #{id} updated.",
                NewValue = result,
            });
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<IActionResult> Delete(
        int id,
        [FromBody] ConfirmDeletionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.DeleteAsync(
                id,
                request,
                userId.Value,
                canSeeAll,
                ct);
            if (result is null) return NotFound();
            return result.IsComplete ? NoContent() : AcceptedOperation(result);
        }
        catch (OperationalProjectOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DeletionPlanChangedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (HardDeleteOperationConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/deletion-impact")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(
        int id,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var canSeeAll = await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct);
        var impact = await service.GetDeletionImpactAsync(id, userId.Value, canSeeAll, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    [HttpGet("{id:int}/documents")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<IReadOnlyList<ProjectDocumentResponse>>> ListDocuments(int id, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        var result = await documents.ListAsync(id, scope.Value.UserId, scope.Value.CanSeeAll, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency(
        "operations.projects.documents.upload",
        typeof(OperationalProjectIdempotencyGuard))]
    [RequestFormLimits(MultipartBodyLengthLimit = ProjectDocumentStorageService.MultipartBodyLengthLimit)]
    public async Task<ActionResult<ProjectDocumentResponse>> UploadDocument(
        int id, [FromForm] ProjectDocumentUploadRequest request, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        if (await service.GetAsync(id, scope.Value.UserId, scope.Value.CanSeeAll, ct) is null) return NotFound();
        if (!IdempotencyService.IsValidKey(Request.Headers["Idempotency-Key"].FirstOrDefault()))
        {
            return BadRequest(new
            {
                message = "Idempotency-Key là bắt buộc cho tải tệp dự án và không được dài quá 120 ký tự; ví dụ: 550e8400-e29b-41d4-a716-446655440000.",
            });
        }
        try
        {
            var result = await documents.UploadAsync(id, request, scope.Value.UserId, scope.Value.CanSeeAll, ct);
            if (result is null) return NotFound();
            AuditDocument("project-document.upload", id, result.Id, "uploaded", result);
            return CreatedAtAction(nameof(GetDocumentContent), new { id, documentId = result.Id }, result);
        }
        catch (ProjectDocumentValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:int}/documents/{documentId:long}/content")]
    [RequirePermission("operations.projects", "view")]
    public async Task<IActionResult> GetDocumentContent(int id, long documentId, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        var content = await documents.DownloadAsync(id, documentId, scope.Value.UserId, scope.Value.CanSeeAll, ct);
        if (content is null) return NotFound();
        Response.ContentType = content.ContentType;
        Response.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        {
            FileNameStar = content.OriginalFileName,
        }.ToString();
        await content.WriteToAsync(Response.Body, ct);
        return new EmptyResult();
    }

    [HttpDelete("{id:int}/documents/{documentId:long}")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<IActionResult> DeleteDocument(int id, long documentId, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        try
        {
            if (!await documents.DeleteAsync(id, documentId, scope.Value.UserId, scope.Value.CanSeeAll, ct)) return NotFound();
            AuditDocument("project-document.delete_requested", id, documentId, "queued for deletion");
            return NoContent();
        }
        catch (ProjectDocumentValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ProjectDocumentConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("{id:int}/documents/{documentId:long}/retry")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<ActionResult<ProjectDocumentResponse>> RetryDocument(int id, long documentId, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        return await MutateDocumentAsync("project-document.retry", id, documentId,
            () => documents.RetryAsync(id, documentId, scope.Value.UserId, scope.Value.CanSeeAll, ct));
    }

    [HttpPost("{id:int}/documents/{documentId:long}/classify")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<ActionResult<ProjectDocumentResponse>> ClassifyDocument(
        int id, long documentId, [FromBody] ClassifyProjectDocumentRequest request, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        return await MutateDocumentAsync("project-document.classify", id, documentId,
            () => documents.ClassifyAsync(id, documentId, request, scope.Value.UserId, scope.Value.CanSeeAll, ct));
    }

    [HttpPost("{id:int}/documents/{documentId:long}/resolve-conflict")]
    [RequirePermission("operations.projects", "manage")]
    public async Task<ActionResult<ProjectDocumentResponse>> ResolveDocumentConflict(
        int id, long documentId, [FromBody] ResolveProjectDocumentConflictRequest request, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(ct);
        if (scope is null) return Unauthorized();
        return await MutateDocumentAsync("project-document.resolve_conflict", id, documentId,
            () => documents.ResolveConflictAsync(id, documentId, request, scope.Value.UserId, scope.Value.CanSeeAll, ct));
    }

    private async Task<ActionResult<ProjectDocumentResponse>> MutateDocumentAsync(
        string action, int projectId, long documentId, Func<Task<ProjectDocumentResponse?>> mutation)
    {
        try
        {
            var result = await mutation();
            if (result is null) return NotFound();
            AuditDocument(action, projectId, documentId, "updated", result);
            return Ok(result);
        }
        catch (ProjectDocumentValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ProjectDocumentConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    private void AuditDocument(string action, int projectId, long documentId, string verb, object? value = null) =>
        audit.Log(new AuditEvent
        {
            Action = action,
            ResourceType = EntityTypes.ProjectDocument,
            ResourceId = documentId.ToString(),
            Message = $"Project document #{documentId} for operational project #{projectId} {verb}.",
            NewValue = value,
        });

    private async Task<(int UserId, bool CanSeeAll)?> ResolveScopeAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return null;
        return (userId.Value, await permissions.HasAsync(userId.Value, "operations.projects.view.all", ct));
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }

    private IActionResult AcceptedOperation(HardDeleteOperationResult result)
    {
        Response.Headers.Location = Url.Action(
            nameof(HardDeleteOperationsController.GetStatus),
            "HardDeleteOperations",
            new { operationId = result.OperationId })!;
        return StatusCode(StatusCodes.Status202Accepted, result);
    }
}
