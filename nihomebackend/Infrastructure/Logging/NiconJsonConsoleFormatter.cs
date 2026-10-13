using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace NihomeBackend.Infrastructure.Logging;

public sealed class NiconJsonConsoleFormatter : ConsoleFormatter, IDisposable
{
    public const string FormatterName = "nicon-json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IDisposable? _optionsReloadToken;
    private NiconJsonConsoleFormatterOptions _options;

    public NiconJsonConsoleFormatter(IOptionsMonitor<NiconJsonConsoleFormatterOptions> options)
        : base(FormatterName)
    {
        _options = options.CurrentValue;
        _optionsReloadToken = options.OnChange(updated => _options = updated);
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var logMessage = logEntry.Formatter(logEntry.State, logEntry.Exception);
        if (string.IsNullOrWhiteSpace(logMessage) && logEntry.Exception is null)
        {
            return;
        }

        var properties = CollectProperties(logEntry.State, scopeProvider);
        var line = FormatLogLine(
            logEntry.Category,
            logEntry.LogLevel,
            logMessage,
            logEntry.Exception,
            properties,
            _options,
            DateTimeOffset.UtcNow,
            Environment.ProcessId,
            Thread.CurrentThread.Name ?? $"managed-{Environment.CurrentManagedThreadId}");

        textWriter.WriteLine(line);
    }

    internal static string FormatLogLine(
        string category,
        LogLevel level,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?> properties,
        NiconJsonConsoleFormatterOptions options,
        DateTimeOffset timestamp,
        int processId,
        string threadName)
    {
        var renderedMessage = exception is null
            ? message
            : $"{message} | {exception}";

        var record = new JsonLogRecord(
            ResolveLoggerPath(category, options.SourceRoot),
            $"date=\"{timestamp:O}\", process_id=\"{processId}\", {renderedMessage}",
            new JsonLogProperties(
                ReadProperty(properties, "api_name") ?? options.ApiName,
                ReadProperty(properties, "app_name") ?? options.AppName,
                ReadProperty(properties, "correlation_id")
                    ?? Activity.Current?.TraceId.ToString()
                    ?? string.Empty,
                ReadProperty(properties, "message_type") ?? options.MessageType),
            ResolveSeverity(level),
            threadName);

        return JsonSerializer.Serialize(record, SerializerOptions);
    }

    internal static string ResolveLoggerPath(string category, string sourceRoot)
    {
        const string applicationPrefix = "NihomeBackend.";
        if (!category.StartsWith(applicationPrefix, StringComparison.Ordinal))
        {
            return category;
        }

        var relativeCategory = category[applicationPrefix.Length..];
        var genericMarker = relativeCategory.IndexOf('`');
        if (genericMarker >= 0)
        {
            relativeCategory = relativeCategory[..genericMarker];
        }

        return $"{sourceRoot.TrimEnd('/', '\\')}/{relativeCategory.Replace('.', '/')}.cs";
    }

    private static Dictionary<string, object?> CollectProperties<TState>(
        TState state,
        IExternalScopeProvider? scopeProvider)
    {
        var properties = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        AddProperties(properties, state);
        scopeProvider?.ForEachScope(
            (scope, target) => AddProperties(target, scope),
            properties);
        return properties;
    }

    private static void AddProperties(Dictionary<string, object?> target, object? state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return;
        }

        foreach (var pair in pairs)
        {
            target[pair.Key] = pair.Value;
        }
    }

    private static string? ReadProperty(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static string ResolveSeverity(LogLevel level) => level switch
    {
        LogLevel.Trace => "DEBUG",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARNING",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRITICAL",
        _ => "INFO",
    };

    public void Dispose() => _optionsReloadToken?.Dispose();

    private sealed record JsonLogRecord(
        string Logger,
        string Message,
        JsonLogProperties Properties,
        string Severity,
        string Thread);

    private sealed record JsonLogProperties(
        string ApiName,
        string AppName,
        string CorrelationId,
        string MessageType);
}
