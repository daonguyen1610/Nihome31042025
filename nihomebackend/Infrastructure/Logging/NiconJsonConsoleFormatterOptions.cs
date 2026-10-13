using Microsoft.Extensions.Logging.Console;

namespace NihomeBackend.Infrastructure.Logging;

public sealed class NiconJsonConsoleFormatterOptions : ConsoleFormatterOptions
{
    public string AppName { get; set; } = "NihomeBackend";
    public string ApiName { get; set; } = "Nihome API";
    public string MessageType { get; set; } = "application_log";
    public string SourceRoot { get; set; } = "nihomebackend";
}
