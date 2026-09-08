using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NihomeBackend.Services;

// Recheck scope before returning a cached response after membership changes.
public sealed class RfqIdempotencyGuard(IProjectAccessService access) : IIdempotencyRequestGuard
{
    public async Task<IActionResult?> ValidateAsync(HttpContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var userId))
            return new UnauthorizedResult();
        if (!int.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out var projectId) ||
            !await access.CanViewOperationalProjectAsync(userId, projectId, ct)) return new NotFoundResult();
        return null;
    }
}
