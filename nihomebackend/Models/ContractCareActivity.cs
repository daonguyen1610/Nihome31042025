namespace NihomeBackend.Models;

/// <summary>A customer interaction belonging to one Upstream contract.</summary>
public class ContractCareActivity
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public CustomerActivityType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
