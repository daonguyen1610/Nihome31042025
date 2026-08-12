using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateVendorRequest
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string VendorCode { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    public VendorType? VendorType { get; set; }

    [StringLength(20)]
    public string? TaxCode { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(200)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(100)]
    public string? LicenseNo { get; set; }

    [StringLength(300)]
    public string? TradeCategory { get; set; }

    [StringLength(1000)]
    public string? CapabilityFileUrl { get; set; }

    [StringLength(1000)]
    public string? DriveFolder { get; set; }
}

public class UpdateVendorRequest : CreateVendorRequest
{
    public bool IsActive { get; set; } = true;
}