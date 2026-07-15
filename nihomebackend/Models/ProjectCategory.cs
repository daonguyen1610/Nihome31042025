namespace NihomeBackend.Models;

public class ProjectCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string NameZh { get; set; } = "";
    public string NameJa { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
