using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services.HardDelete;

public sealed record ProjectHardDeletePlan(
    DeletionImpactResponse Impact,
    IReadOnlyList<HardDeleteItemDefinition> Items);

public interface IProjectHardDeletePlanService
{
    Task<ProjectHardDeletePlan?> ForDesignProjectAsync(int projectId, CancellationToken ct = default);
    Task<ProjectHardDeletePlan?> ForOperationalProjectAsync(int projectId, CancellationToken ct = default);
}

public sealed class ProjectHardDeletePlanService(AppDbContext db) : IProjectHardDeletePlanService
{
    public async Task<ProjectHardDeletePlan?> ForDesignProjectAsync(
        int projectId, CancellationToken ct = default)
    {
        var impact = await DeletionImpactPlanner.ForDesignProjectAsync(db, projectId, ct);
        if (impact is null) return null;
        var references = await DeletionImpactPlanner.GetDesignManagedFileReferencesAsync(db, [projectId], ct);
        var documentIds = (await DesignDocumentScope.GetSidecarDocumentIdsAsync(db, projectId, ct)).ToList();
        var operationalProjectId = await db.DesignProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => item.OperationalProjectId)
            .SingleAsync(ct);
        if (operationalProjectId.HasValue)
        {
            documentIds.AddRange(await db.ProjectDocuments.AsNoTracking()
                .Where(item => item.OperationalProjectId == operationalProjectId &&
                    item.SourceType == ProjectDocumentSourceType.ExistingManagedFile &&
                    item.DesiredOperation == ProjectDocumentDesiredOperation.Delete &&
                    item.SyncStatus != ProjectDocumentSyncStatus.Deleted)
                .Select(item => item.Id)
                .ToListAsync(ct));
        }
        return await BuildAsync(impact, documentIds, [], references, includeFolders: false, ct);
    }

    public async Task<ProjectHardDeletePlan?> ForOperationalProjectAsync(
        int projectId, CancellationToken ct = default)
    {
        var impact = await DeletionImpactPlanner.ForOperationalProjectAsync(db, projectId, ct);
        if (impact is null) return null;

        var projectDocuments = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .ToListAsync(ct);
        var designProjectIds = await db.DesignProjects.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var references = await DeletionImpactPlanner.GetDesignManagedFileReferencesAsync(
            db, designProjectIds, ct);
        var designSidecarIds = await DesignDocumentScope.GetSidecarDocumentIdsAsync(
            db, designProjectIds, ct);
        var designSidecarIdSet = designSidecarIds.ToHashSet();
        var preservedDocumentIds = projectDocuments
            .Where(item => IsIndependentCrmDocument(item) && !designSidecarIdSet.Contains(item.Id))
            .Select(item => item.Id)
            .ToList();
        var documentIds = projectDocuments
            .Where(item => !IsIndependentCrmDocument(item))
            .Select(item => item.Id)
            .Concat(designSidecarIds)
            .Distinct()
            .ToList();
        return await BuildAsync(
            impact, documentIds, preservedDocumentIds, references, includeFolders: true, ct);
    }

    private async Task<ProjectHardDeletePlan> BuildAsync(
        DeletionImpactResponse impact,
        IReadOnlyCollection<long> documentIds,
        IReadOnlyCollection<long> preservedDocumentIds,
        IReadOnlyCollection<DeletionImpactPlanner.DesignManagedFileReference> designReferences,
        bool includeFolders,
        CancellationToken ct)
    {
        var identities = new List<string>();
        var documents = await db.ProjectDocuments.AsNoTracking()
            .Where(item => documentIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .ToListAsync(ct);
        identities.AddRange((await db.ProjectDocuments.AsNoTracking()
                .Where(item => preservedDocumentIds.Contains(item.Id))
                .OrderBy(item => item.Id)
                .ToListAsync(ct))
            .Select(item => $"preserved:{DocumentIdentity(item)}"));
        identities.AddRange(designReferences.Select(reference => reference.Identifier));
        identities.AddRange(documents.Select(DocumentIdentity));

        var referenceRecords = designReferences.Select(reference =>
                $"{reference.SourceEntityType}:{reference.SourceRecordId}")
            .ToHashSet(StringComparer.Ordinal);
        var directDocuments = documents.Where(document =>
                document.SyncStatus != ProjectDocumentSyncStatus.Deleted &&
                !(document.SourceEntityType is not null && document.SourceRecordId.HasValue &&
                    referenceRecords.Contains($"{document.SourceEntityType}:{document.SourceRecordId}")))
            .ToList();
        var blockers = designReferences.Select(reference => reference.Identifier)
            .Concat(directDocuments.Select(document => $"project-document:{document.Id}"))
            .ToList();
        var resolutionLinks = designReferences.Select(ManualResolutionLink)
            .Concat(directDocuments.Select(document => new DeletionImpactLinkResponse
            {
                Label = document.OriginalFileName,
                Url = $"/admin/operational-projects/{document.OperationalProjectId}#project-documents",
            }))
            .GroupBy(link => link.Url, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(link => link.Url, StringComparer.Ordinal)
            .ToList();

        if (includeFolders)
        {
            identities.AddRange((await db.ProjectDriveFolders.AsNoTracking()
                .Where(item => item.OperationalProjectId == impact.ResourceId)
                .OrderBy(item => item.Id)
                .ToListAsync(ct))
                .Select(folder =>
                    $"preserved-folder:{folder.Id}:{folder.Category}:{folder.DriveFolderId}"));

            var surveyMediaLinks = await db.SurveyMedia.AsNoTracking()
                .Where(item => item.Survey.OperationalProjectId == impact.ResourceId)
                .Select(item => new DeletionImpactLinkResponse
                {
                    Label = item.OriginalFileName,
                    Url = $"/admin/surveys/{item.SurveyId}",
                })
                .ToListAsync(ct);
            var surveyLinks = await db.Surveys.AsNoTracking()
                .Where(item => item.OperationalProjectId == impact.ResourceId && item.DriveFolderId != null)
                .Select(item => new DeletionImpactLinkResponse
                {
                    Label = item.Code,
                    Url = $"/admin/surveys/{item.Id}",
                })
                .ToListAsync(ct);
            SetResolutionLinks(impact, "operations.surveyMedia", surveyMediaLinks);
            SetResolutionLinks(impact, "operations.surveyDriveFolders", surveyLinks);
        }

        DeletionImpactPlanner.ApplyDurableFileEvidence(impact, identities, blockers, []);
        var blocker = impact.Items.SingleOrDefault(item => item.Key is
            "design.filesPendingCleanup" or "operations.pendingDocuments");
        if (blocker is not null)
            blocker.ResolutionLinks = resolutionLinks;
        return new ProjectHardDeletePlan(impact,
        [
            new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, "delete-database-aggregate", 0),
        ]);
    }

    private static void SetResolutionLinks(
        DeletionImpactResponse impact,
        string key,
        IReadOnlyCollection<DeletionImpactLinkResponse> links)
    {
        var item = impact.Items.SingleOrDefault(candidate => candidate.Key == key);
        if (item is not null) item.ResolutionLinks = links.ToList();
    }

    private static DeletionImpactLinkResponse ManualResolutionLink(
        DeletionImpactPlanner.DesignManagedFileReference reference) => new()
        {
            Label = reference.Label,
            Url = reference.SourceEntityType switch
            {
                nameof(BasicDesignDoc) => $"/admin/design-projects/{reference.DesignProjectId}?tab=basic",
                nameof(ShopDrawing) => $"/admin/design-projects/{reference.DesignProjectId}?tab=shop",
                nameof(PermitChecklistItem) => $"/admin/permits?designProjectId={reference.DesignProjectId}",
                nameof(AcceptanceRecord) => $"/admin/construction/acceptance?designProjectId={reference.DesignProjectId}",
                nameof(AsBuiltDocument) => $"/admin/construction/asbuilt?designProjectId={reference.DesignProjectId}",
                nameof(HandoverRecord) => $"/admin/construction/handover?designProjectId={reference.DesignProjectId}",
                _ => $"/admin/design-projects/{reference.DesignProjectId}?tab=docs",
            },
        };

    private static bool IsIndependentCrmDocument(ProjectDocument document) =>
        document.SourceModule == ProjectDocumentSourceModule.Crm ||
        document.CustomerId.HasValue ||
        document.ContractId.HasValue ||
        document.SourceEntityType is nameof(QuoteDocument) or nameof(ContractAttachment) or
            nameof(ContractAppendix);

    private static string DocumentIdentity(ProjectDocument document) => string.Join(':',
        "document", document.Id, document.OperationalProjectId, document.SourceModule,
        document.SourceType, document.SourceEntityType, document.SourceSlot,
        document.SourceRecordId, document.LocalPath, document.Origin, document.Generation,
        document.SyncStatus, document.DesiredOperation, document.ConflictState,
        document.DriveFileId, document.DriveFolderId);

}

internal static class DesignDocumentScope
{
    public static async Task<IReadOnlyList<long>> GetSidecarDocumentIdsAsync(
        AppDbContext db, int designProjectId, CancellationToken ct) =>
        await GetSidecarDocumentIdsAsync(db, [designProjectId], ct);

    public static async Task<IReadOnlyList<long>> GetSidecarDocumentIdsAsync(
        AppDbContext db, IReadOnlyCollection<int> designProjectIds, CancellationToken ct)
    {
        var references = await DeletionImpactPlanner.GetDesignManagedFileReferencesAsync(
            db, designProjectIds, ct);
        var referenceRecords = references.Select(reference =>
            $"{reference.SourceEntityType}:{reference.SourceRecordId}")
            .ToHashSet(StringComparer.Ordinal);
        if (referenceRecords.Count == 0) return [];
        var candidates = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.SourceEntityType != null && item.SourceRecordId.HasValue)
            .Select(item => new
            {
                item.Id,
                item.SourceEntityType,
                item.SourceRecordId,
            })
            .ToListAsync(ct);
        return candidates.Where(item => referenceRecords.Contains(
            $"{item.SourceEntityType}:{item.SourceRecordId!.Value}"))
            .Select(item => item.Id)
            .ToList();
    }
}

public sealed class DesignProjectHardDeleteHandler(
    AppDbContext db,
    IProjectHardDeletePlanService plans,
    IProjectDocumentStagingService projectDocuments,
    IPermissionService permissions,
    IProjectAccessService projectAccess) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.DesignProject;

    public async Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var projectId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteAuthorizationException("Thông tin phân quyền tác vụ xóa không hợp lệ.");
        if (!await permissions.HasAsync(requestedBy, "design.projects.manage", ct))
            throw ProjectAuthorizationChanged();
        var exists = await db.DesignProjects.AsNoTracking().AnyAsync(item => item.Id == projectId, ct);
        if (!exists && context.IsForwardRecovery) return;
        if (!exists || !await projectAccess.CanManageDesignProjectAsync(requestedBy, projectId, ct))
            throw ProjectAuthorizationChanged();
        var current = await plans.ForDesignProjectAsync(projectId, ct)
            ?? throw PlanChanged();
        EnsurePlan(context.PlanToken, current.Impact.PlanToken);
    }

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

    private static HardDeleteAuthorizationException ProjectAuthorizationChanged() =>
        new("Quyền xóa dự án hoặc phạm vi dự án đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");

    private static DeletionPlanChangedException PlanChanged() =>
        new("Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
}

public sealed class OperationalProjectHardDeleteHandler(
    AppDbContext db,
    IProjectHardDeletePlanService plans,
    IProjectDocumentStagingService projectDocuments,
    IPermissionService permissions) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.OperationalProject;

    public async Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var projectId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteAuthorizationException("Thông tin phân quyền tác vụ xóa không hợp lệ.");
        if (!await permissions.HasAsync(requestedBy, "operations.projects.manage", ct))
            throw ProjectAuthorizationChanged();
        var canSeeAll = await permissions.HasAsync(requestedBy, "operations.projects.view.all", ct);
        var exists = await db.OperationalProjects.AsNoTracking().AnyAsync(project => project.Id == projectId, ct);
        if (!exists && context.IsForwardRecovery) return;
        var canManage = exists && await db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId &&
            (canSeeAll || project.ProjectManagerUserId == requestedBy || project.CreatedByUserId == requestedBy), ct);
        if (!canManage) throw ProjectAuthorizationChanged();
        var current = await plans.ForOperationalProjectAsync(projectId, ct)
            ?? throw PlanChanged();
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
    }

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

    private static HardDeleteAuthorizationException ProjectAuthorizationChanged() =>
        new("Quyền xóa dự án hoặc phạm vi dự án đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");

    private static DeletionPlanChangedException PlanChanged() =>
        new("Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
}