namespace NihomeBackend.Models.DTOs.Responses;

public class ContactMessageResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public bool IsReplied { get; set; }
    public string? ReplyContent { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
