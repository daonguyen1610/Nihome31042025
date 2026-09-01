using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace NihomeBackend.Services;

/// <summary>
/// Resource filter that short-circuits the MVC pipeline (including model
/// validation) when a request carries an Idempotency-Key whose response is
/// already cached for the given scope. The original wire response is replayed
/// verbatim — same status code, same JSON body — so retries are safe even
/// when the second payload would otherwise fail validation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IdempotencyAttribute(string scope, Type? requestGuardType = null)
    : Attribute, IAsyncResourceFilter
{
    public string Scope { get; } = scope;
    public Type? RequestGuardType { get; } = requestGuardType;

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (IdempotencyService.IsValidKey(key))
        {
            if (RequestGuardType is not null)
            {
                var guard = context.HttpContext.RequestServices.GetRequiredService(RequestGuardType)
                    as IIdempotencyRequestGuard
                    ?? throw new InvalidOperationException(
                        $"{RequestGuardType.Name} must implement {nameof(IIdempotencyRequestGuard)}.");
                var rejection = await guard.ValidateAsync(
                    context.HttpContext, context.HttpContext.RequestAborted);
                if (rejection is not null)
                {
                    context.Result = rejection;
                    return;
                }
            }

            var service = context.HttpContext.RequestServices.GetRequiredService<IdempotencyService>();
            var fingerprintService = context.HttpContext.RequestServices.GetRequiredService<FingerprintService>();
            var fingerprint = await fingerprintService.ComputeAsync(
                context.HttpContext.Request, context.HttpContext.RequestAborted);
            var userId = ResolveUserId(context.HttpContext.User);
            var begin = await service.TryBeginAsync(
                Scope, key, fingerprint, userId, context.HttpContext.RequestAborted);

            if (begin == IdempotencyService.BeginResult.Replay)
            {
                var hit = await service.TryGetCachedAsync(
                    Scope, key, fingerprint, userId, context.HttpContext.RequestAborted);
                context.HttpContext.Response.Headers["Idempotency-Replayed"] = "true";
                foreach (var header in hit!.Value.Headers)
                {
                    context.HttpContext.Response.Headers[header.Key] = header.Value;
                }
                if (hit.Value.StatusCode == StatusCodes.Status204NoContent && hit.Value.ResponseJson is null)
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status204NoContent);
                    return;
                }
                context.Result = new ContentResult
                {
                    StatusCode = hit.Value.StatusCode,
                    ContentType = "application/json",
                    Content = hit.Value.ResponseJson ?? "null",
                };
                return;
            }

            if (begin == IdempotencyService.BeginResult.InProgress)
            {
                context.Result = new ConflictObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Request already in progress.",
                    Detail = "Một yêu cầu có cùng Idempotency-Key đang được xử lý.",
                });
                return;
            }

            try
            {
                var executed = await next();
                if ((executed.Exception is null || executed.ExceptionHandled) &&
                    TryGetCacheableResult(executed.Result, out var statusCode, out var value))
                {
                    await service.SaveAsync(
                        Scope, key, fingerprint, userId, statusCode, value,
                        CaptureReplayHeaders(context.HttpContext.Response),
                        context.HttpContext.RequestAborted);
                }
                else
                {
                    await service.AbandonAsync(
                        Scope, key, fingerprint, userId, CancellationToken.None);
                }
            }
            catch
            {
                await service.AbandonAsync(
                    Scope, key, fingerprint, userId, CancellationToken.None);
                throw;
            }
            return;
        }

        await next();
    }

    private static bool TryGetCacheableResult(
        IActionResult? result,
        out int statusCode,
        out object? value)
    {
        switch (result)
        {
            case ObjectResult objectResult when
                (objectResult.StatusCode ?? StatusCodes.Status200OK) is >= 200 and < 300:
                statusCode = objectResult.StatusCode ?? StatusCodes.Status200OK;
                value = objectResult.Value;
                return true;
            case StatusCodeResult statusCodeResult when statusCodeResult.StatusCode is >= 200 and < 300:
                statusCode = statusCodeResult.StatusCode;
                value = null;
                return true;
            case EmptyResult:
                statusCode = StatusCodes.Status200OK;
                value = null;
                return true;
            default:
                statusCode = default;
                value = null;
                return false;
        }
    }

    private static IReadOnlyDictionary<string, string> CaptureReplayHeaders(HttpResponse response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "ETag", "Location" })
        {
            if (response.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                headers[name] = value.ToString();
            }
        }
        return headers;
    }

    private static int? ResolveUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(raw, out var userId) ? userId : null;
    }
}
