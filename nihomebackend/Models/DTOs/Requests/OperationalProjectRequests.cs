using System.ComponentModel.DataAnnotations;

namespace NihomeBackend.Models.DTOs.Requests;

public class CreateOperationalProjectRequest
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

public class UpdateOperationalProjectRequest : CreateOperationalProjectRequest, IConcurrencyRequest
{
    [Required]
    public OperationalProjectStatus Status { get; set; }

    public string? RowVersion { get; set; }
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
