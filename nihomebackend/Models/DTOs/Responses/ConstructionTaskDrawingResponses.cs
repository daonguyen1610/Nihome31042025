namespace NihomeBackend.Models.DTOs.Responses;

public class ConstructionTaskDrawingsResponse
{
    public Guid Version { get; set; }
    public List<ConstructionDrawingResponse> Linked { get; set; } = [];
    public List<ConstructionDrawingResponse> Available { get; set; } = [];
}

public class ConstructionDrawingResponse
{
    public int IfcReleaseItemId { get; set; }
    public int IfcReleaseId { get; set; }
    public string ReleaseNumber { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public int ShopDrawingId { get; set; }
    public string DrawingCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ConstructionItem { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public bool HasFile { get; set; }
}
