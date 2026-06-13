namespace NihomeBackend.Models;

public class RecruitmentDropdownOption
{
    public int Id { get; set; }
    /// <summary>"experience-level" or "benefit"</summary>
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
