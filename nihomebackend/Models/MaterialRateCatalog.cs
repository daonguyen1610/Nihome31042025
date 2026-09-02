namespace NihomeBackend.Models;

public class MaterialRateCatalog
{
    public int Id { get; set; }
    public MaterialRateCatalogType CatalogType { get; set; } = MaterialRateCatalogType.InvestmentRate;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public List<MaterialRateRevision> Revisions { get; set; } = [];
}

public class MaterialRateRevision
{
    public int Id { get; set; }
    public int CatalogId { get; set; }
    public MaterialRateCatalog Catalog { get; set; } = null!;
    public int Version { get; set; }
    public MaterialRateRevisionStatus Status { get; set; } = MaterialRateRevisionStatus.Draft;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? Note { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public List<MaterialRateLine> Lines { get; set; } = [];
    public decimal TotalRatePerSqm => Lines.Sum(line => line.AmountPerSqm);
}

public class MaterialRateLine
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public MaterialRateRevision Revision { get; set; } = null!;
    public string MaterialCode { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal NormPerSqm { get; set; }
    public decimal UnitRate { get; set; }
    public decimal WastePercent { get; set; }
    public decimal AmountPerSqm { get; set; }
    public int SortOrder { get; set; }
}

public enum MaterialRateRevisionStatus
{
    Draft = 0,
    Approved = 1,
    Rejected = 2,
    Retired = 3,
}

public enum MaterialRateCatalogType
{
    InvestmentRate = 0,
    Boq = 1,
}
