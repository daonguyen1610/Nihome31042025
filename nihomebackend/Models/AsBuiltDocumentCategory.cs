namespace NihomeBackend.Models;

/// <summary>
/// Dynamic document category for as-built dossier.
/// Replaces the old hardcoded <c>AsBuiltCategory</c> enum.
/// </summary>
public class AsBuiltDocumentCategory
{
    public int Id { get; set; }

    /// <summary>Internal code for programmatic references (e.g. "Drawing", "AcceptanceMinute").</summary>
    public string Code { get; set; } = "";

    /// <summary>Primary display name (Vietnamese).</summary>
    public string Name { get; set; } = "";
    public string NameVi { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string NameZh { get; set; } = "";
    public string NameJa { get; set; } = "";

    /// <summary>Whether this category is required for handover completeness.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Soft-delete / deactivate - hidden from new document creation but existing docs preserved.</summary>
    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation - documents using this category
    public ICollection<AsBuiltDocument> Documents { get; set; } = new List<AsBuiltDocument>();
}
