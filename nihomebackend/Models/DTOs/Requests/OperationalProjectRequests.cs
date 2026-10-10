using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public abstract class OperationalProjectFieldsRequest
{
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    public int? ProjectManagerUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }
}

public class CreateOperationalProjectRequest : OperationalProjectFieldsRequest
{
    /// <summary>
    /// Optional project code chosen during creation. When omitted, the server
    /// allocates the next PJ-{year}-{sequence} code.
    /// </summary>
    [StringLength(40, MinimumLength = 2)]
    public string? Code { get; set; }
}

public class UpdateOperationalProjectRequest : OperationalProjectFieldsRequest, IConcurrencyRequest
{
    [Required]
    public OperationalProjectStatus Status { get; set; }

    public string? RowVersion { get; set; }
}

public class ReopenOperationalProjectRequest : IConcurrencyRequest
{
    public string? RowVersion { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public class OperationalProjectListParams
{
    public int? CustomerId { get; set; }
    public int? ProjectManagerUserId { get; set; }
    public OperationalProjectStatus? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
