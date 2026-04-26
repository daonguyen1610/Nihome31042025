using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertJobPositionRequest
{
    [Required]
    public string Title { get; set; } = "";

    [Required]
    public string Department { get; set; } = "";

    [Required]
    public string Location { get; set; } = "";

    public string EmploymentType { get; set; } = "full-time";

    public string ExperienceLevel { get; set; } = "mid";

    public string? Description { get; set; }

    public List<string> Requirements { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
