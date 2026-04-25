using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.Models;

/// <summary>
/// Dynamic entity content translations (polymorphic pattern).
/// Vietnamese is the default stored directly on entities; other languages stored here.
/// </summary>
[Index(nameof(EntityType), nameof(EntityId), nameof(FieldName), nameof(LanguageCode), IsUnique = true)]
public class EntityTranslation
{
    public int Id { get; set; }

    [Required][MaxLength(50)]
    public string EntityType { get; set; } = "";

    public int EntityId { get; set; }

    [Required][MaxLength(50)]
    public string FieldName { get; set; } = "";

    [Required][MaxLength(10)]
    public string LanguageCode { get; set; } = "";

    [Required]
    public string Value { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
