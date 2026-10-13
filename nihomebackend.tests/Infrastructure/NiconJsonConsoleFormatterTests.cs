using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NihomeBackend.Infrastructure.Logging;
using Xunit;

namespace nihomebackend.tests.Infrastructure;

public sealed class NiconJsonConsoleFormatterTests
{
    private static readonly NiconJsonConsoleFormatterOptions Options = new()
    {
        AppName = "NihomeBackend",
        ApiName = "Nihome API",
        MessageType = "application_log",
        SourceRoot = "nihomebackend",
    };

    [Fact]
    public void FormatLogLine_ProducesTheSharedJsonContract()
    {
        var timestamp = new DateTimeOffset(2026, 10, 13, 8, 30, 0, TimeSpan.Zero);
        var properties = new Dictionary<string, object?>
        {
            ["ApiName"] = "must-not-create-a-duplicate-key",
            ["api_name"] = "Users.GetById",
            ["correlation_id"] = "request-123",
            ["message_type"] = "api_request",
            ["UserId"] = 42,
            ["{OriginalFormat}"] = "User {UserId} was not found",
        };

        var line = NiconJsonConsoleFormatter.FormatLogLine(
            "NihomeBackend.Controllers.UsersController",
            LogLevel.Warning,
            "User 42 was not found",
            exception: null,
            properties,
            Options,
            timestamp,
            processId: 314,
            threadName: "managed-7");

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        Assert.Equal("nihomebackend/Controllers/UsersController.cs", root.GetProperty("logger").GetString());
        Assert.Equal(
            "date=\"2026-10-13T08:30:00.0000000+00:00\", process_id=\"314\", User 42 was not found",
            root.GetProperty("message").GetString());
        Assert.Equal("WARNING", root.GetProperty("severity").GetString());
        Assert.Equal("managed-7", root.GetProperty("thread").GetString());

        var logProperties = root.GetProperty("properties");
        Assert.Equal("Users.GetById", logProperties.GetProperty("api_name").GetString());
        Assert.Equal("NihomeBackend", logProperties.GetProperty("app_name").GetString());
        Assert.Equal("request-123", logProperties.GetProperty("correlation_id").GetString());
        Assert.Equal("api_request", logProperties.GetProperty("message_type").GetString());
        Assert.Equal(42, logProperties.GetProperty("user_id").GetInt32());
        Assert.False(logProperties.TryGetProperty("{OriginalFormat}", out _));
        Assert.Equal(1, logProperties.EnumerateObject().Count(property => property.NameEquals("api_name")));
        Assert.Contains("date=\\\"2026-10-13T08:30:00.0000000+00:00\\\"", line);
        Assert.DoesNotContain("\\u0022", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\u002B", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatLogLine_MapsTraceToDebugAndIncludesException()
    {
        var line = NiconJsonConsoleFormatter.FormatLogLine(
            "Microsoft.Hosting.Lifetime",
            LogLevel.Trace,
            "Startup failed",
            new InvalidOperationException("invalid configuration"),
            new Dictionary<string, object?>(),
            Options,
            DateTimeOffset.UnixEpoch,
            processId: 1,
            threadName: "managed-1");

        using var document = JsonDocument.Parse(line);
        Assert.Equal("Microsoft.Hosting.Lifetime", document.RootElement.GetProperty("logger").GetString());
        Assert.Equal("DEBUG", document.RootElement.GetProperty("severity").GetString());
        Assert.Contains("InvalidOperationException: invalid configuration", document.RootElement.GetProperty("message").GetString());
        Assert.Equal("process-1", document.RootElement.GetProperty("properties").GetProperty("correlation_id").GetString());
    }

    [Fact]
    public void FormatLogLine_UsesConfiguredSourcePathOverride()
    {
        var options = new NiconJsonConsoleFormatterOptions
        {
            SourceRoot = "nihomebackend",
            SourcePathOverrides = new Dictionary<string, string>
            {
                ["NihomeBackend.Services.GoogleDrive.ProjectDriveSyncProcessor"] =
                    "nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs",
            },
        };

        var line = NiconJsonConsoleFormatter.FormatLogLine(
            "NihomeBackend.Services.GoogleDrive.ProjectDriveSyncProcessor",
            LogLevel.Information,
            "Sync started",
            exception: null,
            new Dictionary<string, object?>(),
            options,
            DateTimeOffset.UnixEpoch,
            processId: 1,
            threadName: "managed-1");

        using var document = JsonDocument.Parse(line);
        Assert.Equal(
            "nihomebackend/Services/GoogleDrive/ProjectDriveSyncService.cs",
            document.RootElement.GetProperty("logger").GetString());
    }

    [Fact]
    public void FormatLogLine_KeepsPrintablePunctuationReadable()
    {
        var line = NiconJsonConsoleFormatter.FormatLogLine(
            "Microsoft.EntityFrameworkCore.Query",
            LogLevel.Warning,
            "The query uses the 'First' operator and offset +07:00.",
            exception: null,
            new Dictionary<string, object?>(),
            Options,
            DateTimeOffset.UnixEpoch,
            processId: 1,
            threadName: ".NET TP Worker");

        Assert.Contains("the 'First' operator and offset +07:00", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\u0027", line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\u002B", line, StringComparison.OrdinalIgnoreCase);
        JsonDocument.Parse(line).Dispose();
    }

    [Fact]
    public void ResolveCorrelationId_AcceptsSafeHeaderAndRejectsInjectedValue()
    {
        var acceptedContext = new DefaultHttpContext();
        var serverRequestId = acceptedContext.TraceIdentifier;
        acceptedContext.Request.Headers[RequestLoggingMiddleware.CorrelationHeader] = "web-123:request-4";

        var accepted = RequestLoggingMiddleware.ResolveCorrelationId(acceptedContext);

        Assert.Equal("web-123:request-4", accepted);
        Assert.Equal(serverRequestId, acceptedContext.TraceIdentifier);

        var rejectedContext = new DefaultHttpContext();
        rejectedContext.Request.Headers[RequestLoggingMiddleware.CorrelationHeader] = "bad value";

        var rejected = RequestLoggingMiddleware.ResolveCorrelationId(rejectedContext);

        Assert.NotEqual("bad value", rejected);
        Assert.Matches("^[a-f0-9]{32}$", rejected);
    }

    [Fact]
    public async Task RequestMiddleware_PreservesServerRequestIdAndStoresCorrelationSeparately()
    {
        var context = new DefaultHttpContext();
        var serverRequestId = context.TraceIdentifier;
        context.Request.Path = "/api/test";
        context.Request.Headers[RequestLoggingMiddleware.CorrelationHeader] = "frontend-request-123";
        var nextCalled = false;
        var middleware = new RequestLoggingMiddleware(
            next: requestContext =>
            {
                nextCalled = true;
                Assert.Equal(serverRequestId, requestContext.TraceIdentifier);
                Assert.Equal(
                    "frontend-request-123",
                    requestContext.Items[RequestLoggingMiddleware.CorrelationIdItem]);
                return Task.CompletedTask;
            },
            NullLogger<RequestLoggingMiddleware>.Instance,
            Microsoft.Extensions.Options.Options.Create(Options));

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(serverRequestId, context.TraceIdentifier);
    }
}
