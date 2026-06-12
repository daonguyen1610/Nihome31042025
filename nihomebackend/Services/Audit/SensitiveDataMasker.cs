using System.Text.Json;
using System.Text.Json.Nodes;

namespace NihomeBackend.Services.Audit;

/// <summary>
/// Serializes arbitrary objects to JSON with sensitive fields replaced by
/// "***". The check is case-insensitive substring match on property names.
/// </summary>
public static class SensitiveDataMasker
{
    private static readonly string[] SensitiveTokens = new[]
    {
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "authorization",
        "refresh",
        "otp",
        "pin",
        "ssn",
        "creditcard",
        "credit_card",
        "cvv",
        "cardnumber",
        "card_number",
        "privatekey",
        "private_key",
        "salt",
        "hash",
    };

    private const string Mask = "***";
    private const int MaxJsonChars = 8000; // hard cap; avoids huge payloads

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string? Serialize(object? value)
    {
        if (value is null) return null;
        try
        {
            var node = value is JsonNode existing
                ? existing.DeepClone()
                : JsonSerializer.SerializeToNode(value, SerializerOptions);
            if (node is null) return null;
            MaskInPlace(node);
            var json = node.ToJsonString(SerializerOptions);
            return json.Length > MaxJsonChars
                ? json[..MaxJsonChars] + "...[truncated]"
                : json;
        }
        catch
        {
            // Never bubble — serialization issues must not break business flow
            return null;
        }
    }

    private static void MaskInPlace(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = JsonValue.Create(Mask);
                    }
                    else if (obj[key] is JsonNode child)
                    {
                        MaskInPlace(child);
                    }
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    if (item is not null) MaskInPlace(item);
                }
                break;
        }
    }

    private static bool IsSensitive(string name)
    {
        var lower = name.ToLowerInvariant();
        foreach (var token in SensitiveTokens)
        {
            if (lower.Contains(token)) return true;
        }
        return false;
    }
}
