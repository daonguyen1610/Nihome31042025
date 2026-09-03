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

[ApiController]
[Route("api/operational-projects/{projectId:int}/team")]
[Route("api/v1/operational-projects/{projectId:int}/team")]
[Authorize]
public sealed class OperationalProjectTeamController(
    IProjectTeamService service,
    IProjectAccessService access,
    IAuditLogger audit) : ControllerBase
{
    [HttpGet]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<OperationalProjectTeamResponse>> Get(int projectId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await service.GetAsync(projectId, userId.Value, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("history")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<IReadOnlyList<OperationalProjectTeamHistoryResponse>>> GetHistory(
        int projectId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await service.GetHistoryAsync(projectId, userId.Value, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("candidates")]
    [RequirePermission("operations.projects", "view")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberCandidateResponse>>> GetCandidates(
        int projectId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await service.GetCandidatesAsync(projectId, userId.Value, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("members")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.team.members.create")]
    public async Task<ActionResult<OperationalProjectMemberResponse>> AddMember(
        int projectId,
        [FromBody] UpsertOperationalProjectMemberRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageTeamAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await service.AddMemberAsync(projectId, request, userId.Value, ct);
            Audit("project-team.member.create", projectId, result.Id, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Created($"/api/operational-projects/{projectId}/team", result);
        }
        catch (ProjectTeamOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("members/{memberId:int}")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.team.members.update")]
    public async Task<ActionResult<OperationalProjectMemberResponse>> UpdateMember(
        int projectId,
        int memberId,
        [FromBody] UpsertOperationalProjectMemberRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageTeamAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateMemberAsync(projectId, memberId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("project-team.member.update", projectId, memberId, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (ProjectTeamOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("assignments")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.team.assignments.create")]
    public async Task<ActionResult<OperationalProjectAssignmentResponse>> AddAssignment(
        int projectId,
        [FromBody] UpsertOperationalProjectAssignmentRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageTeamAsync(userId.Value, projectId, ct)) return NotFound();
        try
        {
            var result = await service.AddAssignmentAsync(projectId, request, userId.Value, ct);
            Audit("project-team.assignment.create", projectId, result.Id, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Created($"/api/operational-projects/{projectId}/team", result);
        }
        catch (ProjectTeamOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("assignments/{assignmentId:int}")]
    [RequirePermission("operations.projects", "manage")]
    [Idempotency("operations.projects.team.assignments.update")]
    public async Task<ActionResult<OperationalProjectAssignmentResponse>> UpdateAssignment(
        int projectId,
        int assignmentId,
        [FromBody] UpsertOperationalProjectAssignmentRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await access.CanManageTeamAsync(userId.Value, projectId, ct)) return NotFound();
        request.RowVersion = CrmConcurrency.ResolveRequestToken(Request, request.RowVersion);
        try
        {
            var result = await service.UpdateAssignmentAsync(projectId, assignmentId, request, userId.Value, ct);
            if (result is null) return NotFound();
            Audit("project-team.assignment.update", projectId, assignmentId, result);
            CrmConcurrency.SetResponseEntityTag(Response, result.RowVersion);
            return Ok(result);
        }
        catch (ProjectTeamOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private void Audit(string action, int projectId, int entityId, object value) => audit.Log(new AuditEvent
    {
        Action = action,
        ResourceType = EntityTypes.OperationalProjectTeam,
        ResourceId = $"{projectId}:{entityId}",
        Message = $"Operational project #{projectId} team record #{entityId} changed.",
        NewValue = value,
    });

    private int? GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }
}
