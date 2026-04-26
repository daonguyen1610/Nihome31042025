using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class SubmitJobApplicationRequest
{
    public int JobPositionId { get; set; }

    [Required]
    public string CandidateName { get; set; } = "";

    [Required, EmailAddress]
    public string Email { get; set; } = "";

    public string? Phone { get; set; }

    public int? ExperienceYears { get; set; }

    public string? CoverLetter { get; set; }

    public string? CvUrl { get; set; }
}

public class UpdateApplicationStatusRequest
{
    [Required]
    public string Status { get; set; } = "new";
}
