using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class OperationalProjectIdempotencyGuard(
    IOperationalProjectService projects,
    IPermissionService permissions,
    IProjectAccessService projectAccess) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(
        HttpContext context,
        CancellationToken ct = default)
    {
        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(rawUserId, out var userId)) return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["id"]?.ToString(), out var projectId))
            return new NotFoundResult();

        // This runs before cached upload responses are replayed. The action's
        // permission check alone would not protect a replay after revocation.
        var operationsManager = await permissions.HasAsync(userId, "operations.projects.manage", ct);
        var designManager = false;
        if (!operationsManager && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(ct);
            designManager = Enum.TryParse<ProjectDocumentSourceModule>(form["sourceModule"].ToString(), true,
                out var module) && module == ProjectDocumentSourceModule.Design &&
                await projectAccess.CanManageDesignDocumentsAsync(userId, projectId, ct);
        }
        if (!operationsManager && !designManager) return new ForbidResult();
        var canSeeAll = await permissions.HasAsync(
            userId, "operations.projects.view.all", ct);
        var project = await projects.GetAsync(projectId, userId, canSeeAll || designManager, ct);
        return project is null ? new NotFoundResult() : null;
    }
}
