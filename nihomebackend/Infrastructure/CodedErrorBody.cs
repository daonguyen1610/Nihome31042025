namespace NihomeBackend.Infrastructure;

/// <summary>
/// The 400 body of a business error: the Vietnamese message (also logged and audited), and,
/// when the client needs to localize it, a stable code with the values the message needs.
/// </summary>
public static class CodedErrorBody
{
    public static object Create(string message, string? code, IReadOnlyDictionary<string, object>? parameters) =>
        code is null
            ? new { message }
            : new { message, code, @params = parameters };
}
