namespace NihomeBackend.Models.DTOs.Requests;

public class UpsertNewsCategoryRequest
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
