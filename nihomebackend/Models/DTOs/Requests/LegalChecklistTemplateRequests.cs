using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class LegalChecklistTemplateItem
{
    [Required, StringLength(60)]
    public string PermitTypeCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}

public sealed class SaveLegalChecklistTemplateRequest
{
    [Required, StringLength(60), RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]*$")]
    public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string NameVi { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(200)] public string NameZh { get; set; } = string.Empty;
    [Required, StringLength(200)] public string NameJa { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string ScopeVi { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Scope { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string ScopeZh { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string ScopeJa { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(100)]
    public List<LegalChecklistTemplateItem> Items { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class LegalChecklistTemplateDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? ApprovalReference { get; set; }
}
