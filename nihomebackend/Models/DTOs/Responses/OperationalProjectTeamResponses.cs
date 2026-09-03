namespace NihomeBackend.Models.DTOs.Responses;

public sealed class OperationalProjectTeamResponse
{
    public int OperationalProjectId { get; set; }
    public bool CanManage { get; set; }
    public List<ProjectRoleDefinitionResponse> RoleDefinitions { get; set; } = new();
    public List<string> ModuleOptions { get; set; } = new();
    public List<string> DisciplineOptions { get; set; } = new();
    public List<OperationalProjectMemberResponse> Members { get; set; } = new();
    public List<OperationalProjectAssignmentResponse> Assignments { get; set; } = new();
}

public sealed class ProjectRoleDefinitionResponse
{
    public string Code { get; set; } = string.Empty;
    public string Raci { get; set; } = string.Empty;
    public bool CanManageTeam { get; set; }
    public bool CanApproveDesign { get; set; }
    public List<string> AllowedScopes { get; set; } = new();
}

public sealed class OperationalProjectMemberResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int? ReportsToMemberId { get; set; }
    public string? ReportsToName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public List<ProjectMemberRoleResponse> Roles { get; set; } = new();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProjectMemberRoleResponse
{
    public string RoleCode { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? ScopeValue { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class OperationalProjectAssignmentResponse
{
    public int Id { get; set; }
    public string WorkKey { get; set; } = string.Empty;
    public string KpiIdentity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Discipline { get; set; }
    public string? ParallelGroup { get; set; }
    public int AssigneeMemberId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public int? ManagerMemberId { get; set; }
    public string? ManagerName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class OperationalProjectTeamHistoryResponse
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public int ChangedByUserId { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
}

public sealed class ProjectMemberCandidateResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
