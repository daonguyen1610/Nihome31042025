using Microsoft.AspNetCore.Mvc;

namespace nihomebackend.tests.Infrastructure;

internal static class ActionResultAssert
{
    public static T Ok<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value);
    }

    public static string OkMessage(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Message(ok.Value);
    }

    public static string Message(object? value)
    {
        var message = value?.GetType().GetProperty("message")?.GetValue(value)?.ToString()
            ?? value?.GetType().GetProperty("Message")?.GetValue(value)?.ToString();

        return Assert.IsType<string>(message);
    }
}
