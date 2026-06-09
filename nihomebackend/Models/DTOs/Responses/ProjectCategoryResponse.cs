namespace NihomeBackend.Models.DTOs.Responses;

public class ProjectCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
