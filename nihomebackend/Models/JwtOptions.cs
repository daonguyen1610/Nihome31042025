namespace NihomeBackend.Models;

public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; }

    public int RefreshTokenDays { get; set; }

    public string ActiveKeyId { get; set; } = "key2";

    public Dictionary<string, string> Keys { get; set; } = [];
}
