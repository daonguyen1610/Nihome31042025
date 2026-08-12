namespace NihomeBackend.Models;

public class Vendor
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public VendorType VendorType { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? LicenseNo { get; set; }
    public string? TradeCategory { get; set; }
    public string? CapabilityFileUrl { get; set; }
    public string? DriveFolder { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum VendorType
{
    Supplier = 0,
    SubContractor = 1,
    Both = 2,
}