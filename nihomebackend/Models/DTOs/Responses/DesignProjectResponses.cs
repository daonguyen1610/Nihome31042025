namespace NihomeBackend.Models.DTOs.Responses;

/// <summary>Detail-page shape for a <see cref="Models.DesignProject"/>.</summary>
public class DesignProjectResponse
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public int? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }

    public int? DesignLeadUserId { get; set; }
    public string? DesignLeadName { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }

    /// <summary>Enum name — Concept / BasicDesign / ShopDrawing / Completed.</summary>
    public string CurrentStage { get; set; } = string.Empty;
    /// <summary>Enum name — Active / OnHold / Completed / Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Slimmed-down row used by the NIH-113 list view.</summary>
public class DesignProjectListItemResponse
{
    public int Id { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public int? ContractId { get; set; }
    public string? ContractNumber { get; set; }

    public int? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }

    public int? DesignLeadUserId { get; set; }
    public string? DesignLeadName { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }

    public string CurrentStage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}

public class DesignProjectListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<DesignProjectListItemResponse> Items { get; set; } = new();
}
