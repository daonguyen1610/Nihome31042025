namespace NihomeBackend.Models;

public class RecruitmentMetadataItem
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string Value { get; set; } = "";
    public string Label { get; set; } = "";
    public string? TranslationKey { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
