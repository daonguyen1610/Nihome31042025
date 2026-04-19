using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace nihomebackend.tests.Infrastructure;

internal sealed class ScenarioLogSink
{
    private static readonly object Gate = new();

    public ScenarioLogSink(string scenarioName)
    {
        ScenarioName = scenarioName;
    }

    public string ScenarioName { get; }

    public void Step(string message, [CallerMemberName] string source = "")
    {
        Write("TEST", "STEP", source, message);
    }

    public void Log<TState>(
        LogLevel logLevel,
        string categoryName,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.None)
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception != null)
        {
            message = $"{message} | {exception.GetType().Name}: {exception.Message}";
        }

        Write(categoryName, logLevel.ToString().ToUpperInvariant(), eventId.Name ?? eventId.Id.ToString(), message);
    }

    private void Write(string category, string level, string source, string message)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{ScenarioName}] [{level}] [{category}] {source}: {message}";
        lock (Gate)
        {
            Console.WriteLine(line);
        }
    }
}

internal sealed class ScenarioLogger<T>(ScenarioLogSink sink) : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        sink.Log(logLevel, typeof(T).Name, eventId, state, exception, formatter);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
