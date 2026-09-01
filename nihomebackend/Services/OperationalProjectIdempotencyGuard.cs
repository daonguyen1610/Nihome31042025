using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NihomeBackend.Services;

public sealed class OperationalProjectIdempotencyGuard(
    IOperationalProjectService projects,
    IPermissionService permissions) : IIdempotencyRequestGuard
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

        var canSeeAll = await permissions.HasAsync(
            userId, "operations.projects.view.all", ct);
        var project = await projects.GetAsync(projectId, userId, canSeeAll, ct);
        return project is null ? new NotFoundResult() : null;
    }
}