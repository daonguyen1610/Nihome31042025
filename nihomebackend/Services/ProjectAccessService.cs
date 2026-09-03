using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

public sealed class ProjectAccessService(AppDbContext db, IPermissionService permissions) : IProjectAccessService
{
    public async Task<bool> CanViewOperationalProjectAsync(
        int userId,
        int projectId,
        CancellationToken ct = default)
    {
        if (await HasAdministrativeBypassAsync(userId, ct))
        {
            return await db.OperationalProjects.AsNoTracking().AnyAsync(project => project.Id == projectId, ct);
        }

        return await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId &&
            (project.ProjectManagerUserId == userId ||
             project.CreatedByUserId == userId ||
             project.TeamMembers.Any(member => member.UserId == userId && member.EndedAt == null)), ct);
    }

    public async Task<bool> CanManageTeamAsync(
        int userId,
        int projectId,
        CancellationToken ct = default)
    {
        if (await HasAdministrativeBypassAsync(userId, ct)) return true;
        if (!await permissions.HasAsync(userId, "operations.projects.manage", ct)) return false;

        return await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId &&
            (project.ProjectManagerUserId == userId ||
             project.TeamMembers.Any(member =>
                 member.UserId == userId &&
                 member.EndedAt == null &&
                 member.Roles.Any(role => role.EndedAt == null &&
                     (role.RoleCode == ProjectTeamRoleCode.ProjectManager ||
                      role.RoleCode == ProjectTeamRoleCode.DesignLead) &&
                     (role.Scope == ProjectRoleScope.Project ||
                      role.Scope == ProjectRoleScope.Module && role.ScopeValue == "Design")))), ct);
    }

    public async Task<bool> CanViewDesignProjectAsync(
        int userId,
        int designProjectId,
        CancellationToken ct = default)
    {
        var project = await db.DesignProjects.AsNoTracking()
            .Where(item => item.Id == designProjectId)
            .Select(item => new
            {
                item.OperationalProjectId,
                item.ProjectManagerUserId,
                item.DesignLeadUserId,
            })
            .SingleOrDefaultAsync(ct);
        if (project is null) return false;
        if (project.OperationalProjectId.HasValue)
        {
            return await CanViewOperationalProjectAsync(userId, project.OperationalProjectId.Value, ct);
        }

        return await HasAdministrativeBypassAsync(userId, ct) ||
            project.ProjectManagerUserId == userId ||
            project.DesignLeadUserId == userId;
    }

    public Task<bool> CanManageDesignProjectAsync(
        int userId,
        int designProjectId,
        CancellationToken ct = default,
        string? disciplineCode = null) =>
        HasDesignRoleAsync(userId, designProjectId, disciplineCode, approvalOnly: false, ct);

    public Task<bool> CanApproveDesignProjectAsync(
        int userId,
        int designProjectId,
        CancellationToken ct = default,
        string? disciplineCode = null) =>
        HasDesignRoleAsync(userId, designProjectId, disciplineCode, approvalOnly: true, ct);

    public async Task<bool> CanViewDesignResourceAsync(
        int userId,
        DesignProjectResourceType resourceType,
        int resourceId,
        CancellationToken ct = default)
    {
        var projectId = await ResolveDesignProjectIdAsync(resourceType, resourceId, ct);
        if (!projectId.HasValue || !await CanViewDesignProjectAsync(userId, projectId.Value, ct)) return false;
        var disciplineCode = await ResolveDesignDisciplineAsync(resourceType, resourceId, ct);
        var disciplines = await GetAccessibleDesignDisciplinesAsync(userId, projectId.Value, ct);
        return disciplines is null || disciplineCode is not null && disciplines.Contains(disciplineCode);
    }

    public async Task<bool> CanManageDesignResourceAsync(
        int userId,
        DesignProjectResourceType resourceType,
        int resourceId,
        CancellationToken ct = default)
    {
        var projectId = await ResolveDesignProjectIdAsync(resourceType, resourceId, ct);
        if (!projectId.HasValue) return false;
        var disciplineCode = await ResolveDesignDisciplineAsync(resourceType, resourceId, ct);
        return await CanManageDesignProjectAsync(userId, projectId.Value, ct, disciplineCode);
    }

    public async Task<bool> CanApproveDesignResourceAsync(
        int userId,
        DesignProjectResourceType resourceType,
        int resourceId,
        CancellationToken ct = default)
    {
        var projectId = await ResolveDesignProjectIdAsync(resourceType, resourceId, ct);
        if (!projectId.HasValue) return false;
        var disciplineCode = await ResolveDesignDisciplineAsync(resourceType, resourceId, ct);
        return await CanApproveDesignProjectAsync(userId, projectId.Value, ct, disciplineCode);
    }

    public Task<bool> HasAdministrativeBypassAsync(int userId, CancellationToken ct = default) =>
        permissions.HasAsync(userId, "operations.projects.view.all", ct);

    public async Task<IReadOnlySet<int>> GetAccessibleOperationalProjectIdsAsync(
        int userId,
        CancellationToken ct = default)
    {
        if (await HasAdministrativeBypassAsync(userId, ct))
        {
            return (await db.OperationalProjects.AsNoTracking()
                .Select(project => project.Id)
                .ToListAsync(ct))
                .ToHashSet();
        }

        return (await db.OperationalProjects.AsNoTracking()
            .Where(project => project.ProjectManagerUserId == userId ||
                project.CreatedByUserId == userId ||
                project.TeamMembers.Any(member => member.UserId == userId && member.EndedAt == null))
            .Select(project => project.Id)
            .ToListAsync(ct))
            .ToHashSet();
    }

    public async Task<int?> ResolveDesignCreateOperationalProjectIdAsync(
        int? operationalProjectId,
        int? contractId,
        CancellationToken ct = default)
    {
        if (operationalProjectId.HasValue || !contractId.HasValue)
        {
            return operationalProjectId;
        }

        return await db.Contracts.AsNoTracking()
            .Where(contract => contract.Id == contractId.Value)
            .Select(contract => contract.OperationalProjectId)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<int?> ResolveDesignProjectIdAsync(
        DesignProjectResourceType resourceType,
        int resourceId,
        CancellationToken ct = default)
    {
        var resolved = await ResolveDesignProjectIdsAsync(resourceType, [resourceId], ct);
        return resolved.GetValueOrDefault(resourceId);
    }

    public async Task<IReadOnlyDictionary<int, int>> ResolveDesignProjectIdsAsync(
        DesignProjectResourceType resourceType,
        IEnumerable<int> resourceIds,
        CancellationToken ct = default)
    {
        var ids = resourceIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, int>();

        return resourceType switch
        {
            DesignProjectResourceType.ConceptOption => await db.ConceptOptions.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            DesignProjectResourceType.BasicDesignDoc => await db.BasicDesignDocs.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            DesignProjectResourceType.ShopDrawing => await db.ShopDrawings.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            DesignProjectResourceType.DrawingRevision => await ResolveRevisionProjectIdsAsync(ids, ct),
            DesignProjectResourceType.IfcRelease => await db.IfcReleases.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            DesignProjectResourceType.IfcReleaseItem => await (
                    from item in db.IfcReleaseItems.AsNoTracking()
                    join release in db.IfcReleases.AsNoTracking()
                        on item.IfcReleaseId equals release.Id
                    where ids.Contains(item.Id)
                    select new { item.Id, release.DesignProjectId })
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            DesignProjectResourceType.IfcReleaseRecipient => await (
                    from recipient in db.IfcReleaseRecipients.AsNoTracking()
                    join release in db.IfcReleases.AsNoTracking()
                        on recipient.IfcReleaseId equals release.Id
                    where ids.Contains(recipient.Id)
                    select new { recipient.Id, release.DesignProjectId })
                .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null),
        };
    }

    public async Task<string?> ResolveDesignDisciplineAsync(
        DesignProjectResourceType resourceType,
        int resourceId,
        CancellationToken ct = default) => resourceType switch
        {
            DesignProjectResourceType.BasicDesignDoc => await db.BasicDesignDocs.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => item.DisciplineCode)
                .SingleOrDefaultAsync(ct),
            DesignProjectResourceType.ShopDrawing => await db.ShopDrawings.AsNoTracking()
                .Where(item => item.Id == resourceId)
                .Select(item => item.DisciplineCode)
                .SingleOrDefaultAsync(ct),
            DesignProjectResourceType.DrawingRevision => await ResolveRevisionDisciplineAsync(resourceId, ct),
            _ => null,
        };

    public async Task<IReadOnlySet<string>?> GetAccessibleDesignDisciplinesAsync(
        int userId,
        int designProjectId,
        CancellationToken ct = default)
    {
        if (await HasAdministrativeBypassAsync(userId, ct)) return null;
        var project = await db.DesignProjects.AsNoTracking()
            .Where(project => project.Id == designProjectId)
            .Select(project => new
            {
                project.OperationalProjectId,
                OperationalProjectManagerUserId = project.OperationalProject != null
                    ? project.OperationalProject.ProjectManagerUserId
                    : null,
                OperationalProjectCreatedByUserId = project.OperationalProject != null
                    ? project.OperationalProject.CreatedByUserId
                    : null,
            })
            .SingleOrDefaultAsync(ct);
        if (project is null || !project.OperationalProjectId.HasValue) return null;
        if (project.OperationalProjectManagerUserId == userId ||
            project.OperationalProjectCreatedByUserId == userId) return null;

        var roles = await db.OperationalProjectMemberRoles.AsNoTracking()
            .Where(role => role.Member.OperationalProjectId == project.OperationalProjectId.Value &&
                role.Member.UserId == userId && role.Member.EndedAt == null && role.EndedAt == null)
            .Select(role => new { role.Scope, role.ScopeValue })
            .ToListAsync(ct);
        if (roles.Any(role => role.Scope == ProjectRoleScope.Project ||
            role.Scope == ProjectRoleScope.Module && role.ScopeValue == "Design")) return null;

        return roles
            .Where(role => role.Scope == ProjectRoleScope.Discipline && role.ScopeValue != null)
            .Select(role => role.ScopeValue!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> ResolveRevisionDisciplineAsync(int revisionId, CancellationToken ct)
    {
        var revision = await db.DrawingRevisions.AsNoTracking()
            .Where(item => item.Id == revisionId)
            .Select(item => new { item.TargetType, item.TargetId })
            .SingleOrDefaultAsync(ct);
        if (revision is null) return null;
        return revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc
            ? await ResolveDesignDisciplineAsync(DesignProjectResourceType.BasicDesignDoc, revision.TargetId, ct)
            : await ResolveDesignDisciplineAsync(DesignProjectResourceType.ShopDrawing, revision.TargetId, ct);
    }

    private async Task<IReadOnlyDictionary<int, int>> ResolveRevisionProjectIdsAsync(
        IReadOnlyCollection<int> revisionIds,
        CancellationToken ct)
    {
        var revisions = await db.DrawingRevisions.AsNoTracking()
            .Where(revision => revisionIds.Contains(revision.Id))
            .Select(revision => new { revision.Id, revision.TargetType, revision.TargetId })
            .ToListAsync(ct);
        var basicTargetIds = revisions
            .Where(revision => revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc)
            .Select(revision => revision.TargetId)
            .Distinct()
            .ToList();
        var shopTargetIds = revisions
            .Where(revision => revision.TargetType == DrawingRevisionTargetType.ShopDrawing)
            .Select(revision => revision.TargetId)
            .Distinct()
            .ToList();
        var basicProjects = await db.BasicDesignDocs.AsNoTracking()
            .Where(item => basicTargetIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct);
        var shopProjects = await db.ShopDrawings.AsNoTracking()
            .Where(item => shopTargetIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DesignProjectId, ct);

        return revisions
            .Select(revision => new
            {
                revision.Id,
                DesignProjectId = revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc
                    ? basicProjects.GetValueOrDefault(revision.TargetId)
                    : shopProjects.GetValueOrDefault(revision.TargetId),
            })
            .Where(item => item.DesignProjectId != 0)
            .ToDictionary(item => item.Id, item => item.DesignProjectId);
    }

    private async Task<bool> HasDesignRoleAsync(
        int userId,
        int designProjectId,
        string? disciplineCode,
        bool approvalOnly,
        CancellationToken ct)
    {
        if (await HasAdministrativeBypassAsync(userId, ct))
            return await db.DesignProjects.AsNoTracking().AnyAsync(project => project.Id == designProjectId, ct);

        var project = await db.DesignProjects.AsNoTracking()
            .Where(item => item.Id == designProjectId)
            .Select(item => new
            {
                item.OperationalProjectId,
                item.ProjectManagerUserId,
                item.DesignLeadUserId,
            })
            .SingleOrDefaultAsync(ct);
        if (project is null) return false;
        if (!project.OperationalProjectId.HasValue)
            return project.DesignLeadUserId == userId || (!approvalOnly && project.ProjectManagerUserId == userId);

        return await db.OperationalProjectMembers.AsNoTracking().AnyAsync(member =>
            member.OperationalProjectId == project.OperationalProjectId.Value &&
            member.UserId == userId && member.EndedAt == null &&
            member.Roles.Any(role => role.EndedAt == null &&
                (role.RoleCode == ProjectTeamRoleCode.DesignLead ||
                 (!approvalOnly && role.RoleCode != ProjectTeamRoleCode.Observer &&
                  role.RoleCode != ProjectTeamRoleCode.LegalOfficer &&
                  role.RoleCode != ProjectTeamRoleCode.SiteEngineer &&
                  role.RoleCode != ProjectTeamRoleCode.QuantitySurveyor)) &&
                (role.Scope == ProjectRoleScope.Project ||
                 role.Scope == ProjectRoleScope.Module && role.ScopeValue == "Design" ||
                 role.Scope == ProjectRoleScope.Discipline && disciplineCode != null &&
                 role.ScopeValue == disciplineCode)), ct);
    }
}
