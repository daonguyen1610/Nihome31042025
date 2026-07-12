namespace NihomeBackend.Models;

/// <summary>
/// Contact person associated with a <see cref="Customer"/>. A customer must
/// have at least one contact; exactly one is flagged as
/// <see cref="IsPrimary"/> (enforced by the service).
/// </summary>
public class CustomerContact
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;

    public string? Position { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
