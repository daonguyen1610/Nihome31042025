using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using System.Text.Json;

namespace NihomeBackend.Services;

public sealed class LegacyProjectTeamSyncService(AppDbContext db) : ILegacyProjectTeamSyncService
{
    public const string ManualSource = "Manual";
    public const string RuntimeSource = "LegacyDualWrite";
    public const string BackfillSource = "LegacyBackfill";
    public const string OperationalProjectManagerReference = "OperationalProject.ProjectManagerUserId";
    public const string DesignProjectManagerReference = "DesignProject.ProjectManagerUserId";
    public const string DesignLeadReference = "DesignProject.DesignLeadUserId";

    public Task SyncOperationalProjectManagerAsync(
        int operationalProjectId,
        int? projectManagerUserId,
        int actorUserId,
        CancellationToken ct = default) => SyncAsync(
            operationalProjectId,
            [
                new LegacyRole(
                    projectManagerUserId,
                    ProjectTeamRoleCode.ProjectManager,
                    ProjectRoleScope.Project,
                    null,
                    "Project Manager",
                    OperationalProjectManagerReference),
            ],
            actorUserId,
            ct);

    public async Task SyncDesignProjectRolesAsync(
        int operationalProjectId,
        int? projectManagerUserId,
        int? designLeadUserId,
        int actorUserId,
        CancellationToken ct = default,
        int? designProjectId = null)
    {
        IReadOnlyCollection<LegacyRole> desired =
            [
                new LegacyRole(
                    projectManagerUserId,
                    ProjectTeamRoleCode.ProjectManager,
                    ProjectRoleScope.Module,
                    "Design",
                    "Design Project Manager",
                    DesignProjectManagerReference),
                new LegacyRole(
                    designLeadUserId,
                    ProjectTeamRoleCode.DesignLead,
                    ProjectRoleScope.Module,
                    "Design",
                    "Design Lead",
                    DesignLeadReference),
            ];
        await SyncAsync(operationalProjectId, desired, actorUserId, ct, designProjectId);
    }

    private async Task SyncAsync(
        int operationalProjectId,
        IReadOnlyCollection<LegacyRole> desiredRoles,
        int actorUserId,
        CancellationToken ct,
        int? designProjectId = null)
    {
        var members = await db.OperationalProjectMembers
            .Include(member => member.Roles)
            .Where(member => member.OperationalProjectId == operationalProjectId && member.EndedAt == null &&
                member.DesignProjectId == designProjectId)
            .ToListAsync(ct);
        var references = desiredRoles.Select(role => role.SourceReference).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var requestedUserIds = desiredRoles
            .Where(role => role.UserId.HasValue)
            .Select(role => role.UserId!.Value)
            .Distinct()
            .ToList();
        var activeUserIds = (await db.Users.AsNoTracking()
            .Where(user => requestedUserIds.Contains(user.Id) && user.IsActive)
            .Select(user => user.Id)
            .ToListAsync(ct))
            .ToHashSet();
        var changed = false;

        foreach (var role in members.SelectMany(member => member.Roles).Where(role =>
                     role.EndedAt == null &&
                     (references.Contains(role.SourceReference ?? string.Empty) &&
                      role.Source is RuntimeSource or BackfillSource ||
                      designProjectId.HasValue && role.Source == ManualSource &&
                      role.Scope == ProjectRoleScope.Module && role.ScopeValue == "Design" &&
                      role.RoleCode is ProjectTeamRoleCode.ProjectManager or ProjectTeamRoleCode.DesignLead)))
        {
            if (!desiredRoles.Any(desired =>
                (desired.SourceReference == role.SourceReference ||
                 designProjectId.HasValue && role.Source == ManualSource) &&
                desired.UserId == role.Member.UserId && desired.UserId.HasValue &&
                activeUserIds.Contains(desired.UserId.Value) && Matches(role, desired)))
            {
                role.EndedAt = now;
                changed = true;
            }
        }

        foreach (var desired in desiredRoles.Where(role =>
                     role.UserId.HasValue && activeUserIds.Contains(role.UserId.Value)))
        {
            var member = members.SingleOrDefault(item => item.UserId == desired.UserId);
            if (member is null)
            {
                member = new OperationalProjectMember
                {
                    OperationalProjectId = operationalProjectId,
                    DesignProjectId = designProjectId,
                    UserId = desired.UserId!.Value,
                    Position = desired.Position,
                    StartedAt = now,
                    Source = RuntimeSource,
                    SourceReference = desired.SourceReference,
                    CreatedAt = now,
                    CreatedByUserId = actorUserId,
                    UpdatedAt = now,
                    UpdatedByUserId = actorUserId,
                };
                db.OperationalProjectMembers.Add(member);
                members.Add(member);
                changed = true;
            }

            if (member.Roles.Any(role => role.EndedAt == null && Matches(role, desired)))
            {
                continue;
            }

            member.Roles.Add(new OperationalProjectMemberRole
            {
                RoleCode = desired.RoleCode,
                Scope = desired.Scope,
                ScopeValue = desired.ScopeValue,
                Source = RuntimeSource,
                SourceReference = desired.SourceReference,
                StartedAt = now,
            });
            member.UpdatedAt = now;
            member.UpdatedByUserId = actorUserId;
            changed = true;
        }

        foreach (var member in members.Where(member =>
                     member.Source is RuntimeSource or BackfillSource &&
                     member.Roles.All(role => role.EndedAt.HasValue)))
        {
            member.EndedAt = now;
            member.UpdatedAt = now;
            member.UpdatedByUserId = actorUserId;
            changed = true;
        }

        if (changed)
        {
            db.OperationalProjectTeamHistory.Add(new OperationalProjectTeamHistory
            {
                OperationalProjectId = operationalProjectId,
                EntityType = "LegacyTeamSync",
                EntityId = operationalProjectId,
                Action = "Synchronized",
                SnapshotJson = JsonSerializer.Serialize(desiredRoles.Select(role => new
                {
                    role.UserId,
                    RoleCode = role.RoleCode.ToString(),
                    Scope = role.Scope.ToString(),
                    role.ScopeValue,
                    role.SourceReference,
                })),
                ChangedAt = now,
                ChangedByUserId = actorUserId,
            });
        }
    }

    private static bool Matches(OperationalProjectMemberRole role, LegacyRole desired) =>
        role.RoleCode == desired.RoleCode &&
        role.Scope == desired.Scope &&
        string.Equals(role.ScopeValue, desired.ScopeValue, StringComparison.OrdinalIgnoreCase);

    private sealed record LegacyRole(
        int? UserId,
        ProjectTeamRoleCode RoleCode,
        ProjectRoleScope Scope,
        string? ScopeValue,
        string Position,
        string SourceReference);
}
