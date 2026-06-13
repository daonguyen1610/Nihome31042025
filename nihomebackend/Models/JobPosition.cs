namespace NihomeBackend.Models;

public class JobPosition
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Department { get; set; } = "";
    public string Location { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "mid";      // junior | mid | senior
    public string? Description { get; set; }
    /// <summary>JSON array of requirement strings.</summary>
    public string RequirementsJson { get; set; } = "[]";
    /// <summary>JSON array of benefit codes.</summary>
    public string BenefitsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JobApplication> Applications { get; set; } = [];
}
