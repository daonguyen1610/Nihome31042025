namespace NihomeBackend.Models;

/// <summary>A task references a released IFC item; it never owns the drawing or its file.</summary>
public class ConstructionTaskDrawing
{
    public int Id { get; set; }
    public int ConstructionTaskId { get; set; }
    public ConstructionTask ConstructionTask { get; set; } = null!;
    public int IfcReleaseItemId { get; set; }
    public IfcReleaseItem IfcReleaseItem { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
}
