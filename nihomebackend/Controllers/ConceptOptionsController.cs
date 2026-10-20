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

namespace NihomeBackend.Controllers;

/// <summary>Concept option, revision, evidence and decision endpoints.</summary>
[ApiController]
[Route("api/concept-options")]
[Route("api/v1/concept-options")]
[Authorize]
public class ConceptOptionsController(
    IConceptOptionService svc,
    IConceptDossierService dossier,
    IDesignRecordDeletionService deletion,
    IPermissionService permissions,
    IProjectAccessService projectAccess,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet("{id:int}/revisions")]
    [RequirePermission("design.concepts", "view")]
    public async Task<ActionResult<IReadOnlyList<ConceptRevisionResponse>>> Revisions(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: false, ct)) return NotFound();
        return Ok(await dossier.ListAsync(id, ct));
    }

    [HttpPost("{id:int}/revisions")]
    [RequirePermission("design.concepts", "manage")]
    [Idempotency("design.concept.revision", requireKey: true)]
    public Task<ActionResult<ConceptRevisionResponse>> CreateRevision(int id, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.CreateRevisionAsync(id, userId, ct), ct);

    [HttpPost("{id:int}/revisions/{revisionId:int}/artifacts")]
    [RequirePermission("design.concepts", "manage")]
    [Idempotency("design.concept.artifact", requireKey: true)]
    public Task<ActionResult<ConceptRevisionResponse>> AddArtifact(
        int id, int revisionId, [FromBody] AddConceptArtifactRequest request, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.AddArtifactAsync(id, revisionId, request, userId, ct), ct);

    [HttpDelete("{id:int}/revisions/{revisionId:int}/artifacts/{artifactId:int}")]
    [RequirePermission("design.concepts", "manage")]
    public Task<ActionResult<ConceptRevisionResponse>> RemoveArtifact(
        int id, int revisionId, int artifactId, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.RemoveArtifactAsync(id, revisionId, artifactId, userId, ct), ct);

    [HttpPost("{id:int}/revisions/{revisionId:int}/waivers")]
    [RequirePermission("design.concepts", "manage")]
    [Idempotency("design.concept.waiver", requireKey: true)]
    public Task<ActionResult<ConceptRevisionResponse>> Waive(
        int id, int revisionId, [FromBody] WaiveConceptChecklistRequest request, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.WaiveAsync(id, revisionId, request, userId, ct), ct);

    [HttpDelete("{id:int}/revisions/{revisionId:int}/waivers/{kind}")]
    [RequirePermission("design.concepts", "manage")]
    public Task<ActionResult<ConceptRevisionResponse>> RemoveWaiver(
        int id, int revisionId, string kind, CancellationToken ct)
    {
        if (!Enum.TryParse<ConceptArtifactKind>(kind, true, out var parsed) || !Enum.IsDefined(parsed))
            return Task.FromResult<ActionResult<ConceptRevisionResponse>>(BadRequest(new { message = "Hạng mục checklist không hợp lệ." }));
        return ChangeDossier(id, (userId) => dossier.RemoveWaiverAsync(id, revisionId, parsed, userId, ct), ct);
    }

    [HttpPost("{id:int}/revisions/{revisionId:int}/feedback")]
    [RequirePermission("design.concepts", "manage")]
    [Idempotency("design.concept.feedback", requireKey: true)]
    public Task<ActionResult<ConceptRevisionResponse>> Feedback(
        int id, int revisionId, [FromBody] AddConceptFeedbackRequest request, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.AddFeedbackAsync(id, revisionId, request, userId, ct), ct);

    [HttpPost("{id:int}/revisions/{revisionId:int}/feedback/{feedbackId:int}/response")]
    [RequirePermission("design.concepts", "manage")]
    [Idempotency("design.concept.feedback-response", requireKey: true)]
    public Task<ActionResult<ConceptRevisionResponse>> Respond(
        int id, int revisionId, int feedbackId, [FromBody] RespondConceptFeedbackRequest request, CancellationToken ct) =>
        ChangeDossier(id, (userId) => dossier.RespondAsync(id, revisionId, feedbackId, request, userId, ct), ct);

    private async Task<ActionResult<ConceptRevisionResponse>> ChangeDossier(
        int id, Func<int, Task<ConceptRevisionResponse>> action, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: true, ct)) return NotFound();
        try
        {
            var result = await action(userId.Value);
            audit.Log(new AuditEvent
            {
                Action = "concept.dossier.change",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept revision R{result.Number:00} changed by user {userId.Value}.",
                NewValue = result,
            });
            return Ok(result);
        }
        catch (ConceptOptionOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
    [HttpGet]
    [RequirePermission("design.concepts", "view")]
    public async Task<ActionResult<ConceptOptionListResponse>> List(
        [FromQuery] ConceptOptionListParams parameters, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!parameters.DesignProjectId.HasValue)
            return BadRequest(new { message = "DesignProjectId is required." });
        if (!await projectAccess.CanViewDesignProjectAsync(userId.Value, parameters.DesignProjectId.Value, ct))
            return NotFound();

        var result = await svc.ListAsync(parameters, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("design.concepts", "view")]
    public async Task<ActionResult<ConceptOptionResponse>> Get(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: false, ct)) return NotFound();

        var found = await svc.GetAsync(id, ct);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Create(
        [FromBody] CreateConceptOptionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await projectAccess.CanManageDesignProjectAsync(userId.Value, request.DesignProjectId, ct))
            return NotFound();
        try
        {
            var response = await svc.CreateAsync(request, userId.Value, ct);
            audit.Log(new AuditEvent
            {
                Action = "concept.create",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = response.Id.ToString(),
                Message = $"Concept option #{response.Id} created for project {response.DesignProjectId}.",
                NewValue = response,
            });
            return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Update(
        int id, [FromBody] UpdateConceptOptionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: true, ct)) return NotFound();
        try
        {
            var response = await svc.UpdateAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.update",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} updated.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/deletion-impact")]
    [RequirePermission("design.records", "hard-delete")]
    public async Task<ActionResult<DeletionImpactResponse>> GetDeletionImpact(int id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: false, ct)) return NotFound();
        var impact = await deletion.GetImpactAsync(DesignProjectResourceType.ConceptOption, id, ct);
        return impact is null ? NotFound() : Ok(impact);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("design.records", "hard-delete")]
    public async Task<IActionResult> Delete(
        int id, [FromBody] ConfirmDeletionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await CanAccessAsync(userId.Value, id, manage: false, ct)) return NotFound();

        try
        {
            var removed = await deletion.DeleteAsync(
                DesignProjectResourceType.ConceptOption, id, request, ct);
            if (!removed) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.delete",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} deleted.",
            });
            return NoContent();
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DesignRecordDeletionException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DeletionPlanChangedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Move the option to a new status. When the target is Finalized the
    /// server discards sibling options + unlocks the design project's Basic
    /// Design stage; permission-gated on the dedicated
    /// <c>design.concepts.finalize</c> code to match the RBAC catalogue.
    /// Baseline <c>manage</c> gate keeps the route out of read-only roles.
    /// </summary>
    [HttpPost("{id:int}/status")]
    [RequirePermission("design.concepts", "manage")]
    public async Task<ActionResult<ConceptOptionResponse>> Transition(
        int id, [FromBody] TransitionConceptOptionStatusRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var projectId = await projectAccess.ResolveDesignProjectIdAsync(
            DesignProjectResourceType.ConceptOption, id, ct);
        if (!projectId.HasValue ||
            !await projectAccess.CanManageDesignProjectAsync(userId.Value, projectId.Value, ct))
            return NotFound();

        // Finalize is a stricter permission on top of manage.
        if (string.Equals(request.Status, "Finalized", StringComparison.OrdinalIgnoreCase) &&
            !await projectAccess.CanApproveDesignProjectAsync(userId.Value, projectId.Value, ct))
        {
            return NotFound();
        }
        if (string.Equals(request.Status, "Finalized", StringComparison.OrdinalIgnoreCase) &&
            !await permissions.HasAsync(userId.Value, "design.concepts.finalize", ct))
        {
            return Forbid();
        }

        try
        {
            var response = await svc.TransitionStatusAsync(id, request, userId.Value, ct);
            if (response is null) return NotFound();
            audit.Log(new AuditEvent
            {
                Action = "concept.transition",
                ResourceType = EntityTypes.ConceptOption,
                ResourceId = id.ToString(),
                Message = $"Concept option #{id} transitioned to {response.Status}.",
                NewValue = response,
            });
            return Ok(response);
        }
        catch (ConceptOptionOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> CanAccessAsync(int userId, int id, bool manage, CancellationToken ct)
    {
        var projectId = await projectAccess.ResolveDesignProjectIdAsync(
            DesignProjectResourceType.ConceptOption, id, ct);
        if (!projectId.HasValue) return false;

        return manage
            ? await projectAccess.CanManageDesignProjectAsync(userId, projectId.Value, ct)
            : await projectAccess.CanViewDesignProjectAsync(userId, projectId.Value, ct);
    }
}
