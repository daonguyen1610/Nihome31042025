using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Services;

namespace NihomeBackend.Authorization;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireTranslationManageForLegacyFieldsAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values
            .OfType<ILegacyLocalizedCategoryRequest>()
            .FirstOrDefault();
        if (request == null || request.NameEn == null && request.NameZh == null && request.NameJa == null)
        {
            await next();
            return;
        }

        var rawUserId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.HttpContext.User.FindFirstValue("uid");
        if (!int.TryParse(rawUserId, out var userId) || userId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();
        var permissions = await permissionService.GetForUserAsync(userId, context.HttpContext.RequestAborted);
        if (!permissions.Contains("content.translations.manage"))
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
