namespace NihomeBackend.Services;

public interface ILegacyProjectTeamSyncService
{
    Task SyncOperationalProjectManagerAsync(
        int operationalProjectId,
        int? projectManagerUserId,
        int actorUserId,
        CancellationToken ct = default);

    Task SyncDesignProjectRolesAsync(
        int operationalProjectId,
        int? projectManagerUserId,
        int? designLeadUserId,
        int actorUserId,
        CancellationToken ct = default);
}