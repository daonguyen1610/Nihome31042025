using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Infrastructure.Logging;
using NihomeBackend.Services;

namespace NihomeBackend.Infrastructure;

/// <summary>
/// Single source of truth for converting exceptions thrown anywhere downstream
/// of MVC into a consistent <see cref="ProblemDetails"/> response. Keeps logs
/// useful for debugging by always emitting the full exception with a stable
/// correlation id and the request method + path, while leaving the wire body
/// free of sensitive details for non-server errors.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // A client closing the connection is normal traffic, not a bug. The
        // client is already gone, so no response is written. Cancellation does
        // not always arrive as an OperationCanceledException: when a client
        // supersedes an in-flight query, EF Core surfaces the aborted SQL batch
        // as an InvalidOperationException ("transient failure") wrapping
        // SqlException 3980 "Operation cancelled by user". Matching only the
        // cancellation exception type let that shape fall through to the
        // InvalidOperationException mapping below and answer a departed client
        // with 400 "Invalid operation.", which the SPA then rendered as a page
        // error over still-valid content.
        if (httpContext.RequestAborted.IsCancellationRequested && IsClientAbort(exception))
        {
            return true;
        }

        var (status, title, detail) = MapException(exception);
        var traceId = httpContext.TraceIdentifier;
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["api_name"] = httpContext.Items[RequestLoggingMiddleware.ApiNameItem]?.ToString()
                ?? $"{httpContext.Request.Method} {httpContext.Request.Path}",
            ["correlation_id"] = traceId,
            ["message_type"] = "exception",
        });

        // Log every caught exception with the same shape so the operator can
        // grep by traceId and pivot to the request that produced it.
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception during {Method} {Path}. TraceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Handled domain exception ({Status}) during {Method} {Path}. TraceId={TraceId}",
                status,
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["traceId"] = traceId;
        if (exception is CrmConcurrencyException)
        {
            problem.Extensions["code"] = "crm_concurrency_conflict";
        }
        else if (exception is CrmConcurrencyTokenException)
        {
            problem.Extensions["code"] = "crm_concurrency_token_invalid";
        }
        else if (exception is IdempotencyConflictException)
        {
            problem.Extensions["code"] = "idempotency_key_conflict";
        }

        // Only echo the raw exception message in Development to avoid leaking
        // internals (e.g. SQL fragments) to clients in production.
        if (status >= StatusCodes.Status500InternalServerError && environment.IsDevelopment())
        {
            problem.Extensions["exception"] = exception.GetType().FullName;
            problem.Extensions["debugMessage"] = exception.Message;
        }

        httpContext.Response.StatusCode = status;
        // Pass contentType explicitly — the default WriteAsJsonAsync overload
        // would override Response.ContentType back to "application/json".
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }

    private static (int Status, string Title, string Detail) MapException(Exception exception) =>
        exception switch
        {
            IdempotencyConflictException idempotencyEx => (
                StatusCodes.Status409Conflict,
                "Idempotency key conflict.",
                idempotencyEx.Message),

            CrmConcurrencyException concurrencyEx => (
                StatusCodes.Status409Conflict,
                "Concurrent update conflict.",
                concurrencyEx.Message),

            CrmConcurrencyTokenException tokenEx => (
                StatusCodes.Status400BadRequest,
                "Invalid concurrency token.",
                tokenEx.Message),

            UserServiceException userEx => (
                userEx.Error switch
                {
                    UserServiceError.DuplicatePhoneNumber => StatusCodes.Status409Conflict,
                    UserServiceError.DuplicateEmail => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest,
                },
                userEx.Error switch
                {
                    UserServiceError.DuplicatePhoneNumber => "Conflict.",
                    UserServiceError.DuplicateEmail => "Conflict.",
                    _ => "Bad request.",
                },
                userEx.Message),

            EmailAlreadyRegisteredException emailEx => (
                StatusCodes.Status409Conflict,
                "Email already registered.",
                emailEx.Message),

            // Unique-constraint races that escape the service layer.
            DbUpdateException dbEx when IsUniqueConstraintViolation(dbEx) => (
                StatusCodes.Status409Conflict,
                "Conflict.",
                "The requested change conflicts with existing data."),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized.",
                "Authentication is required to access this resource."),

            KeyNotFoundException knfEx => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                knfEx.Message),

            ArgumentException argEx => (
                StatusCodes.Status400BadRequest,
                "Invalid argument.",
                argEx.Message),

            InvalidOperationException invalidEx => (
                StatusCodes.Status400BadRequest,
                "Invalid operation.",
                invalidEx.Message),

            BadHttpRequestException badEx => (
                badEx.StatusCode,
                ResolveTitle(badEx.StatusCode),
                badEx.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "The server encountered an error while handling the request."),
        };

    /// <summary>
    /// True when the failure is the request's own cancellation rather than a
    /// domain error. Accepts every shape the stack produces for an aborted
    /// request: a cancellation exception, or the aborted SQL batch EF Core
    /// wraps as an invalid operation (SqlException 3980 / "Operation cancelled
    /// by user"). Message matching mirrors <see cref="IsUniqueConstraintViolation"/>
    /// because SqlException cannot be constructed outside a live connection.
    /// </summary>
    private static bool IsClientAbort(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                return true;
            }

            if (current.Message.Contains("Operation cancelled by user", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("Error Number:3980", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // SQL Server unique index = 2601, primary key = 2627.
        var inner = ex.InnerException;
        while (inner != null)
        {
            var msg = inner.Message;
            if (msg.Contains("2601", StringComparison.Ordinal) ||
                msg.Contains("2627", StringComparison.Ordinal) ||
                msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            inner = inner.InnerException;
        }
        return false;
    }

    private static string ResolveTitle(int status) =>
        ((HttpStatusCode)status).ToString();
}
