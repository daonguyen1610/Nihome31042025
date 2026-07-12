using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models;

/// <summary>
/// Generic lookup / dropdown value used across CRM, Design, Permit and
/// admin surfaces. Keeps the same shape as
/// <see cref="RecruitmentDropdownOption"/> but is category-driven so any
/// module can add its own catalogue without a schema change.
///
/// Multi-language name follows the RBAC pattern: <see cref="Name"/> holds
/// the Vietnamese default and <see cref="LabelKey"/> points at a
/// translation key (`masterData.&#123;category&#125;.&#123;code&#125;.label`)
/// stored in the standard <see cref="Translation"/> table.
/// </summary>
public class MasterDataOption
{
    public int Id { get; set; }

    /// <summary>
    /// Logical group of options, e.g. <c>customer_type</c>,
    /// <c>opportunity_stage</c>, <c>permit_type</c>.
    /// </summary>
    [Required, MaxLength(80)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Stable machine identifier, unique inside a category.
    /// </summary>
    [Required, MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Vietnamese default label — used when i18n lookup misses.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Translation key for the label (optional).</summary>
    [MaxLength(200)]
    public string? LabelKey { get; set; }

    /// <summary>Free-text description in Vietnamese (optional).</summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
