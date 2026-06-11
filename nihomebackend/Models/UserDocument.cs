using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

public enum UserDocumentType
{
    CCCD,
    PASSPORT,
    OTHER,
}

public class UserDocument
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [Required]
    public UserDocumentType DocumentType { get; set; } = UserDocumentType.OTHER;

    [Required, MaxLength(255)]
    public string OriginalName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
