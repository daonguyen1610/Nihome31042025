using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IProjectAccessService
{
    Task<bool> CanViewOperationalProjectAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanManageTeamAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanManageDesignScheduleAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanManageDesignDocumentsAsync(int userId, int operationalProjectId, CancellationToken ct = default);
    Task<bool> CanManageDesignDocumentSourceAsync(int userId, int operationalProjectId, string? sourceType, long? sourceId, CancellationToken ct = default);
    Task<bool> CanManageDesignDocumentAsync(int userId, int operationalProjectId, long documentId, CancellationToken ct = default);
    Task<bool> CanViewDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default);
    Task<DesignProjectPermissions> GetDesignProjectPermissionsAsync(int userId, int designProjectId, CancellationToken ct = default);
    Task<bool> HasDesignProjectPermissionAsync(int userId, int designProjectId, DesignProjectAction action, CancellationToken ct = default, string? disciplineCode = null);
    Task<bool> CanManageDesignProjectTeamAsync(int userId, int designProjectId, CancellationToken ct = default);
    Task<bool> CanManageDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default, string? disciplineCode = null);
    Task<bool> CanApproveDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default, string? disciplineCode = null);
    Task<bool> CanViewDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> CanManageDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> CanWriteDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, DesignProjectAction action, CancellationToken ct = default);
    Task<bool> CanApproveDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> HasAdministrativeBypassAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlySet<int>> GetAccessibleOperationalProjectIdsAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlySet<int>> GetAccessibleDesignProjectIdsAsync(int userId, CancellationToken ct = default);
    Task<int?> ResolveDesignCreateOperationalProjectIdAsync(int? operationalProjectId, int? contractId, CancellationToken ct = default);
    Task<int?> ResolveDesignProjectIdAsync(DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, int>> ResolveDesignProjectIdsAsync(DesignProjectResourceType resourceType, IEnumerable<int> resourceIds, CancellationToken ct = default);
    Task<string?> ResolveDesignDisciplineAsync(DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<IReadOnlySet<string>?> GetAccessibleDesignDisciplinesAsync(int userId, int designProjectId, CancellationToken ct = default);
}

public enum DesignProjectResourceType
{
    ConceptOption,
    BasicDesignDoc,
    ShopDrawing,
    DrawingRevision,
    IfcRelease,
    IfcReleaseItem,
    IfcReleaseRecipient,
    ConstructionTask,
    PermitChecklistItem,
}

public enum DesignProjectAction
{
    View = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
}

/// <summary>Effective dossier rights of one user on one design project.</summary>
public sealed record DesignProjectPermissions(bool CanView, bool CanCreate, bool CanUpdate, bool CanDelete)
{
    public static readonly DesignProjectPermissions None = new(false, false, false, false);
    public static readonly DesignProjectPermissions ViewOnly = new(true, false, false, false);
    public static readonly DesignProjectPermissions Full = new(true, true, true, true);

    /// <summary>
    /// Defaults used when a member has no explicit matrix row yet: leadership
    /// gets full rights, design disciplines may create/update, everyone else
    /// may only view.
    /// </summary>
    public static DesignProjectPermissions FromRoles(IEnumerable<OperationalProjectMemberRole> memberRoles)
    {
        var roles = memberRoles.Where(role => role.EndedAt == null).ToList();
        static bool InDesignScope(OperationalProjectMemberRole role) =>
            role.Scope == ProjectRoleScope.Project ||
            role.Scope == ProjectRoleScope.Module &&
                string.Equals(role.ScopeValue, "Design", StringComparison.OrdinalIgnoreCase) ||
            role.Scope == ProjectRoleScope.Discipline;

        if (roles.Any(role => InDesignScope(role) &&
            role.RoleCode is ProjectTeamRoleCode.DesignLead or ProjectTeamRoleCode.ProjectManager))
            return Full;

        if (roles.Any(role => InDesignScope(role) &&
            role.RoleCode is ProjectTeamRoleCode.Architect or ProjectTeamRoleCode.StructuralEngineer
                or ProjectTeamRoleCode.MepEngineer or ProjectTeamRoleCode.InteriorDesigner))
            return new DesignProjectPermissions(true, true, true, false);

        return ViewOnly;
    }
}

public interface IProjectTeamService
{
    Task<OperationalProjectTeamResponse?> GetAsync(int projectId, int callerUserId, CancellationToken ct = default, int? designProjectId = null);
    Task<IReadOnlyList<OperationalProjectTeamHistoryResponse>?> GetHistoryAsync(int projectId, int callerUserId, CancellationToken ct = default, int? designProjectId = null);
    Task<IReadOnlyList<ProjectMemberCandidateResponse>?> GetCandidatesAsync(int projectId, int callerUserId, CancellationToken ct = default, int? designProjectId = null);
    Task<OperationalProjectMemberResponse> AddMemberAsync(int projectId, UpsertOperationalProjectMemberRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectMemberResponse?> UpdateMemberAsync(int projectId, int memberId, UpsertOperationalProjectMemberRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectMemberResponse?> UpdateMemberPermissionsAsync(int projectId, int memberId, UpdateDesignProjectMemberPermissionsRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectAssignmentResponse> AddAssignmentAsync(int projectId, UpsertOperationalProjectAssignmentRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectAssignmentResponse?> UpdateAssignmentAsync(int projectId, int assignmentId, UpsertOperationalProjectAssignmentRequest request, int callerUserId, CancellationToken ct = default);
}

public sealed class ProjectTeamOperationException(string message) : Exception(message);
