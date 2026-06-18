using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

/// <summary>
/// Stores the response of an externally identified request so that retries
/// carrying the same <see cref="Key"/> within <see cref="ExpiresAt"/> short-circuit
/// to the original outcome instead of mutating state again.
/// </summary>
public class IdempotencyRecord
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Scope { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Fingerprint { get; set; }

    public int? UserId { get; set; }

    public int StatusCode { get; set; }

    public string? ResponseJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }
}
