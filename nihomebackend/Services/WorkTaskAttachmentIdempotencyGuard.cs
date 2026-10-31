using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NihomeBackend.Services;

public sealed class WorkTaskAttachmentIdempotencyGuard(
    WorkTaskAttachmentService attachments,
    IPermissionService permissions) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(rawId, out var callerId)) return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["id"]?.ToString(), out var packageId) ||
            !int.TryParse(context.Request.RouteValues["taskId"]?.ToString(), out var taskId))
            return new NotFoundResult();
        if (!await permissions.HasAsync(callerId, "work.packages.view", ct)) return new ForbidResult();
        var canViewAll = await permissions.HasAsync(callerId, "work.packages.view.all", ct);
        return await attachments.CanUploadAsync(packageId, taskId, callerId, canViewAll, ct)
            ? null : new NotFoundResult();
    }
}
