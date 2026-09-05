using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public interface IProjectAccessService
{
    Task<bool> CanViewOperationalProjectAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanManageTeamAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanManageDesignScheduleAsync(int userId, int projectId, CancellationToken ct = default);
    Task<bool> CanViewDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default);
    Task<bool> CanManageDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default, string? disciplineCode = null);
    Task<bool> CanApproveDesignProjectAsync(int userId, int designProjectId, CancellationToken ct = default, string? disciplineCode = null);
    Task<bool> CanViewDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> CanManageDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> CanApproveDesignResourceAsync(int userId, DesignProjectResourceType resourceType, int resourceId, CancellationToken ct = default);
    Task<bool> HasAdministrativeBypassAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlySet<int>> GetAccessibleOperationalProjectIdsAsync(int userId, CancellationToken ct = default);
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
}

public interface IProjectTeamService
{
    Task<OperationalProjectTeamResponse?> GetAsync(int projectId, int callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<OperationalProjectTeamHistoryResponse>?> GetHistoryAsync(int projectId, int callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMemberCandidateResponse>?> GetCandidatesAsync(int projectId, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectMemberResponse> AddMemberAsync(int projectId, UpsertOperationalProjectMemberRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectMemberResponse?> UpdateMemberAsync(int projectId, int memberId, UpsertOperationalProjectMemberRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectAssignmentResponse> AddAssignmentAsync(int projectId, UpsertOperationalProjectAssignmentRequest request, int callerUserId, CancellationToken ct = default);
    Task<OperationalProjectAssignmentResponse?> UpdateAssignmentAsync(int projectId, int assignmentId, UpsertOperationalProjectAssignmentRequest request, int callerUserId, CancellationToken ct = default);
}

public sealed class ProjectTeamOperationException(string message) : Exception(message);
