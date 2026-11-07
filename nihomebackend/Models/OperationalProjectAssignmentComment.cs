namespace NihomeBackend.Models;

public sealed class OperationalProjectAssignmentComment
{
    public long Id { get; set; }
    public int AssignmentId { get; set; }
    public OperationalProjectAssignment Assignment { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;
}
