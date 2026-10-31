using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;

namespace NihomeBackend.Services;

/// <summary>Rechecks team scope before an idempotent write can be replayed.</summary>
public sealed class ProjectTeamIdempotencyGuard(
    AppDbContext db,
    IProjectAccessService access,
    IPermissionService permissions) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        var rawUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!int.TryParse(rawUserId, out var userId)) return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out var projectId))
            return new NotFoundResult();

        int? designProjectId;
        context.Request.EnableBuffering();
        try
        {
            using var body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
            designProjectId = (body.RootElement.TryGetProperty("designProjectId", out var value) ||
                body.RootElement.TryGetProperty("DesignProjectId", out value)) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) ? id : null;
        }
        catch (JsonException)
        {
            return new BadRequestResult();
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        var operationsView = await permissions.HasAsync(userId, "operations.projects.view", ct);
        if (!operationsView && (!designProjectId.HasValue ||
            !await permissions.HasAsync(userId, "design.projects.view", ct))) return new ForbidResult();

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
