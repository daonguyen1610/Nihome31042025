using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>Rechecks team scope before an idempotent assignment attachment upload is replayed.</summary>
public sealed class ProjectAssignmentAttachmentIdempotencyGuard(
    AppDbContext db,
    IProjectAccessService access,
    IPermissionService permissions) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(rawUserId, out var userId)) return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out var projectId) ||
            !int.TryParse(context.Request.RouteValues["assignmentId"]?.ToString(), out var assignmentId))
            return new NotFoundResult();
        int? designProjectId = int.TryParse(context.Request.Query["designProjectId"], out var designId)
            ? designId
            : null;

        if (!await permissions.HasAsync(userId, "operations.projects.view", ct) &&
            (!designProjectId.HasValue ||
             !await permissions.HasAsync(userId, "design.projects.view", ct))) return new ForbidResult();

        if (!await db.OperationalProjectAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.Id == assignmentId && assignment.OperationalProjectId == projectId, ct))
            return new NotFoundResult();

        var operationsManage = await permissions.HasAsync(userId, "operations.projects.manage", ct);
        if (!designProjectId.HasValue)
            return operationsManage && await access.CanManageTeamAsync(userId, projectId, ct)
                ? null : new NotFoundResult();

        if (!await db.DesignProjects.AsNoTracking().AnyAsync(project =>
            project.Id == designProjectId.Value && project.OperationalProjectId == projectId, ct))
            return new NotFoundResult();
        var designManage = await permissions.HasAsync(userId, "design.projects.manage", ct);
        return (operationsManage || designManage) &&
            (await access.CanManageTeamAsync(userId, projectId, ct) ||
             await access.CanManageDesignProjectAsync(userId, designProjectId.Value, ct))
            ? null : new NotFoundResult();
    }
}
