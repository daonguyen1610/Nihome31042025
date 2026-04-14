namespace NihomeBackend.Models;

public sealed class HealthResponse
{
    public string Name { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime TimestampUtc { get; set; }
}
