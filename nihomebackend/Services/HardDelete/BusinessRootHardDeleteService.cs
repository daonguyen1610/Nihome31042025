using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services.HardDelete;

public sealed record BusinessRootHardDeletePlan(
    DeletionImpactResponse Impact,
    IReadOnlyList<HardDeleteItemDefinition> Items)
{
    internal byte[]? ConcurrencyToken { get; init; }
}

public interface IBusinessRootHardDeleteService
{
    Task<DeletionImpactResponse?> GetOpportunityImpactAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HardDeleteOperationResult?> DeleteOpportunityAsync(int id, ConfirmDeletionRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<DeletionImpactResponse?> GetContractImpactAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<HardDeleteOperationResult?> DeleteContractAsync(int id, ConfirmDeletionRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<DeletionImpactResponse?> GetSurveyImpactAsync(int id, int callerUserId, bool canManageAll, CancellationToken ct = default);
    Task<HardDeleteOperationResult?> DeleteSurveyAsync(int id, ConfirmDeletionRequest request, int callerUserId, bool canManageAll, CancellationToken ct = default);
    Task<DeletionImpactResponse?> GetCapabilityImpactAsync(int id, CancellationToken ct = default);
    Task<HardDeleteOperationResult?> DeleteCapabilityAsync(int id, ConfirmDeletionRequest request, int callerUserId, CancellationToken ct = default);
}

public interface IBusinessRootHardDeletePlanService
{
    Task<BusinessRootHardDeletePlan?> ForOpportunityAsync(int id, CancellationToken ct = default);
    Task<BusinessRootHardDeletePlan?> ForContractAsync(int id, CancellationToken ct = default);
    Task<BusinessRootHardDeletePlan?> ForSurveyAsync(int id, CancellationToken ct = default);
    Task<BusinessRootHardDeletePlan?> ForCapabilityAsync(int id, CancellationToken ct = default);
    Task<bool> CanAccessOpportunityAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> CanAccessContractAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default);
    Task<bool> CanAccessSurveyAsync(int id, int callerUserId, bool canManageAll, CancellationToken ct = default);
}

public sealed class BusinessRootHardDeleteService(
    AppDbContext db,
    IBusinessRootHardDeletePlanService plans,
    IHardDeleteOperationService operations) : IBusinessRootHardDeleteService
{
    public async Task<DeletionImpactResponse?> GetOpportunityImpactAsync(
        int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var plan = await plans.ForOpportunityAsync(id, ct);
        return plan is not null && await plans.CanAccessOpportunityAsync(id, callerUserId, canSeeAll, ct)
            ? plan.Impact
            : null;
    }

    public async Task<HardDeleteOperationResult?> DeleteOpportunityAsync(
        int id, ConfirmDeletionRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await plans.CanAccessOpportunityAsync(id, callerUserId, canSeeAll, ct)) return null;
        return await StartAsync(() => plans.ForOpportunityAsync(id, ct), EntityTypes.Opportunity, id,
            request, callerUserId, requireConcurrency: true, ct);
    }

    public async Task<DeletionImpactResponse?> GetContractImpactAsync(
        int id, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var plan = await plans.ForContractAsync(id, ct);
        return plan is not null && await plans.CanAccessContractAsync(id, callerUserId, canSeeAll, ct)
            ? plan.Impact
            : null;
    }

    public async Task<HardDeleteOperationResult?> DeleteContractAsync(
        int id, ConfirmDeletionRequest request, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        if (!await plans.CanAccessContractAsync(id, callerUserId, canSeeAll, ct)) return null;
        return await StartAsync(() => plans.ForContractAsync(id, ct), EntityTypes.Contract, id,
            request, callerUserId, requireConcurrency: true, ct);
    }

    public async Task<DeletionImpactResponse?> GetSurveyImpactAsync(
        int id, int callerUserId, bool canManageAll, CancellationToken ct = default)
    {
        var plan = await plans.ForSurveyAsync(id, ct);
        return plan is not null && await plans.CanAccessSurveyAsync(id, callerUserId, canManageAll, ct)
            ? plan.Impact
            : null;
    }

    public async Task<HardDeleteOperationResult?> DeleteSurveyAsync(
        int id, ConfirmDeletionRequest request, int callerUserId, bool canManageAll, CancellationToken ct = default)
    {
        if (!await plans.CanAccessSurveyAsync(id, callerUserId, canManageAll, ct)) return null;
        return await StartAsync(() => plans.ForSurveyAsync(id, ct), EntityTypes.Survey, id,
            request, callerUserId, requireConcurrency: false, ct);
    }

    public async Task<DeletionImpactResponse?> GetCapabilityImpactAsync(int id, CancellationToken ct = default) =>
        (await plans.ForCapabilityAsync(id, ct))?.Impact;

    public Task<HardDeleteOperationResult?> DeleteCapabilityAsync(
        int id, ConfirmDeletionRequest request, int callerUserId, CancellationToken ct = default) =>
        StartAsync(() => plans.ForCapabilityAsync(id, ct), EntityTypes.CapabilityDocument, id,
            request, callerUserId, requireConcurrency: false, ct);

    private async Task<HardDeleteOperationResult?> StartAsync(
        Func<Task<BusinessRootHardDeletePlan?>> createPlan,
        string resourceType,
        int id,
        ConfirmDeletionRequest request,
        int callerUserId,
        bool requireConcurrency,
        CancellationToken ct)
    {
        return await DurableHardDeleteStarter.StartAsync(db, operations, createPlan, plan =>
        {
            Validate(plan.Impact, request);
            if (requireConcurrency)
                CrmConcurrency.EnsureMatches(plan.ConcurrencyToken ?? [], request.RowVersion);
            return new CreateHardDeleteOperationRequest(
                resourceType,
                id.ToString(CultureInfo.InvariantCulture),
                plan.Impact.ResourceLabel,
                plan.Impact.PlanToken,
                request.Confirmation,
                callerUserId.ToString(CultureInfo.InvariantCulture),
                plan.Items);
        }, ct);
    }

    private static void Validate(DeletionImpactResponse impact, ConfirmDeletionRequest request)
    {
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!impact.CanDelete)
            throw new BusinessRootDeleteException("Không thể xoá vì còn dữ liệu liên quan cần được xử lý an toàn trước.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new BusinessRootDeleteException($"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
    }
}

public sealed class BusinessRootHardDeletePlanService(
    AppDbContext db,
    IGoogleDriveSettingsStore settingsStore,
    IHardDeleteFileService files) : IBusinessRootHardDeletePlanService
{
    public async Task<BusinessRootHardDeletePlan?> ForOpportunityAsync(int id, CancellationToken ct = default)
    {
        var root = await db.Opportunities.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, item.Name, item.RowVersion })
            .SingleOrDefaultAsync(ct);
        if (root is null) return null;

        var activities = await db.OpportunityActivities.AsNoTracking()
            .Where(item => item.OpportunityId == id).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var translations = await db.EntityTranslations.AsNoTracking()
            .Where(item => item.EntityType == EntityTypes.Opportunity && item.EntityId == id)
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync(ct);
        var quotes = await db.Quotes.AsNoTracking().Where(item => item.OpportunityId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.Code }).ToListAsync(ct);
        var contracts = await db.Contracts.AsNoTracking().Where(item => item.OpportunityId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.ContractNumber }).ToListAsync(ct);
        var surveys = await db.Surveys.AsNoTracking().Where(item => item.LinkedOpportunityId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.Code }).ToListAsync(ct);
        var leads = await db.Leads.AsNoTracking().Where(item => item.ConvertedOpportunityId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.Name }).ToListAsync(ct);
        var tenders = await db.Tenders.AsNoTracking().Where(item => item.WonOpportunityId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.Code, item.Name }).ToListAsync(ct);

        var detail = $"/admin/opportunities/{id}";
        var items = new List<DeletionImpactItemResponse>();
        Add(items, "opportunity.activities", activities.Select(Id).ToList(), DeletionImpactActions.Delete,
            detail, [Link(root.Name, detail)]);
        Add(items, "opportunity.translations", translations.Select(Id).ToList());
        Add(items, "opportunity.quotes", quotes.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Block,
            null, quotes.Select(item => Link(item.Code, $"/admin/quotes/{item.Id}")).ToList());
        Add(items, "opportunity.contracts", contracts.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Unlink,
            null, contracts.Select(item => Link(item.ContractNumber, $"/admin/contracts/{item.Id}")).ToList());
        Add(items, "opportunity.surveys", surveys.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Unlink,
            null, surveys.Select(item => Link(item.Code, $"/admin/surveys/{item.Id}")).ToList());
        Add(items, "opportunity.convertedLeads", leads.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Unlink,
            null, leads.Select(item => Link(item.Name, $"/admin/leads/{item.Id}")).ToList());
        Add(items, "opportunity.winningTenders", tenders.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Unlink,
            null, tenders.Select(item => Link($"{item.Code} · {item.Name}", $"/admin/tenders/{item.Id}")).ToList());

        var identities = new[]
        {
            Part("activities", activities), Part("translations", translations),
            Part("quotes", quotes.Select(item => item.Id)), Part("contracts", contracts.Select(item => item.Id)),
            Part("surveys", surveys.Select(item => item.Id)), Part("leads", leads.Select(item => item.Id)),
            Part("tenders", tenders.Select(item => item.Id)),
        };
        return Plan(EntityTypes.Opportunity, id, root.Name, $"OPPORTUNITY-{id}",
            root.RowVersion, quotes.Count == 0, items, identities, []);
    }

    public async Task<BusinessRootHardDeletePlan?> ForContractAsync(int id, CancellationToken ct = default)
    {
        var root = await db.Contracts.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.ContractNumber,
                item.RowVersion,
                item.OpportunityId,
                item.CustomerId,
                item.Status,
                item.SignedDate,
                item.OperationalProjectId,
            }).SingleOrDefaultAsync(ct);
        if (root is null) return null;
        var driveOptions = await settingsStore.GetRuntimeAsync(ct);

        var milestones = await db.ContractPaymentMilestones.AsNoTracking()
            .Where(item => item.ContractId == id).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var attachments = await db.ContractAttachments.AsNoTracking()
            .Where(item => item.ContractId == id).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.FilePath, item.OriginalFileName }).ToListAsync(ct);
        var appendices = await db.ContractAppendices.AsNoTracking()
            .Where(item => item.ContractId == id).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.FilePath, item.OriginalFileName }).ToListAsync(ct);
        var projects = await db.DesignProjects.AsNoTracking().Where(item => item.ContractId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.ProjectCode, item.Name }).ToListAsync(ct);

        var attachmentIds = attachments.Select(item => (long)item.Id).ToList();
        var appendixIds = appendices.Select(item => (long)item.Id).ToList();
        var sourcePaths = attachments.Select(item => item.FilePath)
            .Concat(appendices.Where(item => item.FilePath != null).Select(item => item.FilePath!))
            .Distinct(StringComparer.Ordinal).ToList();
        var sidecars = await db.ProjectDocuments.AsNoTracking()
            .Where(item => sourcePaths.Contains(item.LocalPath) ||
                root.OperationalProjectId.HasValue && item.OperationalProjectId == root.OperationalProjectId.Value &&
                item.SourceRecordId.HasValue && (attachmentIds.Contains(item.SourceRecordId.Value) || appendixIds.Contains(item.SourceRecordId.Value)) ||
                item.SourceModule == ProjectDocumentSourceModule.Crm && item.SourceSlot == "file" &&
                item.SourceRecordId.HasValue &&
                (item.SourceEntityType == nameof(ContractAttachment) && attachmentIds.Contains(item.SourceRecordId.Value) ||
                 item.SourceEntityType == nameof(ContractAppendix) && appendixIds.Contains(item.SourceRecordId.Value)))
            .OrderBy(item => item.Id).ToListAsync(ct);

        var pathEntries = attachments.Select(item => new { item.Id, item.FilePath })
            .Concat(appendices.Where(item => item.FilePath != null)
                .Select(item => new { item.Id, FilePath = item.FilePath! })).ToList();
        var localPaths = new List<string>();
        var blockers = new List<string>();
        foreach (var entry in pathEntries)
        {
            try
            {
                var path = files.ValidateManagedPath(entry.FilePath);
                if (!path.StartsWith("/files/contracts/", StringComparison.Ordinal)) blockers.Add($"outside-root:{entry.Id}:{path}");
                else localPaths.Add(path);
            }
            catch (HardDeleteFileException)
            {
                blockers.Add($"invalid:{entry.Id}:{entry.FilePath}");
            }
        }
        localPaths = localPaths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var sharedPaths = await db.ContractAttachments.AsNoTracking()
            .Where(item => item.ContractId != id && localPaths.Contains(item.FilePath))
            .Select(item => item.FilePath)
            .Concat(db.ContractAppendices.AsNoTracking()
                .Where(item => item.ContractId != id && item.FilePath != null && localPaths.Contains(item.FilePath))
                .Select(item => item.FilePath!))
            .Distinct().OrderBy(path => path).ToListAsync(ct);
        blockers.AddRange(sharedPaths.Select(path => $"shared:{path}"));
        localPaths = localPaths.Except(sharedPaths, StringComparer.Ordinal).ToList();

        var attachmentPaths = attachments.ToDictionary(item => (long)item.Id, item => item.FilePath);
        var appendixPaths = appendices.Where(item => item.FilePath != null)
            .ToDictionary(item => (long)item.Id, item => item.FilePath!);
        var candidateDriveIds = sidecars.Where(item => !string.IsNullOrWhiteSpace(item.DriveFileId))
            .Select(item => item.DriveFileId!).Distinct(StringComparer.Ordinal).ToList();
        var matchingDriveIds = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.DriveFileId != null && candidateDriveIds.Contains(item.DriveFileId))
            .Select(item => item.DriveFileId!).ToListAsync(ct);
        var duplicateDriveIds = matchingDriveIds.GroupBy(item => item, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var sidecarIdentifiers = new List<string>();
        var sidecarBlockers = new List<string>();
        var driveIdentifiers = new List<string>();
        var driveDefinitions = new List<HardDeleteItemDefinition>();
        foreach (var sidecar in sidecars)
        {
            var identity = SidecarIdentity(sidecar);
            if (!IsSafeContractSidecar(sidecar, attachmentPaths, appendixPaths,
                    root.OperationalProjectId, driveOptions.InstanceId) ||
                sidecar.DriveFileId is not null && duplicateDriveIds.Contains(sidecar.DriveFileId))
            {
                sidecarBlockers.Add(identity);
                continue;
            }

            sidecarIdentifiers.Add(identity);
            if (sidecar.SyncStatus == ProjectDocumentSyncStatus.Deleted) continue;
            driveIdentifiers.Add(sidecar.DriveFileId!);
            driveDefinitions.Add(new HardDeleteItemDefinition(
                HardDeleteItemKind.DriveFile,
                sidecar.DriveFileId!,
                localPaths.Count + driveDefinitions.Count,
                new Dictionary<string, string>
                {
                    ["niconInstance"] = driveOptions.InstanceId,
                    ["niconReplicaKey"] = $"project-document:{sidecar.Id}",
                    ["niconGeneration"] = sidecar.Generation.ToString(CultureInfo.InvariantCulture),
                },
                sidecar.DriveFolderId));
        }
        sidecarIdentifiers.Sort(StringComparer.Ordinal);
        sidecarBlockers.Sort(StringComparer.Ordinal);
        driveIdentifiers.Sort(StringComparer.Ordinal);

        var lifecycleBlockers = new List<(int Id, string Name)>();
        if (root.OpportunityId.HasValue && root.SignedDate.HasValue &&
            root.Status is not ContractStatus.Draft and not ContractStatus.Cancelled)
        {
            var opportunity = await db.Opportunities.AsNoTracking()
                .Where(item => item.Id == root.OpportunityId.Value && item.Stage == OpportunityStage.Won && item.CustomerId == root.CustomerId)
                .Select(item => new { item.Id, item.Name }).SingleOrDefaultAsync(ct);
            if (opportunity is not null && !await db.Contracts.AsNoTracking().AnyAsync(item =>
                    item.Id != id && item.OpportunityId == opportunity.Id && item.CustomerId == root.CustomerId &&
                    item.SignedDate.HasValue && item.Status != ContractStatus.Draft && item.Status != ContractStatus.Cancelled, ct))
                lifecycleBlockers.Add((opportunity.Id, opportunity.Name));
        }

        var detail = $"/admin/contracts/{id}";
        var items = new List<DeletionImpactItemResponse>();
        Add(items, "contract.paymentMilestones", milestones.Select(Id).ToList(), DeletionImpactActions.Delete, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.attachments", attachments.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Delete, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.appendices", appendices.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Delete, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.localFiles", localPaths, DeletionImpactActions.Delete, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.fileBlockers", blockers, DeletionImpactActions.Block, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.driveFiles", driveIdentifiers, DeletionImpactActions.Delete, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.projectDocumentSidecars", sidecarIdentifiers, DeletionImpactActions.Unlink, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.projectDocumentSidecarBlockers", sidecarBlockers, DeletionImpactActions.Block, detail, [Link(root.ContractNumber, detail)]);
        Add(items, "contract.designProjects", projects.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Unlink,
            null, projects.Select(item => Link($"{item.ProjectCode} · {item.Name}", $"/admin/design-projects/{item.Id}")).ToList());
        Add(items, "contract.wonOpportunity", lifecycleBlockers.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Block,
            null, lifecycleBlockers.Select(item => Link(item.Name, $"/admin/opportunities/{item.Id}")).ToList());

        var identities = new[]
        {
            Part("milestones", milestones), Part("attachments", attachments.Select(item => item.Id)),
            Part("appendices", appendices.Select(item => item.Id)), Part("paths", localPaths),
            Part("file-blockers", blockers), Part("projects", projects.Select(item => item.Id)),
            Part("lifecycle", lifecycleBlockers.Select(item => item.Id)), $"drive-instance:{driveOptions.InstanceId}",
            Part("drive-files", driveIdentifiers), Part("sidecars", sidecarIdentifiers),
            Part("sidecar-blockers", sidecarBlockers),
        };
        var definitions = localPaths.Select((path, index) =>
                new HardDeleteItemDefinition(HardDeleteItemKind.LocalFile, path, index))
            .Concat(driveDefinitions).ToList();
        return Plan(EntityTypes.Contract, id, root.ContractNumber, root.ContractNumber,
            root.RowVersion, blockers.Count == 0 && lifecycleBlockers.Count == 0 && sidecarBlockers.Count == 0,
            items, identities, definitions);
    }

    public async Task<BusinessRootHardDeletePlan?> ForSurveyAsync(int id, CancellationToken ct = default)
    {
        var root = await db.Surveys.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.Id, item.Code, item.DriveFolderId }).SingleOrDefaultAsync(ct);
        if (root is null) return null;
        var media = await db.SurveyMedia.AsNoTracking().Where(item => item.SurveyId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.OriginalFileName }).ToListAsync(ct);
        var checklist = await db.SurveyChecklistResults.AsNoTracking().Where(item => item.SurveyId == id)
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync(ct);
        var conditions = await db.SurveySiteConditions.AsNoTracking().Where(item => item.SurveyId == id)
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync(ct);
        var detail = $"/admin/surveys/{id}";
        var items = new List<DeletionImpactItemResponse>();
        Add(items, "survey.checklistResults", checklist.Select(Id).ToList(), DeletionImpactActions.Delete, detail, [Link(root.Code, detail)]);
        Add(items, "survey.siteConditions", conditions.Select(Id).ToList(), DeletionImpactActions.Delete, detail, [Link(root.Code, detail)]);
        Add(items, "survey.media", media.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Block, detail,
            media.Select(item => Link(item.OriginalFileName, $"{detail}?mediaId={item.Id}")).ToList());
        var folders = string.IsNullOrWhiteSpace(root.DriveFolderId) ? new List<string>() : ["drive-folder"];
        Add(items, "survey.driveFolder", folders, DeletionImpactActions.Unlink, detail, [Link(root.Code, detail)]);
        var identities = new[]
        {
            Part("media", media.Select(item => item.Id)), Part("checklist", checklist), Part("conditions", conditions),
            $"folder:{root.DriveFolderId ?? "none"}",
        };
        return Plan(EntityTypes.Survey, id, root.Code, root.Code, null,
            media.Count == 0, items, identities, []);
    }

    public async Task<BusinessRootHardDeletePlan?> ForCapabilityAsync(int id, CancellationToken ct = default)
    {
        var root = await db.CapabilityDocuments.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.FilePath,
                IsSeeded = item.Description != null && item.Description.StartsWith("[SAMPLE_CAP]"),
            }).SingleOrDefaultAsync(ct);
        if (root is null) return null;
        var versions = await db.CapabilityDocumentVersions.AsNoTracking().Where(item => item.CapabilityDocumentId == id)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.FilePath }).ToListAsync(ct);
        var rawPaths = versions.Select(item => item.FilePath).Append(root.FilePath).Distinct(StringComparer.Ordinal).ToList();
        var localPaths = new List<string>();
        var fileBlockers = new List<string>();
        foreach (var rawPath in rawPaths)
        {
            try
            {
                var path = files.ValidateManagedPath(rawPath);
                if (!path.StartsWith("/files/capability/", StringComparison.Ordinal)) fileBlockers.Add($"outside-root:{path}");
                else localPaths.Add(path);
            }
            catch (HardDeleteFileException)
            {
                fileBlockers.Add($"invalid:{rawPath}");
            }
        }
        localPaths = localPaths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var sharedPaths = await db.CapabilityDocuments.AsNoTracking()
            .Where(item => item.Id != id && localPaths.Contains(item.FilePath)).Select(item => item.FilePath)
            .Concat(db.CapabilityDocumentVersions.AsNoTracking()
                .Where(item => item.CapabilityDocumentId != id && localPaths.Contains(item.FilePath))
                .Select(item => item.FilePath))
            .Distinct().OrderBy(path => path).ToListAsync(ct);
        fileBlockers.AddRange(sharedPaths.Select(path => $"shared:{path}"));
        localPaths = localPaths.Except(sharedPaths, StringComparer.Ordinal).ToList();
        var tenderReferences = await db.TenderChecklistItems.AsNoTracking()
            .Where(item => item.CapabilityDocumentId == id || item.FilePath != null && rawPaths.Contains(item.FilePath))
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.TenderId, item.Tender.Code, item.Tender.Name }).ToListAsync(ct);
        fileBlockers = fileBlockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var detail = $"/admin/capability-documents?documentId={id}";
        var items = new List<DeletionImpactItemResponse>();
        Add(items, "capability.versions", versions.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Delete, detail, [Link(root.Name, detail)]);
        Add(items, "capability.localFiles", localPaths, DeletionImpactActions.Delete, detail, [Link(root.Name, detail)]);
        Add(items, "capability.fileBlockers", fileBlockers, DeletionImpactActions.Block, detail, [Link(root.Name, detail)]);
        Add(items, "capability.tenderReferences", tenderReferences.Select(item => Id(item.Id)).ToList(), DeletionImpactActions.Block,
            null, tenderReferences.Select(item => Link($"{item.Code} · {item.Name}", $"/admin/tenders/{item.TenderId}")).ToList());
        var identities = new[]
        {
            Part("versions", versions.Select(item => item.Id)), Part("paths", localPaths),
            Part("file-blockers", fileBlockers), Part("tenders", tenderReferences.Select(item => item.Id)),
            $"seeded:{root.IsSeeded}",
        };
        var definitions = localPaths.Select((path, index) =>
                new HardDeleteItemDefinition(HardDeleteItemKind.LocalFile, path, index)).ToList();
        return Plan(EntityTypes.CapabilityDocument, id, root.Name, $"CAPABILITY-{id}", null,
            fileBlockers.Count == 0 && tenderReferences.Count == 0, items, identities, definitions);
    }

    private static BusinessRootHardDeletePlan Plan(
        string resourceType,
        int id,
        string label,
        string confirmation,
        byte[]? concurrencyToken,
        bool canDelete,
        List<DeletionImpactItemResponse> items,
        IEnumerable<string> identities,
        IReadOnlyList<HardDeleteItemDefinition> externalItems)
    {
        var concurrency = concurrencyToken is null ? string.Empty : CrmConcurrency.Encode(concurrencyToken);
        var source = string.Join('|', new[] { $"root:{id}:{label}:{concurrency}" }.Concat(identities));
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{resourceType}:{source}"))).ToLowerInvariant();
        var definitions = externalItems.Append(new HardDeleteItemDefinition(
            HardDeleteItemKind.DatabaseAggregate, $"delete-{resourceType.ToLowerInvariant()}-aggregate", externalItems.Count)).ToList();
        return new BusinessRootHardDeletePlan(new DeletionImpactResponse
        {
            ResourceType = resourceType,
            ResourceId = id,
            ResourceLabel = label,
            RequiredConfirmation = confirmation,
            PlanToken = token,
            CanDelete = canDelete,
            TotalAffected = 1 + items.Sum(item => item.Count),
            Items = items,
        }, definitions)
        {
            ConcurrencyToken = concurrencyToken?.ToArray(),
        };
    }

    public Task<bool> CanAccessOpportunityAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default) =>
        db.Opportunities.AsNoTracking().AnyAsync(item => item.Id == id && (canSeeAll || item.OwnerUserId == callerUserId), ct);

    public Task<bool> CanAccessContractAsync(int id, int callerUserId, bool canSeeAll, CancellationToken ct = default) =>
        db.Contracts.AsNoTracking().AnyAsync(item => item.Id == id && (canSeeAll || item.OwnerUserId == callerUserId), ct);

    public Task<bool> CanAccessSurveyAsync(int id, int callerUserId, bool canManageAll, CancellationToken ct = default) =>
        db.Surveys.AsNoTracking().AnyAsync(item => item.Id == id &&
            (canManageAll || item.SurveyorUserId == callerUserId || item.CreatedByUserId == callerUserId ||
             item.OperationalProject.ProjectManagerUserId == callerUserId ||
             item.OperationalProject.CreatedByUserId == callerUserId), ct);

    private static bool IsSafeContractSidecar(
        ProjectDocument sidecar,
        IReadOnlyDictionary<long, string> attachmentPaths,
        IReadOnlyDictionary<long, string> appendixPaths,
        int? operationalProjectId,
        string instanceId)
    {
        var expectedPaths = string.Equals(sidecar.SourceEntityType, nameof(ContractAttachment), StringComparison.Ordinal)
            ? attachmentPaths
            : string.Equals(sidecar.SourceEntityType, nameof(ContractAppendix), StringComparison.Ordinal)
                ? appendixPaths
                : null;
        if (!operationalProjectId.HasValue || sidecar.OperationalProjectId != operationalProjectId.Value ||
            sidecar.SourceModule != ProjectDocumentSourceModule.Crm ||
            sidecar.SourceType != ProjectDocumentSourceType.ExistingManagedFile ||
            !string.Equals(sidecar.SourceSlot, "file", StringComparison.Ordinal) ||
            sidecar.Origin != ProjectDocumentOrigin.Nicon ||
            sidecar.ConflictState != ProjectDocumentConflictState.None ||
            sidecar.ConflictWithDocumentId.HasValue ||
            !string.IsNullOrWhiteSpace(sidecar.ConflictObservedDriveFileId) ||
            !string.IsNullOrWhiteSpace(sidecar.ConflictObservedDriveVersion) ||
            sidecar.DesiredOperation != ProjectDocumentDesiredOperation.None ||
            sidecar.SyncStatus == ProjectDocumentSyncStatus.Processing ||
            sidecar.ClaimToken.HasValue || sidecar.ClaimExpiresAt.HasValue ||
            !sidecar.SourceRecordId.HasValue || expectedPaths is null ||
            !expectedPaths.TryGetValue(sidecar.SourceRecordId.Value, out var expectedPath) ||
            !string.Equals(sidecar.LocalPath, expectedPath, StringComparison.Ordinal)) return false;

        return sidecar.SyncStatus switch
        {
            ProjectDocumentSyncStatus.Synced =>
                !string.IsNullOrWhiteSpace(instanceId) &&
                !string.IsNullOrWhiteSpace(sidecar.DriveFileId) &&
                !string.IsNullOrWhiteSpace(sidecar.DriveFolderId) && sidecar.Generation > 0,
            ProjectDocumentSyncStatus.Deleted => string.IsNullOrWhiteSpace(sidecar.DriveFileId),
            _ => false,
        };
    }

    private static string SidecarIdentity(ProjectDocument document) => string.Join(':',
        "project-document", document.Id, document.OperationalProjectId, document.SourceModule,
        document.SourceType, document.SourceEntityType, document.SourceSlot, document.SourceRecordId,
        document.LocalPath, document.Origin, document.Generation, document.SyncStatus,
        document.DesiredOperation, document.ConflictState, document.DriveFileId,
        document.DriveFolderId, document.ClaimToken, document.ClaimExpiresAt?.ToString("O"),
        document.ConflictWithDocumentId, document.ConflictObservedDriveFileId,
        document.ConflictObservedDriveVersion, document.SyncAttemptCount, document.SyncError,
        document.NextSyncAttemptAt?.ToString("O"), document.LastSyncAttemptAt?.ToString("O"),
        document.DeletedAt?.ToString("O"), document.DeletedByUserId);

    private static void Add(
        ICollection<DeletionImpactItemResponse> items,
        string key,
        IReadOnlyList<string> identifiers,
        string action = DeletionImpactActions.Delete,
        string? resolutionUrl = null,
        IReadOnlyList<DeletionImpactLinkResponse>? links = null)
    {
        if (identifiers.Count == 0) return;
        items.Add(new DeletionImpactItemResponse
        {
            Key = key,
            Action = action,
            Count = identifiers.Count,
            Examples = identifiers.Take(3).ToList(),
            ResolutionUrl = resolutionUrl,
            ResolutionLinks = links?.DistinctBy(item => item.Url).Take(3).ToList() ?? [],
        });
    }

    private static DeletionImpactLinkResponse Link(string label, string url) => new() { Label = label, Url = url };
    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
    private static string Part<T>(string name, IEnumerable<T> values) => $"{name}:{string.Join(',', values)}";
}

public sealed class BusinessRootDeleteException(string message) : InvalidOperationException(message);

public abstract class BusinessRootHardDeleteHandler(
    AppDbContext db,
    IBusinessRootHardDeletePlanService plans,
    IPermissionService permissions) : IHardDeleteResourceHandler
{
    protected AppDbContext Db { get; } = db;
    protected IBusinessRootHardDeletePlanService Plans { get; } = plans;
    protected IPermissionService Permissions { get; } = permissions;
    public abstract string ResourceType { get; }
    protected abstract string PermissionResource { get; }
    protected abstract Task<BusinessRootHardDeletePlan?> CurrentPlanAsync(int id, CancellationToken ct);
    protected abstract Task<bool> HasScopeAsync(int id, int requestedBy, CancellationToken ct);
    protected abstract Task FinalizeRootAsync(int id, int requestedBy, CancellationToken ct);

    public async Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        var (id, requestedBy) = Parse(context);
        if (!await Permissions.HasAsync(requestedBy, $"{PermissionResource}.manage", ct))
            throw new HardDeleteAuthorizationException("Quyền xoá hoặc phạm vi dữ liệu đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");
        var current = await CurrentPlanAsync(id, ct);
        if (current is null)
            throw new HardDeleteAuthorizationException("Tài nguyên cần xoá không còn tồn tại trước khi tác vụ bắt đầu.");
        if (!await HasScopeAsync(id, requestedBy, ct))
            throw new HardDeleteAuthorizationException("Quyền xoá hoặc phạm vi dữ liệu đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        if (!current.Impact.CanDelete)
            throw new BusinessRootDeleteException("Dữ liệu phụ thuộc đã thay đổi và đang chặn thao tác xoá.");
    }

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        var (id, requestedBy) = Parse(context);
        await using var transaction = Db.Database.IsRelational()
            ? await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        try
        {
            var current = await CurrentPlanAsync(id, ct);
            if (current is null)
            {
                if (transaction is not null) await transaction.CommitAsync(ct);
                return;
            }
            DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
            if (!current.Impact.CanDelete)
                throw new BusinessRootDeleteException("Dữ liệu phụ thuộc đã thay đổi và đang chặn thao tác xoá.");
            await FinalizeRootAsync(id, requestedBy, ct);
            await Db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            DesignProjectHardDeleteHandler.DetachRolledBackDomainEntries(Db);
            throw;
        }
    }

    protected async Task AddSeedTombstoneAsync(
        string resourceType, string resourceKey, bool isSeeded, int requestedBy, CancellationToken ct)
    {
        if (!isSeeded || await Db.SeededRootDeletions.AnyAsync(item =>
                item.ResourceType == resourceType && item.ResourceKey == resourceKey, ct)) return;
        Db.SeededRootDeletions.Add(new SeededRootDeletion
        {
            ResourceType = resourceType,
            ResourceKey = resourceKey,
            DeletedByUserId = requestedBy,
        });
    }

    private static (int Id, int RequestedBy) Parse(HardDeleteResourceContext context)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xoá không hợp lệ.");
        return (id, requestedBy);
    }
}

public sealed class OpportunityHardDeleteHandler(
    AppDbContext db, IBusinessRootHardDeletePlanService plans, IPermissionService permissions)
    : BusinessRootHardDeleteHandler(db, plans, permissions)
{
    public override string ResourceType => EntityTypes.Opportunity;
    protected override string PermissionResource => "crm.opportunities";
    protected override Task<BusinessRootHardDeletePlan?> CurrentPlanAsync(int id, CancellationToken ct) => Plans.ForOpportunityAsync(id, ct);
    protected override async Task<bool> HasScopeAsync(int id, int requestedBy, CancellationToken ct) =>
        await Permissions.HasAsync(requestedBy, "crm.opportunities.view.all", ct) ||
        await Db.Opportunities.AnyAsync(item => item.Id == id && item.OwnerUserId == requestedBy, ct);

    protected override async Task FinalizeRootAsync(int id, int requestedBy, CancellationToken ct)
    {
        var root = await Db.Opportunities.SingleAsync(item => item.Id == id, ct);
        foreach (var contract in await Db.Contracts.Where(item => item.OpportunityId == id).ToListAsync(ct)) contract.OpportunityId = null;
        foreach (var survey in await Db.Surveys.Where(item => item.LinkedOpportunityId == id).ToListAsync(ct)) survey.LinkedOpportunityId = null;
        foreach (var lead in await Db.Leads.Where(item => item.ConvertedOpportunityId == id).ToListAsync(ct)) lead.ConvertedOpportunityId = null;
        foreach (var tender in await Db.Tenders.Where(item => item.WonOpportunityId == id).ToListAsync(ct)) tender.WonOpportunityId = null;
        await Db.EntityTranslations.Where(item => item.EntityType == EntityTypes.Opportunity && item.EntityId == id)
            .ExecuteDeleteOrRemoveAsync(Db, ct);
        await AddSeedTombstoneAsync(EntityTypes.Opportunity, root.Name,
            root.Name.StartsWith("[SAMPLE]", StringComparison.Ordinal), requestedBy, ct);
        Db.Opportunities.Remove(root);
    }
}

public sealed class ContractHardDeleteHandler(
    AppDbContext db,
    IBusinessRootHardDeletePlanService plans,
    IPermissionService permissions)
    : BusinessRootHardDeleteHandler(db, plans, permissions)
{
    public override string ResourceType => EntityTypes.Contract;
    protected override string PermissionResource => "crm.contracts";
    protected override Task<BusinessRootHardDeletePlan?> CurrentPlanAsync(int id, CancellationToken ct) => Plans.ForContractAsync(id, ct);
    protected override async Task<bool> HasScopeAsync(int id, int requestedBy, CancellationToken ct) =>
        await Permissions.HasAsync(requestedBy, "crm.contracts.view.all", ct) ||
        await Db.Contracts.AnyAsync(item => item.Id == id && item.OwnerUserId == requestedBy, ct);

    protected override async Task FinalizeRootAsync(int id, int requestedBy, CancellationToken ct)
    {
        var root = await Db.Contracts.SingleAsync(item => item.Id == id, ct);
        var attachmentIds = await Db.ContractAttachments.Where(item => item.ContractId == id)
            .Select(item => (long)item.Id).ToListAsync(ct);
        var appendixIds = await Db.ContractAppendices.Where(item => item.ContractId == id)
            .Select(item => (long)item.Id).ToListAsync(ct);
        var sidecars = await Db.ProjectDocuments.Where(item =>
            item.SourceModule == ProjectDocumentSourceModule.Crm && item.SourceSlot == "file" &&
            item.SourceRecordId.HasValue &&
            (item.SourceEntityType == nameof(ContractAttachment) && attachmentIds.Contains(item.SourceRecordId.Value) ||
             item.SourceEntityType == nameof(ContractAppendix) && appendixIds.Contains(item.SourceRecordId.Value)))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var sidecar in sidecars)
        {
            sidecar.DesiredOperation = ProjectDocumentDesiredOperation.None;
            sidecar.SyncStatus = ProjectDocumentSyncStatus.Deleted;
            sidecar.DriveFileId = null;
            sidecar.DriveWebViewLink = null;
            sidecar.DriveVersion = null;
            sidecar.DriveModifiedAt = null;
            sidecar.SyncAttemptCount = 0;
            sidecar.SyncError = null;
            sidecar.NextSyncAttemptAt = null;
            sidecar.LastSyncAttemptAt = null;
            sidecar.ClaimToken = null;
            sidecar.ClaimExpiresAt = null;
            sidecar.DeletedAt = now;
            sidecar.DeletedByUserId = requestedBy;
            sidecar.UpdatedAt = now;
            sidecar.UpdatedByUserId = requestedBy;
        }
        await AddSeedTombstoneAsync(EntityTypes.Contract, root.ContractNumber,
            root.ContractNumber.StartsWith("HD-SAMPLE-", StringComparison.Ordinal), requestedBy, ct);
        Db.Contracts.Remove(root);
    }
}

public sealed class SurveyHardDeleteHandler(
    AppDbContext db, IBusinessRootHardDeletePlanService plans, IPermissionService permissions)
    : BusinessRootHardDeleteHandler(db, plans, permissions)
{
    public override string ResourceType => EntityTypes.Survey;
    protected override string PermissionResource => "crm.surveys";
    protected override Task<BusinessRootHardDeletePlan?> CurrentPlanAsync(int id, CancellationToken ct) => Plans.ForSurveyAsync(id, ct);
    protected override async Task<bool> HasScopeAsync(int id, int requestedBy, CancellationToken ct) =>
        await Permissions.HasAsync(requestedBy, "crm.surveys.manage.all", ct) ||
        await Db.Surveys.AnyAsync(item => item.Id == id &&
            (item.SurveyorUserId == requestedBy || item.CreatedByUserId == requestedBy ||
             item.OperationalProject.ProjectManagerUserId == requestedBy ||
             item.OperationalProject.CreatedByUserId == requestedBy), ct);
    protected override async Task FinalizeRootAsync(int id, int requestedBy, CancellationToken ct)
    {
        var root = await Db.Surveys.SingleAsync(item => item.Id == id, ct);
        await AddSeedTombstoneAsync(EntityTypes.Survey, root.Code,
            root.Code.StartsWith("SV-SAMPLE-", StringComparison.Ordinal), requestedBy, ct);
        Db.Surveys.Remove(root);
    }
}

public sealed class CapabilityDocumentHardDeleteHandler(
    AppDbContext db, IBusinessRootHardDeletePlanService plans, IPermissionService permissions)
    : BusinessRootHardDeleteHandler(db, plans, permissions)
{
    public override string ResourceType => EntityTypes.CapabilityDocument;
    protected override string PermissionResource => "crm.capability-docs";
    protected override Task<BusinessRootHardDeletePlan?> CurrentPlanAsync(int id, CancellationToken ct) => Plans.ForCapabilityAsync(id, ct);
    protected override Task<bool> HasScopeAsync(int id, int requestedBy, CancellationToken ct) => Task.FromResult(true);
    protected override async Task FinalizeRootAsync(int id, int requestedBy, CancellationToken ct)
    {
        var root = await Db.CapabilityDocuments.SingleAsync(item => item.Id == id, ct);
        await AddSeedTombstoneAsync(EntityTypes.CapabilityDocument, root.FilePath,
            root.Description?.StartsWith("[SAMPLE_CAP]", StringComparison.Ordinal) == true, requestedBy, ct);
        Db.CapabilityDocuments.Remove(root);
    }
}