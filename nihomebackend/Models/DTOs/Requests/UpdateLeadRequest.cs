using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class UpdateLeadRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(150)]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>New status. <c>NotInterested</c> and <c>Junk</c> require
    /// the <c>crm.leads.manage</c> permission — the service enforces this.</summary>
    [Required]
    public LeadStatus Status { get; set; }

    /// <summary>Assign / re-assign owner. Only users with
    /// <c>crm.leads.manage</c> may change owner; a Sales user may only leave
    /// their own id here.</summary>
    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}
