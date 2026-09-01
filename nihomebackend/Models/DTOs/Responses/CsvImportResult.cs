namespace NihomeBackend.Models.DTOs.Responses;

public sealed class CsvImportError
{
    public int? Row { get; init; }
    public int? Column { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? MessageKey { get; init; }
    public Dictionary<string, object>? MessageArgs { get; init; }
}

public sealed class CsvImportResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Headers { get; init; } = [];
    public List<IReadOnlyDictionary<string, string>> Rows { get; init; } = [];
    public List<CsvImportError> Errors { get; init; } = [];
}
