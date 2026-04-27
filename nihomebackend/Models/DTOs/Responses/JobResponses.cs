namespace NihomeBackend.Models.DTOs.Responses;

public class JobPositionResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Department { get; set; } = "";
    public string Location { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string ExperienceLevel { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Requirements { get; set; } = [];
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public int ApplicationCount { get; set; }
}

public class RecruitmentOptionResponse
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class RecruitmentMetadataItemResponse
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public string? TranslationKey { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class RecruitmentMetadataResponse
{
    public List<RecruitmentOptionResponse> EmploymentTypes { get; set; } = [];
    public List<RecruitmentOptionResponse> ExperienceLevels { get; set; } = [];
    public List<RecruitmentOptionResponse> ApplicationStatuses { get; set; } = [];
}

public class JobApplicationResponse
{
    public int Id { get; set; }
    public int JobPositionId { get; set; }
    public string PositionTitle { get; set; } = "";
    public string CandidateName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int? ExperienceYears { get; set; }
    public string? CoverLetter { get; set; }
    public string? CvUrl { get; set; }
    public string Status { get; set; } = "new";
    public DateTime AppliedAt { get; set; }
}
