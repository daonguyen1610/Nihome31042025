using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NihomeBackend.Services;

namespace NihomeBackend.Authorization;

public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
        {
            return;
        }

        // [AllowAnonymous] on method or class wins over class-level [RequirePermission].
        var allowAnonymous = descriptor.MethodInfo
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true)
                .Length > 0
            || descriptor.ControllerTypeInfo
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true)
                .Length > 0;
        if (allowAnonymous) return;

        var classAttrs = descriptor.ControllerTypeInfo
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>();
        var methodAttrs = descriptor.MethodInfo
            .GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
            .Cast<RequirePermissionAttribute>();

        var requiredCodes = classAttrs.Concat(methodAttrs)
            .Select(a => a.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requiredCodes.Count == 0) return;

        var user = context.HttpContext.User;
        if (user?.Identity is not { IsAuthenticated: true })
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userId = ResolveUserId(user);
        if (userId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissions = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();
        var granted = await permissions.GetForUserAsync(userId, context.HttpContext.RequestAborted);

        foreach (var code in requiredCodes)
        {
            if (!granted.Contains(code))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }

    private static int ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("uid");
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
