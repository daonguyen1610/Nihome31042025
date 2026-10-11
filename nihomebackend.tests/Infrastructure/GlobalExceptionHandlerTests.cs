using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NihomeBackend.Infrastructure;
using Xunit;

namespace nihomebackend.tests.Infrastructure;

public class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context) Create(bool aborted)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        if (aborted)
        {
            // Kept alive on purpose: the token must stay cancelled for the
            // handler's RequestAborted check.
            var cts = new CancellationTokenSource();
            cts.Cancel();
            context.RequestAborted = cts.Token;
        }

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            Mock.Of<IHostEnvironment>());
        return (handler, context);
    }

    [Fact]
    public async Task TryHandleAsync_SwallowsCancellationException_WhenClientAborted()
    {
        var (handler, context) = Create(aborted: true);

        var handled = await handler.TryHandleAsync(
            context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        // No response reaches a client that already disconnected.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task TryHandleAsync_SwallowsTransientFailureFromAbortedBatch_WhenClientAborted()
    {
        var (handler, context) = Create(aborted: true);
        // EF Core reports an aborted SQL batch as an InvalidOperationException
        // wrapping SqlException 3980 rather than as a cancellation exception.
        // SqlException cannot be constructed outside a live connection, so the
        // inner exception reproduces the message the handler matches on.
        var exception = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure. Consider enabling transient error resiliency by adding 'EnableRetryOnFailure' to the 'UseSqlServer' call.",
            new InvalidOperationException(
                "The request failed to run because the batch is aborted. Operation cancelled by user. Error Number:3980,State:1"));

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task TryHandleAsync_MapsInvalidOperationToBadRequest_WhenClientStillConnected()
    {
        var (handler, context) = Create(aborted: false);

        var handled = await handler.TryHandleAsync(
            context, new InvalidOperationException("Work package is not open."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        var problem = System.Text.Json.JsonSerializer.Deserialize<ProblemDetails>(body);
        Assert.Equal(StatusCodes.Status400BadRequest, problem!.Status);
        Assert.Equal("Invalid operation.", problem.Title);
    }
}
