using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
            ["api_name"] = "Users.GetById",
            ["correlation_id"] = "request-123",
            ["message_type"] = "api_request",
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
        acceptedContext.Request.Headers[RequestLoggingMiddleware.CorrelationHeader] = "web-123:request-4";

        var accepted = RequestLoggingMiddleware.ResolveCorrelationId(acceptedContext);

        Assert.Equal("web-123:request-4", accepted);

        var rejectedContext = new DefaultHttpContext();
        rejectedContext.Request.Headers[RequestLoggingMiddleware.CorrelationHeader] = "bad value";

        var rejected = RequestLoggingMiddleware.ResolveCorrelationId(rejectedContext);

        Assert.NotEqual("bad value", rejected);
        Assert.Matches("^[a-f0-9]{32}$", rejected);
    }
}
