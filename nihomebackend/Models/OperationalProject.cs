namespace NihomeBackend.Models;

/// <summary>
/// Internal NICON project aggregate shared by the CRM, design, construction,
/// procurement, finance, document, and reporting modules. This is distinct
/// from <see cref="Project"/>, which is public website content, and from
/// <see cref="DesignProject"/>, which owns the three-stage design workflow.
/// </summary>
public class OperationalProject : IConcurrencyTracked
{
    public int Id { get; set; }

    /// <summary>Server-generated, human-readable identifier.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Every operational project belongs to exactly one customer.</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    /// <summary>Primary accountable PM; the team model can add more members.</summary>
    public int? ProjectManagerUserId { get; set; }
    public ApplicationUser? ProjectManager { get; set; }

    public OperationalProjectStatus Status { get; set; } = OperationalProjectStatus.Planning;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<Opportunity> Opportunities { get; set; } = new();
    public List<Quote> Quotes { get; set; } = new();
    public List<Contract> Contracts { get; set; } = new();
    public DesignProject? DesignProject { get; set; }
    public List<ProjectDocument> Documents { get; set; } = new();
    public List<ProjectDriveFolder> DriveFolders { get; set; } = new();
}

public enum OperationalProjectStatus
{
    Planning = 0,
    Active = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4,
}
