using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateVendorRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string VendorCode { get; set; } = string.Empty;
    [Required, StringLength(300, MinimumLength = 1)]
    public string CompanyName { get; set; } = string.Empty;
    [Required]
    public VendorType VendorType { get; set; }
    [StringLength(20)]
    public string? TaxCode { get; set; }
    [StringLength(30)]
    public string? Phone { get; set; }
    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }
    [StringLength(500)]
    public string? Address { get; set; }
    [StringLength(150)]
    public string? ContactPerson { get; set; }
    [StringLength(100)]
    public string? LicenseNo { get; set; }
    [Required, StringLength(80, MinimumLength = 1)]
    public string ServiceGroupCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int OwnerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateVendorRequest : CreateVendorRequest;

public class UpsertVendorEvaluationRequest
{
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }
    [Range(0, 10)]
    public byte ScoreQuality { get; set; }
    [Range(0, 10)]
    public byte ScoreSchedule { get; set; }
    [Range(0, 10)]
    public byte ScoreCost { get; set; }
    [Range(0, 10)]
    public byte ScoreSafety { get; set; }
    [StringLength(1000)]
    public string? Comment { get; set; }
}
