namespace NihomeBackend.Models.DTOs.Responses;

public class BusinessDocumentUploadResponse
{
    public string Path { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}