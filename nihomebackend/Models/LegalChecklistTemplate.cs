namespace NihomeBackend.Models;

public enum LegalChecklistTemplateStatus { Draft, Published, Retired }

public sealed class LegalChecklistTemplate : IConcurrencyTracked
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string NameVi { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameJa { get; set; } = string.Empty;
    public string ScopeVi { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string ScopeZh { get; set; } = string.Empty;
    public string ScopeJa { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public LegalChecklistTemplateStatus Status { get; set; }
    public int CreatedByUserId { get; set; }
    public int UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ApprovalReference { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
