namespace NihomeBackend.Models.DTOs.Responses;

public class ProcessResponse
{
    public int Id { get; set; }
    public string GroupKey { get; set; } = "";
    public string? Code { get; set; }
    public string Title { get; set; } = "";
}
