using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.Models;

/// <summary>
/// Static UI translation strings (navigation, buttons, labels, messages).
/// Admin-managed key-value pairs per language.
/// </summary>
[Index(nameof(Key), nameof(LanguageCode), IsUnique = true)]
public class Translation
{
    public int TranslationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Key { get; set; } = "";

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";

    [Required]
    public string Value { get; set; } = "";

    [MaxLength(50)]
    public string? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
