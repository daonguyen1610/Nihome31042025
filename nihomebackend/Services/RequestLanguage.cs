namespace NihomeBackend.Services;

/// <summary>The language the user reads the app in, sent by the client on every request.</summary>
public interface IRequestLanguage
{
    string Lang { get; }
}

/// <summary>
/// Reads <c>X-Nihome-Lang</c> (vi, en, zh or ja); anything else, or no request, is Vietnamese,
/// the source language.
/// </summary>
public sealed class RequestLanguage(IHttpContextAccessor http) : IRequestLanguage
{
    public const string Header = "X-Nihome-Lang";
    private static readonly string[] Supported = ["vi", "en", "zh", "ja"];

    public string Lang
    {
        get
        {
            var value = http.HttpContext?.Request.Headers[Header].ToString().Trim().ToLowerInvariant();
            return value is not null && Supported.Contains(value) ? value : "vi";
        }
    }
}
