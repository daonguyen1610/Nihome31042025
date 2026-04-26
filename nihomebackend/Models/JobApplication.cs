namespace NihomeBackend.Models;

public class JobApplication
{
    public int Id { get; set; }
    public int JobPositionId { get; set; }
    public JobPosition JobPosition { get; set; } = null!;
    public string CandidateName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int? ExperienceYears { get; set; }
    public string? CoverLetter { get; set; }
    public string? CvUrl { get; set; }
    /// <summary>new | interview | hired | rejected</summary>
    public string Status { get; set; } = "new";
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
