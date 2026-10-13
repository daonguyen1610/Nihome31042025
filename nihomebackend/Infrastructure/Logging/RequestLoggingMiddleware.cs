using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace NihomeBackend.Infrastructure.Logging;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger,
    IOptions<NiconJsonConsoleFormatterOptions> options)
{
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string ApiNameItem = "Nicon.Logging.ApiName";
    public const string CorrelationIdItem = "Nicon.Logging.CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdItem] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        var apiName = ResolveApiName(context);
        context.Items[ApiNameItem] = apiName;
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["api_name"] = apiName,
            ["app_name"] = options.Value.AppName,
            ["correlation_id"] = correlationId,
            ["message_type"] = "api_request",
        });

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        logger.LogDebug(
            "HTTP {Method} {Path}{QueryString} started",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);

        var completed = false;
        try
        {
            await next(context);
            completed = true;
        }
        finally
        {
            stopwatch.Stop();
            logger.LogDebug(
                "HTTP {Method} {Path} finished {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path,
                completed ? context.Response.StatusCode : StatusCodes.Status500InternalServerError,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));
        }
    }

    internal static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        if (IsValidCorrelationId(supplied))
        {
            return supplied!;
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 80
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or ':');

    private static string ResolveApiName(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        return action is null
            ? $"{context.Request.Method} {context.Request.Path}"
            : $"{action.ControllerName}.{action.ActionName}";
    }
}
