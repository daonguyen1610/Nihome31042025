using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services.HardDelete;

public sealed record ProjectHardDeletePlan(
    DeletionImpactResponse Impact,
    IReadOnlyList<HardDeleteItemDefinition> Items);

public interface IProjectHardDeletePlanService
{
    Task<ProjectHardDeletePlan?> ForDesignProjectAsync(int projectId, CancellationToken ct = default);
    Task<ProjectHardDeletePlan?> ForOperationalProjectAsync(int projectId, CancellationToken ct = default);
}

public sealed class ProjectHardDeletePlanService(
    AppDbContext db,
    IGoogleDriveSettingsStore settingsStore,
    IHardDeleteFileService files) : IProjectHardDeletePlanService
{
    public async Task<ProjectHardDeletePlan?> ForDesignProjectAsync(
        int projectId, CancellationToken ct = default)
    {
        var impact = await DeletionImpactPlanner.ForDesignProjectAsync(db, projectId, ct);
        if (impact is null) return null;
        var unmatchedReferenceCount = impact.Items
            .Where(item => item.Key == "design.filesPendingCleanup" &&
                item.Action == DeletionImpactActions.Block)
            .Sum(item => item.Count);

        var documentIds = await DesignDocumentScope.GetSidecarDocumentIdsAsync(db, projectId, ct);
        return await BuildAsync(impact, documentIds, includeFolders: false, unmatchedReferenceCount, ct);
    }

    public async Task<ProjectHardDeletePlan?> ForOperationalProjectAsync(
        int projectId, CancellationToken ct = default)
    {
        var impact = await DeletionImpactPlanner.ForOperationalProjectAsync(db, projectId, ct);
        if (impact is null) return null;

        var documentIds = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => item.Id)
            .ToListAsync(ct);
        return await BuildAsync(impact, documentIds, includeFolders: true, 0, ct);
    }

    private async Task<ProjectHardDeletePlan> BuildAsync(
        DeletionImpactResponse impact,
        IReadOnlyCollection<long> documentIds,
        bool includeFolders,
        int unmatchedReferenceCount,
        CancellationToken ct)
    {
        var options = await settingsStore.GetRuntimeAsync(ct);
        var blockers = new List<string>();
        blockers.AddRange(Enumerable.Range(1, unmatchedReferenceCount)
            .Select(index => $"unmatched-design-reference:{index}"));
        var identities = new List<string>();
        var definitions = new List<HardDeleteItemDefinition>();
        var sequence = 0;
        var documents = await db.ProjectDocuments.AsNoTracking()
            .Where(item => documentIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .ToListAsync(ct);

        foreach (var document in documents)
        {
            identities.Add(DocumentIdentity(document));
            if (!IsStableNiconDocument(document))
            {
                blockers.Add($"document:{document.Id}");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(document.LocalPath))
            {
                try
                {
                    var path = files.ValidateManagedPath(document.LocalPath);
                    definitions.Add(new HardDeleteItemDefinition(
                        HardDeleteItemKind.LocalFile, path, sequence++));
                }
                catch (HardDeleteFileException)
                {
                    blockers.Add($"local:{document.Id}:{document.LocalPath}");
                    continue;
                }
            }

            if (document.SyncStatus == ProjectDocumentSyncStatus.Deleted &&
                string.IsNullOrWhiteSpace(document.DriveFileId))
                continue;
            if (string.IsNullOrWhiteSpace(options.InstanceId) ||
                string.IsNullOrWhiteSpace(document.DriveFileId) ||
                string.IsNullOrWhiteSpace(document.DriveFolderId) ||
                document.Generation <= 0)
            {
                blockers.Add($"drive:{document.Id}");
                continue;
            }

            definitions.Add(new HardDeleteItemDefinition(
                HardDeleteItemKind.DriveFile,
                document.DriveFileId,
                sequence++,
                new Dictionary<string, string>
                {
                    ["niconInstance"] = options.InstanceId,
                    ["niconReplicaKey"] = $"project-document:{document.Id}",
                    ["niconGeneration"] = document.Generation.ToString(CultureInfo.InvariantCulture),
                },
                document.DriveFolderId));
        }

        if (includeFolders)
        {
            var folders = await db.ProjectDriveFolders.AsNoTracking()
                .Where(item => item.OperationalProjectId == impact.ResourceId)
                .OrderByDescending(item => options.Folders.SegmentsFor(item.Category).Count)
                .ThenBy(item => item.Id)
                .ToListAsync(ct);
            foreach (var folder in folders)
            {
                var path = TryFolderPath(options, folder.Category);
                identities.Add($"folder:{folder.Id}:{folder.Category}:{folder.DriveFolderId}:{path}");
                if (string.IsNullOrWhiteSpace(options.InstanceId) ||
                    string.IsNullOrWhiteSpace(folder.DriveFolderId) || string.IsNullOrWhiteSpace(path))
                {
                    blockers.Add($"folder:{folder.Id}");
                    continue;
                }
                definitions.Add(new HardDeleteItemDefinition(
                    HardDeleteItemKind.DriveFolder,
                    folder.DriveFolderId,
                    sequence++,
                    ProjectDriveFolderService.CreatePathIdentity(
                        options.InstanceId, impact.ResourceId, path)));
            }
        }

        var duplicateLocalPaths = definitions.Where(item => item.Kind == HardDeleteItemKind.LocalFile)
            .GroupBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var duplicateDriveIds = definitions.Where(item => item.Kind is HardDeleteItemKind.DriveFile or HardDeleteItemKind.DriveFolder)
            .GroupBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        blockers.AddRange(duplicateLocalPaths.Select(path => $"duplicate-local:{path}"));
        blockers.AddRange(duplicateDriveIds.Select(id => $"duplicate-drive:{id}"));
        definitions = definitions
            .Where(item => !duplicateLocalPaths.Contains(item.ActionIdentifier, StringComparer.Ordinal) &&
                !duplicateDriveIds.Contains(item.ActionIdentifier, StringComparer.Ordinal))
            .ToList();

        DeletionImpactPlanner.ApplyDurableFileEvidence(impact, identities, blockers, definitions);
        definitions.Add(new HardDeleteItemDefinition(
            HardDeleteItemKind.DatabaseAggregate, "delete-database-aggregate", sequence));
        return new ProjectHardDeletePlan(impact, definitions);
    }

    private static bool IsStableNiconDocument(ProjectDocument document) =>
        Enum.IsDefined(document.SourceType) &&
        document.SourceType != ProjectDocumentSourceType.GoogleDriveImport &&
        document.Origin == ProjectDocumentOrigin.Nicon &&
        document.ConflictState == ProjectDocumentConflictState.None &&
        document.SyncStatus is ProjectDocumentSyncStatus.Synced or ProjectDocumentSyncStatus.Deleted;

    private static string DocumentIdentity(ProjectDocument document) => string.Join(':',
        "document", document.Id, document.OperationalProjectId, document.SourceModule,
        document.SourceType, document.SourceEntityType, document.SourceSlot,
        document.SourceRecordId, document.LocalPath, document.Origin, document.Generation,
        document.SyncStatus, document.DesiredOperation, document.ConflictState,
        document.DriveFileId, document.DriveFolderId);

    private static string? TryFolderPath(GoogleDriveOptions options, ProjectDocumentCategory category)
    {
        if (!Enum.IsDefined(category) || category == ProjectDocumentCategory.Unclassified) return null;
        try
        {
            var segments = options.Folders.SegmentsFor(category);
            return segments.Count == 0 ? null : string.Join('/', segments);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

internal static class DesignDocumentScope
{
    public static async Task<IReadOnlyList<long>> GetSidecarDocumentIdsAsync(
        AppDbContext db, int designProjectId, CancellationToken ct)
    {
        var operationalProjectId = await db.DesignProjects.AsNoTracking()
            .Where(item => item.Id == designProjectId)
            .Select(item => item.OperationalProjectId)
            .SingleOrDefaultAsync(ct);
        if (!operationalProjectId.HasValue) return [];

        var sourceIds = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal)
        {
            [nameof(BasicDesignDoc)] = (await db.BasicDesignDocs.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
            [nameof(ShopDrawing)] = (await db.ShopDrawings.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
            [nameof(PermitChecklistItem)] = (await db.PermitChecklistItems.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
            [nameof(AcceptanceRecord)] = (await db.AcceptanceRecords.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
            [nameof(AsBuiltDocument)] = (await db.AsBuiltDocuments.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
            [nameof(HandoverRecord)] = (await db.HandoverRecords.AsNoTracking()
                .Where(item => item.DesignProjectId == designProjectId).Select(item => (long)item.Id).ToListAsync(ct)).ToHashSet(),
        };
        return await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.OperationalProjectId == operationalProjectId &&
                item.SourceType == ProjectDocumentSourceType.ExistingManagedFile &&
                item.SourceEntityType != null && item.SourceRecordId.HasValue)
            .Where(item => sourceIds.Keys.Contains(item.SourceEntityType!) &&
                sourceIds[item.SourceEntityType!].Contains(item.SourceRecordId!.Value))
            .Select(item => item.Id).ToListAsync(ct);
    }
}

public sealed class DesignProjectHardDeleteHandler(
    AppDbContext db,
    IProjectHardDeletePlanService plans,
    IProjectDocumentStagingService projectDocuments) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.DesignProject;

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var projectId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var project = await db.DesignProjects.SingleOrDefaultAsync(item => item.Id == projectId, ct);
        if (project is null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }
        var current = await plans.ForDesignProjectAsync(projectId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy Dự án thiết kế.");
        EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        try
        {
            await AggregateDeletionService.DeleteDesignProjectsAsync(
                db, [projectId], projectDocuments, userId, ct, stageExternalDeletes: false);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            DetachRolledBackDomainEntries(db);
            throw;
        }
    }

    internal static void EnsurePlan(string stored, string current)
    {
        if (!string.Equals(stored, current, StringComparison.Ordinal))
            throw new DeletionPlanChangedException(
                "Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
    }

    internal static void DetachRolledBackDomainEntries(AppDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(entry =>
            entry.Entity is not HardDeleteOperation && entry.Entity is not HardDeleteItem).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}

public sealed class OperationalProjectHardDeleteHandler(
    AppDbContext db,
    IProjectHardDeletePlanService plans,
    IProjectDocumentStagingService projectDocuments) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.OperationalProject;

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var projectId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var userId))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var project = await db.OperationalProjects.SingleOrDefaultAsync(item => item.Id == projectId, ct);
        if (project is null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }
        var current = await plans.ForOperationalProjectAsync(projectId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy Dự án vận hành.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        try
        {
            await AggregateDeletionService.DeleteOperationalProjectAsync(
                db, project, projectDocuments, userId, ct, stageExternalDeletes: false);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            DesignProjectHardDeleteHandler.DetachRolledBackDomainEntries(db);
            throw;
        }
    }
}