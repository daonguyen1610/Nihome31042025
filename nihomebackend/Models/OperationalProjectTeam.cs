namespace NihomeBackend.Models;

public class OperationalProjectMember : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Position { get; set; } = string.Empty;
    public int? ReportsToMemberId { get; set; }
    public OperationalProjectMember? ReportsToMember { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Source { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<OperationalProjectMemberRole> Roles { get; set; } = new();
    public List<OperationalProjectAssignment> Assignments { get; set; } = new();
}

public class OperationalProjectMemberRole
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public OperationalProjectMember Member { get; set; } = null!;
    public ProjectTeamRoleCode RoleCode { get; set; }
    public ProjectRoleScope Scope { get; set; } = ProjectRoleScope.Project;
    public string? ScopeValue { get; set; }
    public string Source { get; set; } = "Manual";
    public string? SourceReference { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class OperationalProjectAssignment : IConcurrencyTracked
{
    public int Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string WorkKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Discipline { get; set; }
    public string? ParallelGroup { get; set; }
    public int AssigneeMemberId { get; set; }
    public OperationalProjectMember AssigneeMember { get; set; } = null!;
    public int? ManagerMemberId { get; set; }
    public OperationalProjectMember? ManagerMember { get; set; }
    public ProjectAssignmentStatus Status { get; set; } = ProjectAssignmentStatus.Planned;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class OperationalProjectTeamHistory
{
    public long Id { get; set; }
    public int OperationalProjectId { get; set; }
    public OperationalProject OperationalProject { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public int ChangedByUserId { get; set; }
    public ApplicationUser ChangedByUser { get; set; } = null!;
}

public enum ProjectTeamRoleCode
{
    ProjectManager = 0,
    DesignLead = 1,
    Architect = 2,
    StructuralEngineer = 3,
    MepEngineer = 4,
    InteriorDesigner = 5,
    LegalOfficer = 6,
    SiteEngineer = 7,
    QuantitySurveyor = 8,
    Observer = 9,
}

public enum ProjectRoleScope
{
    Project = 0,
    Module = 1,
    Discipline = 2,
}

public enum ProjectAssignmentStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}
