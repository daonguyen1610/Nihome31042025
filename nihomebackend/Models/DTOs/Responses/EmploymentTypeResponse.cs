namespace NihomeBackend.Models.DTOs.Responses;

public class EmploymentTypeResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
