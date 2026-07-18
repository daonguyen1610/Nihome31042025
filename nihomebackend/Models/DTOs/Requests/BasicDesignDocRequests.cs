using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

/// <summary>Create payload for a Basic Design document (NIH-115).</summary>
public class CreateBasicDesignDocRequest
{
    [Required]
    public int DesignProjectId { get; set; }

    /// <summary>Master-data code from <c>design_discipline</c> (architecture / structure / mep / interior).</summary>
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string DisciplineCode { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

/// <summary>Update payload. Status transitions use a dedicated /status endpoint.</summary>
public class UpdateBasicDesignDocRequest
{
    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string DisciplineCode { get; set; } = string.Empty;

    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    public int? OwnerUserId { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class TransitionBasicDesignDocStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public class BasicDesignDocListParams
{
    public int? DesignProjectId { get; set; }
    /// <summary>Master-data code from <c>design_discipline</c>.</summary>
    public string? DisciplineCode { get; set; }
    /// <summary>Comma-separated <c>BasicDesignDocStatus</c> values.</summary>
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
