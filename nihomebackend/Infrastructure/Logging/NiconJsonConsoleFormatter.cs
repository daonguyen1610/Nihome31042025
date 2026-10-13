using System.Diagnostics;
using System.Text.Encodings.Web;
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
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
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

        var outputProperties = BuildOutputProperties(properties, options, processId);
        var record = new JsonLogRecord(
            ResolveLoggerPath(category, options),
            $"date=\"{timestamp:O}\", process_id=\"{processId}\", {renderedMessage}",
            outputProperties,
            ResolveSeverity(level),
            threadName);

        return JsonSerializer.Serialize(record, SerializerOptions);
    }

    internal static string ResolveLoggerPath(
        string category,
        NiconJsonConsoleFormatterOptions options)
    {
        if (options.SourcePathOverrides.TryGetValue(category, out var configuredPath)
            && !string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Replace('\\', '/');
        }

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

        return $"{options.SourceRoot.TrimEnd('/', '\\')}/{relativeCategory.Replace('.', '/')}.cs";
    }

    private static Dictionary<string, object?> BuildOutputProperties(
        IReadOnlyDictionary<string, object?> properties,
        NiconJsonConsoleFormatterOptions options,
        int processId)
    {
        var output = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in properties)
        {
            if (property.Key.Equals("{OriginalFormat}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalizedKey = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Key);
            // Log state is collected before scopes, so later normalized keys
            // intentionally win and request-scope identity cannot be spoofed
            // by a message-template property such as {ApiName}.
            output[normalizedKey] = NormalizePropertyValue(property.Value);
        }

        output["api_name"] = ReadProperty(output, "api_name") ?? options.ApiName;
        output["app_name"] = ReadProperty(output, "app_name") ?? options.AppName;
        output["correlation_id"] = ReadProperty(output, "correlation_id")
            ?? Activity.Current?.TraceId.ToString()
            ?? $"process-{processId}";
        output["message_type"] = ReadProperty(output, "message_type") ?? options.MessageType;

        return output;
    }

    private static object? NormalizePropertyValue(object? value) => value switch
    {
        null => null,
        string or bool or byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal or DateTime or DateTimeOffset or Guid => value,
        Enum enumValue => enumValue.ToString(),
        _ => value.ToString(),
    };

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
        IReadOnlyDictionary<string, object?> Properties,
        string Severity,
        string Thread);
}
