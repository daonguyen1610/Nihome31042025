using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.HardDelete;

namespace NihomeBackend.Services;

internal static class DeletionImpactPlanner
{
    internal static void ApplyDurableFileEvidence(
        DeletionImpactResponse impact,
        IReadOnlyCollection<string> identities,
        IReadOnlyCollection<string> blockers,
        IReadOnlyCollection<HardDeleteItemDefinition> definitions)
    {
        impact.Items.RemoveAll(item => item.Key is "design.filesPendingCleanup" or
            "operations.pendingDocuments" or "operations.driveFolders");
        var deleteCount = definitions.Count(item => item.Kind != HardDeleteItemKind.DatabaseAggregate);
        if (deleteCount > 0)
            impact.Items.Add(new DeletionImpactItemResponse
            {
                Key = "hardDelete.managedExternalItems",
                Action = DeletionImpactActions.Delete,
                Count = deleteCount,
                Examples = definitions.Select(item => item.ActionIdentifier).Take(3).ToList(),
            });
        if (blockers.Count > 0)
            impact.Items.Add(new DeletionImpactItemResponse
            {
                Key = impact.ResourceType == EntityTypes.DesignProject
                    ? "design.filesPendingCleanup"
                    : "operations.pendingDocuments",
                Action = DeletionImpactActions.Block,
                Count = blockers.Count,
                Examples = blockers.Take(3).ToList(),
            });
        impact.Items = impact.Items.OrderBy(item => item.Key, StringComparer.Ordinal).ToList();
        impact.CanDelete = impact.Items.All(item => item.Action != DeletionImpactActions.Block);
        impact.TotalAffected = 1 + impact.Items.Sum(item => item.Count);
        var source = string.Join('|', identities.OrderBy(item => item, StringComparer.Ordinal));
        impact.PlanToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{impact.PlanToken}:{source}"))).ToLowerInvariant();
    }

    public static async Task<DeletionImpactResponse?> ForDesignProjectAsync(
        AppDbContext db, int projectId, CancellationToken ct)
    {
        var project = await db.DesignProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.Id, item.ProjectCode, item.Name })
            .SingleOrDefaultAsync(ct);
        if (project is null) return null;

        var items = new List<PlanItem>();
        await AddDesignDependenciesAsync(db, [projectId], items, ct);

        return Build("DesignProject", project.Id, $"{project.ProjectCode} · {project.Name}",
            project.ProjectCode, items);
    }

    private static async Task AddDesignDependenciesAsync(
        AppDbContext db,
        IReadOnlyCollection<int> projectIds,
        List<PlanItem> items,
        CancellationToken ct)
    {
        if (projectIds.Count == 0) return;

        await AddAsync(items, "design.conceptOptions", DeletionImpactActions.Delete,
            db.ConceptOptions.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Name, ct);
        await AddAsync(items, "design.permits", DeletionImpactActions.Delete,
            db.PermitChecklistItems.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.PermitTypeCode, ct);
        await AddAsync(items, "design.basicDocuments", DeletionImpactActions.Delete,
            db.BasicDesignDocs.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "design.shopDrawings", DeletionImpactActions.Delete,
            db.ShopDrawings.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "design.ifcReleases", DeletionImpactActions.Delete,
            db.IfcReleases.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.ReleaseNumber, ct);
        await AddAsync(items, "design.constructionTasks", DeletionImpactActions.Delete,
            db.ConstructionTasks.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Name, ct);
        await AddAsync(items, "design.siteDiaries", DeletionImpactActions.Delete,
            db.SiteDiaries.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.WorkPerformed, ct);
        await AddAsync(items, "design.punchItems", DeletionImpactActions.Delete,
            db.PunchItems.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "design.acceptanceRecords", DeletionImpactActions.Delete,
            db.AcceptanceRecords.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "design.asBuiltDocuments", DeletionImpactActions.Delete,
            db.AsBuiltDocuments.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "design.handoverRecords", DeletionImpactActions.Delete,
            db.HandoverRecords.Where(item => projectIds.Contains(item.DesignProjectId)), item => item.Id,
            item => item.Title, ct);

        var basicDocumentIds = db.BasicDesignDocs
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);
        var conceptOptionIds = db.ConceptOptions
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);
        var shopDrawingIds = db.ShopDrawings
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);
        var ifcReleaseIds = db.IfcReleases
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);
        var constructionTaskIds = db.ConstructionTasks
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);
        var handoverRecordIds = db.HandoverRecords
            .Where(item => projectIds.Contains(item.DesignProjectId))
            .Select(item => item.Id);

        await AddAsync(items, "design.drawingRevisions", DeletionImpactActions.Delete,
            db.DrawingRevisions.Where(item =>
                item.TargetType == DrawingRevisionTargetType.BasicDesignDoc && basicDocumentIds.Contains(item.TargetId) ||
                item.TargetType == DrawingRevisionTargetType.ShopDrawing && shopDrawingIds.Contains(item.TargetId)),
            item => item.Id, item => item.Note, ct);
        await AddAsync(items, "design.ifcReleaseItems", DeletionImpactActions.Delete,
            db.IfcReleaseItems.Where(item => ifcReleaseIds.Contains(item.IfcReleaseId)),
            item => item.Id, item => item.ShopDrawing.DrawingCode, ct);
        await AddAsync(items, "design.ifcRecipients", DeletionImpactActions.Delete,
            db.IfcReleaseRecipients.Where(item => ifcReleaseIds.Contains(item.IfcReleaseId)),
            item => item.Id, item => item.Name, ct);
        await AddAsync(items, "design.taskDependencies", DeletionImpactActions.Delete,
            db.ConstructionTaskDependencies.Where(item => constructionTaskIds.Contains(item.TaskId) ||
                constructionTaskIds.Contains(item.PredecessorTaskId)),
            item => item.Id, item => item.Task.Name, ct);
        await AddAsync(items, "design.handoverHistory", DeletionImpactActions.Delete,
            db.HandoverStatusHistory.Where(item => handoverRecordIds.Contains(item.HandoverRecordId)),
            item => item.Id, item => item.Note!, ct);

        await AddDesignFileBlockersAsync(db, projectIds, items, ct);
        await AddTranslationsAsync(items, db, EntityTypes.DesignProject,
            db.DesignProjects.Where(item => projectIds.Contains(item.Id)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.ConceptOption, conceptOptionIds, ct);
        await AddTranslationsAsync(items, db, EntityTypes.PermitChecklistItem,
            db.PermitChecklistItems.Where(item => projectIds.Contains(item.DesignProjectId)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.BasicDesignDoc, basicDocumentIds, ct);
        await AddTranslationsAsync(items, db, EntityTypes.ShopDrawing, shopDrawingIds, ct);
        await AddTranslationsAsync(items, db, EntityTypes.DrawingRevision,
            db.DrawingRevisions.Where(item =>
                item.TargetType == DrawingRevisionTargetType.BasicDesignDoc && basicDocumentIds.Contains(item.TargetId) ||
                item.TargetType == DrawingRevisionTargetType.ShopDrawing && shopDrawingIds.Contains(item.TargetId))
                .Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.IfcRelease, ifcReleaseIds, ct);
        await AddTranslationsAsync(items, db, EntityTypes.ConstructionTask, constructionTaskIds, ct);
        await AddTranslationsAsync(items, db, EntityTypes.SiteDiary,
            db.SiteDiaries.Where(item => projectIds.Contains(item.DesignProjectId)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.PunchItem,
            db.PunchItems.Where(item => projectIds.Contains(item.DesignProjectId)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.AcceptanceRecord,
            db.AcceptanceRecords.Where(item => projectIds.Contains(item.DesignProjectId)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.AsBuiltDocument,
            db.AsBuiltDocuments.Where(item => projectIds.Contains(item.DesignProjectId)).Select(item => item.Id), ct);
        await AddTranslationsAsync(items, db, EntityTypes.HandoverRecord, handoverRecordIds, ct);
    }

    private static async Task AddDesignFileBlockersAsync(
        AppDbContext db,
        IReadOnlyCollection<int> projectIds,
        List<PlanItem> items,
        CancellationToken ct)
    {
        var projects = await db.DesignProjects.AsNoTracking()
            .Where(item => projectIds.Contains(item.Id))
            .Select(item => new { item.Id, item.OperationalProjectId })
            .ToListAsync(ct);
        var operationalProjectByDesign = projects
            .Where(item => item.OperationalProjectId.HasValue)
            .ToDictionary(item => item.Id, item => item.OperationalProjectId!.Value);
        var references = new List<ManagedFileReference>();

        var basicDocuments = await db.BasicDesignDocs.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) && item.FilePath != null)
            .Select(item => new { item.Id, item.DesignProjectId, item.FilePath })
            .ToListAsync(ct);
        references.AddRange(basicDocuments.Select(item => new ManagedFileReference(
            item.DesignProjectId, ProjectDocumentSourceModule.Design, nameof(BasicDesignDoc),
            "file", item.Id, item.FilePath!, item.FilePath!)));

        var shopDrawings = await db.ShopDrawings.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) && item.FilePath != null)
            .Select(item => new { item.Id, item.DesignProjectId, item.FilePath })
            .ToListAsync(ct);
        references.AddRange(shopDrawings.Select(item => new ManagedFileReference(
            item.DesignProjectId, ProjectDocumentSourceModule.Design, nameof(ShopDrawing),
            "file", item.Id, item.FilePath!, item.FilePath!)));

        var permits = await db.PermitChecklistItems.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) &&
                (item.SubmittedFilePath != null || item.IssuedFilePath != null))
            .Select(item => new
            {
                item.Id,
                item.DesignProjectId,
                item.PermitTypeCode,
                item.SubmittedFilePath,
                item.IssuedFilePath
            })
            .ToListAsync(ct);
        foreach (var permit in permits)
        {
            if (!string.IsNullOrWhiteSpace(permit.SubmittedFilePath))
                references.Add(new ManagedFileReference(permit.DesignProjectId,
                    ProjectDocumentSourceModule.Design, nameof(PermitChecklistItem), "submittedPackage",
                    permit.Id, permit.SubmittedFilePath, permit.PermitTypeCode));
            if (!string.IsNullOrWhiteSpace(permit.IssuedFilePath))
                references.Add(new ManagedFileReference(permit.DesignProjectId,
                    ProjectDocumentSourceModule.Design, nameof(PermitChecklistItem), "issuedPermit",
                    permit.Id, permit.IssuedFilePath, permit.PermitTypeCode));
        }

        var acceptances = await db.AcceptanceRecords.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) && item.Documents != null)
            .Select(item => new { item.Id, item.DesignProjectId, item.Title, item.Documents })
            .ToListAsync(ct);
        foreach (var acceptance in acceptances)
            foreach (var path in DeserializeManagedPaths(acceptance.Documents,
                         "/files/business-documents/acceptance/"))
                references.Add(new ManagedFileReference(acceptance.DesignProjectId,
                    ProjectDocumentSourceModule.Acceptance, nameof(AcceptanceRecord), "documents",
                    acceptance.Id, path, acceptance.Title));

        var asBuiltDocuments = await db.AsBuiltDocuments.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) && item.FileUrl != null &&
                item.FileUrl.StartsWith("/files/business-documents/as-built/"))
            .Select(item => new { item.Id, item.DesignProjectId, item.Title, item.FileUrl })
            .ToListAsync(ct);
        references.AddRange(asBuiltDocuments.Select(item => new ManagedFileReference(
            item.DesignProjectId, ProjectDocumentSourceModule.Acceptance, nameof(AsBuiltDocument),
            "file", item.Id, item.FileUrl!, item.Title)));

        var handovers = await db.HandoverRecords.AsNoTracking()
            .Where(item => projectIds.Contains(item.DesignProjectId) && item.Documents != null)
            .Select(item => new { item.Id, item.DesignProjectId, item.Title, item.Documents })
            .ToListAsync(ct);
        foreach (var handover in handovers)
            foreach (var path in DeserializeManagedPaths(handover.Documents,
                         "/files/business-documents/handover/"))
                references.Add(new ManagedFileReference(handover.DesignProjectId,
                    ProjectDocumentSourceModule.Handover, nameof(HandoverRecord), "documents",
                    handover.Id, path, handover.Title));

        var linkedProjectIds = operationalProjectByDesign.Values.Distinct().ToList();
        var sidecars = await db.ProjectDocuments.AsNoTracking()
            .Where(item => linkedProjectIds.Contains(item.OperationalProjectId) &&
                item.SourceType == ProjectDocumentSourceType.ExistingManagedFile)
            .Select(item => new
            {
                item.OperationalProjectId,
                item.SourceModule,
                item.SourceEntityType,
                item.SourceSlot,
                item.SourceRecordId,
                item.LocalPath,
                item.SyncStatus
            })
            .ToListAsync(ct);
        var sidecarStatus = sidecars.ToDictionary(
            item => ManagedFileKey(item.OperationalProjectId, item.SourceModule, item.SourceEntityType!,
                item.SourceSlot!, item.SourceRecordId!.Value, item.LocalPath),
            item => item.SyncStatus,
            StringComparer.Ordinal);
        var blockers = references.Where(reference =>
        {
            if (!operationalProjectByDesign.TryGetValue(reference.DesignProjectId, out var projectId)) return true;
            return !sidecarStatus.TryGetValue(ManagedFileKey(projectId, reference.SourceModule,
                       reference.SourceEntityType, reference.SourceSlot, reference.SourceRecordId,
                       reference.LocalPath), out var status) || status == ProjectDocumentSyncStatus.Processing;
        }).ToList();
        AddRaw(items, "design.filesPendingCleanup", DeletionImpactActions.Block,
            blockers.Select(reference => reference.Identifier), blockers.Select(reference => reference.Label));
    }

    private static string ManagedFileKey(
        int projectId,
        ProjectDocumentSourceModule sourceModule,
        string sourceEntityType,
        string sourceSlot,
        long sourceRecordId,
        string localPath) =>
        $"{projectId}:{sourceModule}:{sourceEntityType}:{sourceSlot}:{sourceRecordId}:{localPath.Trim()}";

    private static IEnumerable<string> DeserializeManagedPaths(string? json, string prefix)
    {
        if (string.IsNullOrWhiteSpace(json)) yield break;
        List<string>? paths;
        try { paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json); }
        catch (System.Text.Json.JsonException) { yield break; }
        if (paths is null) yield break;
        foreach (var path in paths.Select(value => value?.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)))
            if (path!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) yield return path;
    }

    public static async Task<DeletionImpactResponse?> ForOperationalProjectAsync(
        AppDbContext db, int projectId, CancellationToken ct)
    {
        var project = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.Id, item.Code, item.Name })
            .SingleOrDefaultAsync(ct);
        if (project is null) return null;

        var items = new List<PlanItem>();
        var designProjectIds = await db.DesignProjects.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .Select(item => item.Id)
            .ToListAsync(ct);
        await AddAsync(items, "operations.designProjects", DeletionImpactActions.Delete,
            db.DesignProjects.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.ProjectCode, ct);
        await AddDesignDependenciesAsync(db, designProjectIds, items, ct);
        await AddAsync(items, "operations.surveys", DeletionImpactActions.Delete,
            db.Surveys.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Code, ct);
        await AddAsync(items, "operations.surveyChecklistResults", DeletionImpactActions.Delete,
            db.SurveyChecklistResults.Where(item => item.Survey.OperationalProjectId == projectId), item => item.Id,
            item => item.TemplateTitle, ct);
        await AddAsync(items, "operations.surveySiteConditions", DeletionImpactActions.Delete,
            db.SurveySiteConditions.Where(item => item.Survey.OperationalProjectId == projectId), item => item.Id,
            item => item.Code, ct);
        await AddAsync(items, "operations.teamMembers", DeletionImpactActions.Delete,
            db.OperationalProjectMembers.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Position, ct);
        await AddAsync(items, "operations.teamRoles", DeletionImpactActions.Delete,
            db.OperationalProjectMemberRoles.Where(item => item.Member.OperationalProjectId == projectId), item => item.Id,
            item => item.Source, ct);
        await AddAsync(items, "operations.assignments", DeletionImpactActions.Delete,
            db.OperationalProjectAssignments.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Title, ct);
        await AddAsync(items, "operations.teamHistory", DeletionImpactActions.Delete,
            db.OperationalProjectTeamHistory.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Action, ct);
        await AddAsync(items, "operations.opportunities", DeletionImpactActions.Unlink,
            db.Opportunities.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Name, ct);
        await AddAsync(items, "operations.quotes", DeletionImpactActions.Unlink,
            db.Quotes.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Code, ct);
        await AddAsync(items, "operations.contracts", DeletionImpactActions.Unlink,
            db.Contracts.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.ContractNumber, ct);
        await AddAsync(items, "operations.pendingDocuments", DeletionImpactActions.Block,
            db.ProjectDocuments.Where(item => item.OperationalProjectId == projectId &&
                item.SyncStatus != ProjectDocumentSyncStatus.Deleted), item => item.Id,
            item => item.OriginalFileName, ct);
        await AddAsync(items, "operations.driveFolders", DeletionImpactActions.Unlink,
            db.ProjectDriveFolders.Where(item => item.OperationalProjectId == projectId), item => item.Id,
            item => item.Category.ToString(), ct);
        await AddAsync(items, "operations.surveyMedia", DeletionImpactActions.Block,
            db.SurveyMedia.Where(item => item.Survey.OperationalProjectId == projectId), item => item.Id,
            item => item.OriginalFileName, ct);
        var surveyFolders = await db.Surveys.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.DriveFolderId != null)
            .Select(item => new { item.Id, item.DriveFolderId })
            .ToListAsync(ct);
        AddRaw(items, "operations.surveyDriveFolders", DeletionImpactActions.Block,
            surveyFolders.Select(item => $"{item.Id}:{item.DriveFolderId}"),
            surveyFolders.Select(item => item.DriveFolderId!));
        await AddTranslationsAsync(items, db, EntityTypes.Survey,
            db.Surveys.Where(item => item.OperationalProjectId == projectId).Select(item => item.Id), ct,
            "operations.translations");
        await AddTranslationsAsync(items, db, EntityTypes.OperationalProject,
            db.OperationalProjects.Where(item => item.Id == projectId).Select(item => item.Id), ct,
            "operations.translations");

        return Build("OperationalProject", project.Id, $"{project.Code} · {project.Name}",
            project.Code, items);
    }

    private static Task AddTranslationsAsync(
        List<PlanItem> items,
        AppDbContext db,
        string entityType,
        IQueryable<int> entityIds,
        CancellationToken ct,
        string key = "design.translations") =>
        AddAsync(items, key, DeletionImpactActions.Delete,
            db.EntityTranslations.Where(item => item.EntityType == entityType && entityIds.Contains(item.EntityId)),
            item => item.Id, item => item.FieldName, ct);

    private static async Task AddAsync<TEntity, TKey>(
        List<PlanItem> items,
        string key,
        string action,
        IQueryable<TEntity> query,
        System.Linq.Expressions.Expression<Func<TEntity, TKey>> idSelector,
        System.Linq.Expressions.Expression<Func<TEntity, string>> labelSelector,
        CancellationToken ct)
        where TEntity : class
        where TKey : struct
    {
        var ids = await query.AsNoTracking().Select(idSelector).OrderBy(id => id).ToListAsync(ct);
        if (ids.Count == 0) return;
        var labels = await query.AsNoTracking().Select(labelSelector).OrderBy(label => label).Take(3).ToListAsync(ct);
        var existing = items.FirstOrDefault(item => item.Key == key && item.Action == action);
        if (existing is null)
        {
            items.Add(new PlanItem(key, action, ids.Select(id => id.ToString()!).ToList(), labels));
            return;
        }

        existing.Ids.AddRange(ids.Select(id => id.ToString()!));
        existing.Ids.Sort(StringComparer.Ordinal);
        existing.Labels.AddRange(labels);
        var distinctLabels = existing.Labels.Distinct(StringComparer.Ordinal).OrderBy(label => label).Take(3).ToList();
        existing.Labels.Clear();
        existing.Labels.AddRange(distinctLabels);
    }

    private static void AddRaw(
        List<PlanItem> items,
        string key,
        string action,
        IEnumerable<string> identifiers,
        IEnumerable<string> labels)
    {
        var ids = identifiers.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return;
        var examples = labels.Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).Take(3).ToList();
        var existing = items.FirstOrDefault(item => item.Key == key && item.Action == action);
        if (existing is null)
        {
            items.Add(new PlanItem(key, action, ids, examples));
            return;
        }
        existing.Ids.AddRange(ids);
        var distinctIds = existing.Ids.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList();
        existing.Ids.Clear();
        existing.Ids.AddRange(distinctIds);
        existing.Labels.AddRange(examples);
        var distinctLabels = existing.Labels.Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal).Take(3).ToList();
        existing.Labels.Clear();
        existing.Labels.AddRange(distinctLabels);
    }

    private static DeletionImpactResponse Build(
        string resourceType,
        int resourceId,
        string resourceLabel,
        string requiredConfirmation,
        List<PlanItem> items)
    {
        items.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        var tokenSource = string.Join('|', items.Select(item =>
            $"{item.Key}:{item.Action}:{string.Join(',', item.Ids)}"));
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{resourceType}:{resourceId}:{resourceLabel}:{requiredConfirmation}:{tokenSource}"))).ToLowerInvariant();
        return new DeletionImpactResponse
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceLabel = resourceLabel,
            RequiredConfirmation = requiredConfirmation,
            PlanToken = token,
            CanDelete = items.All(item => item.Action != DeletionImpactActions.Block),
            TotalAffected = 1 + items.Sum(item => item.Ids.Count),
            Items = items.Select(item => new DeletionImpactItemResponse
            {
                Key = item.Key,
                Action = item.Action,
                Count = item.Ids.Count,
                Examples = item.Labels,
            }).ToList(),
        };
    }

    private sealed record PlanItem(string Key, string Action, List<string> Ids, List<string> Labels);

    private sealed record ManagedFileReference(
        int DesignProjectId,
        ProjectDocumentSourceModule SourceModule,
        string SourceEntityType,
        string SourceSlot,
        long SourceRecordId,
        string LocalPath,
        string Label)
    {
        public string Identifier =>
            $"{DesignProjectId}:{SourceModule}:{SourceEntityType}:{SourceSlot}:{SourceRecordId}:{LocalPath.Trim()}";
    }
}
