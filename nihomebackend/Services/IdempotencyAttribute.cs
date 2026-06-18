using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NihomeBackend.Services;

/// <summary>
/// Resource filter that short-circuits the MVC pipeline (including model
/// validation) when a request carries an Idempotency-Key whose response is
/// already cached for the given scope. The original wire response is replayed
/// verbatim — same status code, same JSON body — so retries are safe even
/// when the second payload would otherwise fail validation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IdempotencyAttribute(string scope) : Attribute, IAsyncResourceFilter
{
    public string Scope { get; } = scope;

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (IdempotencyService.IsValidKey(key))
        {
            var service = context.HttpContext.RequestServices.GetRequiredService<IdempotencyService>();
            var cached = await service.TryGetCachedAsync(Scope, key, context.HttpContext.RequestAborted);
            if (cached is { } hit)
            {
                context.Result = new ContentResult
                {
                    StatusCode = hit.StatusCode,
                    ContentType = "application/json",
                    Content = hit.ResponseJson ?? "null",
                };
                return;
            }
        }

        await next();
    }
}
