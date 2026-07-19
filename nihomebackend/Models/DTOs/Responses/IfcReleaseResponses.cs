namespace NihomeBackend.Models.DTOs.Responses;

public class IfcReleaseResponse
{
    public int Id { get; set; }
    public int DesignProjectId { get; set; }
    public string? DesignProjectCode { get; set; }

    public string ReleaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public DateTime? ReleaseDate { get; set; }

    public int? IssuedByUserId { get; set; }
    public string? IssuedByName { get; set; }

    /// <summary>Enum name — Draft / Released / Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }

    public List<IfcReleaseItemResponse> Items { get; set; } = new();
    public List<IfcReleaseRecipientResponse> Recipients { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class IfcReleaseItemResponse
{
    public int Id { get; set; }
    public int ShopDrawingId { get; set; }
    public string? DrawingCode { get; set; }
    public string? Title { get; set; }
    public string? DisciplineCode { get; set; }
    public string? DisciplineLabel { get; set; }
    public string? Status { get; set; }
}

public class IfcReleaseRecipientResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RecipientTypeCode { get; set; } = string.Empty;
    public string? RecipientTypeLabel { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool IsAcknowledged => AcknowledgedAt.HasValue;
    public string? AcknowledgementNote { get; set; }
}

public class IfcReleaseListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<IfcReleaseResponse> Items { get; set; } = new();
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}
