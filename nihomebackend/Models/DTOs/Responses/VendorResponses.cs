namespace NihomeBackend.Models.DTOs.Responses;

public class VendorResponse
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
    public bool IsActive { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class VendorListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<VendorResponse> Items { get; set; } = new();
}