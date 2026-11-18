using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public sealed class DesignApprovalInboxQuery
{
    /// <summary>Matches code, title, dossier code or name, and the person who submitted.</summary>
    [StringLength(200)]
    public string? Search { get; set; }

    public int? DesignProjectId { get; set; }

    /// <summary>Reviews only: Concept, BasicDesign or ShopDrawing.</summary>
    public string? Stage { get; set; }

    /// <summary>Reviews only: true = already commented, false = not yet.</summary>
    public bool? Commented { get; set; }

    /// <summary>Waiting longest first unless set to true.</summary>
    public bool NewestFirst { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
