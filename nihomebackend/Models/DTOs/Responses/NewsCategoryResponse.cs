namespace NihomeBackend.Models.DTOs.Responses;

public class NewsCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string NameZh { get; set; } = "";
    public string NameJa { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
