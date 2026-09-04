using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.GoogleDrive;

namespace NihomeBackend.Services.HardDelete;

public sealed record CrmHardDeletePlan(
    DeletionImpactResponse Impact,
    IReadOnlyList<HardDeleteItemDefinition> Items);

public interface ICrmHardDeletePlanService
{
    Task<CrmHardDeletePlan?> ForLeadAsync(int leadId, CancellationToken ct = default);
    Task<CrmHardDeletePlan?> ForCustomerAsync(int customerId, CancellationToken ct = default);
    Task<CrmHardDeletePlan?> ForTenderAsync(int tenderId, CancellationToken ct = default);
    Task<CrmHardDeletePlan?> ForQuoteAsync(int quoteId, CancellationToken ct = default);
}

public sealed class CrmHardDeletePlanService(
    AppDbContext db,
    IGoogleDriveSettingsStore settingsStore,
    IHardDeleteFileService files) : ICrmHardDeletePlanService
{
    public async Task<CrmHardDeletePlan?> ForCustomerAsync(
        int customerId, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(item => item.Id == customerId)
            .Select(item => new { item.Id, item.Name, item.RowVersion })
            .SingleOrDefaultAsync(ct);
        if (customer is null) return null;

        var contactIds = await db.CustomerContacts.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var activityIds = await db.CustomerActivities.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var documents = await db.CustomerDocuments.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.FilePath }).ToListAsync(ct);
        var translationIds = await db.EntityTranslations.AsNoTracking()
            .Where(item => item.EntityType == EntityTypes.Customer && item.EntityId == customerId)
            .OrderBy(item => item.Id).Select(item => item.Id).ToListAsync(ct);
        var leadIds = await db.Leads.AsNoTracking()
            .Where(item => item.ConvertedCustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var projectDocumentIds = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
        var opportunityRecords = await db.Opportunities.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Name }).ToListAsync(ct);
        var tenderRecords = await db.Tenders.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Code, item.Name }).ToListAsync(ct);
        var contractRecords = await db.Contracts.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.ContractNumber }).ToListAsync(ct);
        var designProjectRecords = await db.DesignProjects.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.ProjectCode, item.Name }).ToListAsync(ct);
        var operationalProjectRecords = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.CustomerId == customerId).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.Code, item.Name }).ToListAsync(ct);

        var localDefinitions = new List<HardDeleteItemDefinition>();
        var fileBlockers = new List<string>();
        foreach (var document in documents)
        {
            try
            {
                var path = files.ValidateManagedPath(document.FilePath);
                if (!path.StartsWith($"/files/customers/{customerId}/", StringComparison.Ordinal))
                {
                    fileBlockers.Add($"outside-root:{document.Id}:{path}");
                    continue;
                }
                localDefinitions.Add(new HardDeleteItemDefinition(
                    HardDeleteItemKind.LocalFile, path, localDefinitions.Count));
            }
            catch (HardDeleteFileException)
            {
                fileBlockers.Add($"invalid:{document.Id}:{document.FilePath}");
            }
        }

        var duplicatePaths = localDefinitions
            .GroupBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key).Order(StringComparer.Ordinal).ToList();
        fileBlockers.AddRange(duplicatePaths.Select(path => $"duplicate-local:{path}"));
        var candidatePaths = localDefinitions.Select(item => item.ActionIdentifier)
            .ToHashSet(StringComparer.Ordinal);
        var otherCustomerPaths = await db.CustomerDocuments.AsNoTracking()
            .Where(item => item.CustomerId != customerId)
            .Select(item => item.FilePath).ToListAsync(ct);
        var projectDocumentPaths = await db.ProjectDocuments.AsNoTracking()
            .Select(item => item.LocalPath).ToListAsync(ct);
        var sharedCustomerPaths = NormalizeSharedPaths(otherCustomerPaths, candidatePaths);
        var sharedProjectPaths = NormalizeSharedPaths(projectDocumentPaths, candidatePaths);
        fileBlockers.AddRange(sharedCustomerPaths.Select(path => $"shared-customer-document:{path}"));
        fileBlockers.AddRange(sharedProjectPaths.Select(path => $"shared-project-document:{path}"));
        var sharedPaths = sharedCustomerPaths.Concat(sharedProjectPaths)
            .ToHashSet(StringComparer.Ordinal);
        localDefinitions = localDefinitions
            .Where(item => !duplicatePaths.Contains(item.ActionIdentifier, StringComparer.Ordinal) &&
                !sharedPaths.Contains(item.ActionIdentifier))
            .OrderBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Select((item, index) => item with { Sequence = index }).ToList();
        fileBlockers = fileBlockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var contacts = contactIds.Select(Identifier).ToList();
        var activities = activityIds.Select(Identifier).ToList();
        var documentIdentifiers = documents.Select(item => Identifier(item.Id)).ToList();
        var translations = translationIds.Select(Identifier).ToList();
        var leads = leadIds.Select(Identifier).ToList();
        var projectDocuments = projectDocumentIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        var opportunities = opportunityRecords.Select(item => Identifier(item.Id)).ToList();
        var tenders = tenderRecords.Select(item => Identifier(item.Id)).ToList();
        var contracts = contractRecords.Select(item => Identifier(item.Id)).ToList();
        var designProjects = designProjectRecords.Select(item => Identifier(item.Id)).ToList();
        var operationalProjects = operationalProjectRecords.Select(item => Identifier(item.Id)).ToList();
        var opportunityLinks = opportunityRecords.Select(item => DetailLink(
            item.Name, $"/admin/opportunities/{item.Id}")).ToList();
        var tenderLinks = tenderRecords.Select(item => DetailLink(
            $"{item.Code} · {item.Name}", $"/admin/tenders/{item.Id}")).ToList();
        var contractLinks = contractRecords.Select(item => DetailLink(
            item.ContractNumber, $"/admin/contracts/{item.Id}")).ToList();
        var designProjectLinks = designProjectRecords.Select(item => DetailLink(
            $"{item.ProjectCode} · {item.Name}", $"/admin/design-projects/{item.Id}")).ToList();
        var operationalProjectLinks = operationalProjectRecords.Select(item => DetailLink(
            $"{item.Code} · {item.Name}", $"/admin/operational-projects/{item.Id}")).ToList();
        var localPaths = localDefinitions.Select(item => item.ActionIdentifier).ToList();

        var impactItems = new List<DeletionImpactItemResponse>();
        AddImpact(impactItems, "customer.contacts", contacts);
        AddImpact(impactItems, "customer.activities", activities);
        AddImpact(impactItems, "customer.documents", documentIdentifiers);
        AddImpact(impactItems, "customer.localFiles", localPaths);
        AddImpact(impactItems, "customer.fileBlockers", fileBlockers, DeletionImpactActions.Block);
        AddImpact(impactItems, "customer.translations", translations);
        AddImpact(impactItems, "customer.convertedLeads", leads, DeletionImpactActions.Unlink);
        AddImpact(impactItems, "customer.projectDocuments", projectDocuments, DeletionImpactActions.Unlink);
        AddImpact(impactItems, "customer.opportunities", opportunities, DeletionImpactActions.Block,
            $"/admin/opportunities?customerId={customerId}", opportunityLinks);
        AddImpact(impactItems, "customer.tenders", tenders, DeletionImpactActions.Block,
            $"/admin/tenders?customerId={customerId}", tenderLinks);
        AddImpact(impactItems, "customer.contracts", contracts, DeletionImpactActions.Block,
            $"/admin/contracts?customerId={customerId}", contractLinks);
        AddImpact(impactItems, "customer.designProjects", designProjects, DeletionImpactActions.Block,
            $"/admin/design-projects?customerId={customerId}", designProjectLinks);
        AddImpact(impactItems, "customer.operationalProjects", operationalProjects, DeletionImpactActions.Block,
            $"/admin/operational-projects?customerId={customerId}", operationalProjectLinks);

        var rowVersion = CrmConcurrency.Encode(customer.RowVersion);
        var confirmation = $"CUSTOMER-{customer.Id}";
        var tokenSource = string.Join('|',
            $"customer.root:{customer.Id}:{customer.Name}:{rowVersion}",
            $"customer.contacts:{string.Join(',', contacts)}",
            $"customer.activities:{string.Join(',', activities)}",
            $"customer.documents:{string.Join(',', documentIdentifiers)}",
            $"customer.localFiles:{string.Join(',', localPaths)}",
            $"customer.fileBlockers:{string.Join(',', fileBlockers)}",
            $"customer.translations:{string.Join(',', translations)}",
            $"customer.convertedLeads:{string.Join(',', leads)}",
            $"customer.projectDocuments:{string.Join(',', projectDocuments)}",
            $"customer.opportunities:{string.Join(',', opportunities)}",
            $"customer.tenders:{string.Join(',', tenders)}",
            $"customer.contracts:{string.Join(',', contracts)}",
            $"customer.designProjects:{string.Join(',', designProjects)}",
            $"customer.operationalProjects:{string.Join(',', operationalProjects)}");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{EntityTypes.Customer}:{tokenSource}"))).ToLowerInvariant();
        var blockerCount = fileBlockers.Count + opportunities.Count + tenders.Count +
            contracts.Count + designProjects.Count + operationalProjects.Count;
        var impact = new DeletionImpactResponse
        {
            ResourceType = EntityTypes.Customer,
            ResourceId = customer.Id,
            ResourceLabel = customer.Name,
            RequiredConfirmation = confirmation,
            PlanToken = token,
            CanDelete = blockerCount == 0,
            TotalAffected = 1 + contacts.Count + activities.Count + documentIdentifiers.Count +
                localPaths.Count + fileBlockers.Count + translations.Count + leads.Count +
                projectDocuments.Count + opportunities.Count + tenders.Count + contracts.Count +
                designProjects.Count + operationalProjects.Count,
            Items = impactItems,
        };
        var definitions = localDefinitions
            .Append(new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, "delete-customer-aggregate", localDefinitions.Count))
            .ToList();
        return new CrmHardDeletePlan(impact, definitions);
    }

    private IReadOnlyList<string> NormalizeSharedPaths(
        IEnumerable<string> paths,
        IReadOnlySet<string> candidatePaths)
    {
        var shared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            try
            {
                var normalized = files.ValidateManagedPath(path);
                if (candidatePaths.Contains(normalized)) shared.Add(normalized);
            }
            catch (HardDeleteFileException)
            {
            }
        }
        return shared.Order(StringComparer.Ordinal).ToList();
    }

    public async Task<CrmHardDeletePlan?> ForLeadAsync(int leadId, CancellationToken ct = default)
    {
        var lead = await db.Leads.AsNoTracking()
            .Where(item => item.Id == leadId)
            .Select(item => new { item.Id, item.Name })
            .SingleOrDefaultAsync(ct);
        if (lead is null) return null;

        var activityIds = await db.LeadActivities.AsNoTracking()
            .Where(item => item.LeadId == leadId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id.ToString(CultureInfo.InvariantCulture))
            .ToListAsync(ct);
        var translationIds = await db.EntityTranslations.AsNoTracking()
            .Where(item => item.EntityType == EntityTypes.Lead && item.EntityId == leadId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id.ToString(CultureInfo.InvariantCulture))
            .ToListAsync(ct);

        var items = new List<DeletionImpactItemResponse>();
        AddImpact(items, "lead.activities", activityIds);
        AddImpact(items, "lead.translations", translationIds);
        var confirmation = $"LEAD-{lead.Id}";
        var tokenSource = string.Join('|',
            $"lead.activities:{string.Join(',', activityIds)}",
            $"lead.translations:{string.Join(',', translationIds)}");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{EntityTypes.Lead}:{lead.Id}:{lead.Name}:{confirmation}:{tokenSource}"))).ToLowerInvariant();
        var impact = new DeletionImpactResponse
        {
            ResourceType = EntityTypes.Lead,
            ResourceId = lead.Id,
            ResourceLabel = lead.Name,
            RequiredConfirmation = confirmation,
            PlanToken = token,
            CanDelete = true,
            TotalAffected = 1 + activityIds.Count + translationIds.Count,
            Items = items,
        };
        return new CrmHardDeletePlan(impact,
        [
            new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, "delete-lead-aggregate", 0),
        ]);
    }

    public async Task<CrmHardDeletePlan?> ForTenderAsync(int tenderId, CancellationToken ct = default)
    {
        var tender = await db.Tenders.AsNoTracking()
            .Where(item => item.Id == tenderId)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.Name,
                item.Status,
                item.WonOpportunityId,
            })
            .SingleOrDefaultAsync(ct);
        if (tender is null) return null;

        var checklist = await db.TenderChecklistItems.AsNoTracking()
            .Where(item => item.TenderId == tenderId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.FilePath, item.CapabilityDocumentId })
            .ToListAsync(ct);
        var revisionIds = await db.TenderEstimateRevisions.AsNoTracking()
            .Where(item => item.TenderId == tenderId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var lineIds = await db.TenderEstimateLines.AsNoTracking()
            .Where(item => revisionIds.Contains(item.RevisionId))
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var opportunityIds = await db.Opportunities.AsNoTracking()
            .Where(item => item.WonTenderId == tenderId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);

        var checklistIds = checklist.Select(item => Identifier(item.Id)).ToList();
        var revisionIdentifiers = revisionIds.Select(Identifier).ToList();
        var lineIdentifiers = lineIds.Select(Identifier).ToList();
        var opportunityIdentifiers = opportunityIds.Select(Identifier).ToList();
        var capabilityIdentifiers = checklist
            .Where(item => item.CapabilityDocumentId.HasValue)
            .Select(item => Identifier(item.CapabilityDocumentId!.Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var localPaths = new List<string>();
        var fileBlockers = new List<string>();
        foreach (var item in checklist.Where(item =>
            !item.CapabilityDocumentId.HasValue && !string.IsNullOrWhiteSpace(item.FilePath)))
        {
            try
            {
                var path = files.ValidateManagedPath(item.FilePath!);
                if (!path.StartsWith("/files/tenders/", StringComparison.Ordinal))
                {
                    fileBlockers.Add($"{item.Id}:{path}");
                    continue;
                }
                localPaths.Add(path);
            }
            catch (HardDeleteFileException)
            {
                fileBlockers.Add($"{item.Id}:{item.FilePath}");
            }
        }
        localPaths = localPaths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        fileBlockers = fileBlockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var impactItems = new List<DeletionImpactItemResponse>();
        AddImpact(impactItems, "tender.checklistItems", checklistIds);
        AddImpact(impactItems, "tender.estimateRevisions", revisionIdentifiers);
        AddImpact(impactItems, "tender.estimateLines", lineIdentifiers);
        AddImpact(impactItems, "tender.localFiles", localPaths);
        AddImpact(impactItems, "tender.fileBlockers", fileBlockers, DeletionImpactActions.Block);
        AddImpact(impactItems, "tender.capabilityDocuments", capabilityIdentifiers,
            DeletionImpactActions.Unlink);
        AddImpact(impactItems, "tender.winningOpportunities", opportunityIdentifiers,
            DeletionImpactActions.Unlink);
        var wonOpportunityIdentifiers = tender.WonOpportunityId.HasValue
            ? new[] { Identifier(tender.WonOpportunityId.Value) }
            : [];
        AddImpact(impactItems, "tender.wonOpportunity", wonOpportunityIdentifiers,
            DeletionImpactActions.Unlink);
        var canDelete = tender.Status == TenderStatus.Preparing && fileBlockers.Count == 0;
        if (tender.Status != TenderStatus.Preparing)
        {
            impactItems.Add(new DeletionImpactItemResponse
            {
                Key = "tender.status",
                Action = DeletionImpactActions.Block,
                Count = 1,
                Examples = [tender.Status.ToString()],
            });
        }

        var confirmation = tender.Code;
        var tokenSource = string.Join('|',
            $"tender.checklistItems:{string.Join(',', checklistIds)}",
            $"tender.estimateRevisions:{string.Join(',', revisionIdentifiers)}",
            $"tender.estimateLines:{string.Join(',', lineIdentifiers)}",
            $"tender.localFiles:{string.Join(',', localPaths)}",
            $"tender.fileBlockers:{string.Join(',', fileBlockers)}",
            $"tender.capabilityDocuments:{string.Join(',', capabilityIdentifiers)}",
            $"tender.winningOpportunities:{string.Join(',', opportunityIdentifiers)}",
            $"tender.wonOpportunity:{string.Join(',', wonOpportunityIdentifiers)}",
            $"tender.status:{tender.Status}");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{EntityTypes.Tender}:{tender.Id}:{tender.Name}:{confirmation}:{tokenSource}"))).ToLowerInvariant();
        var impact = new DeletionImpactResponse
        {
            ResourceType = EntityTypes.Tender,
            ResourceId = tender.Id,
            ResourceLabel = tender.Name,
            RequiredConfirmation = confirmation,
            PlanToken = token,
            CanDelete = canDelete,
            TotalAffected = 1 + checklistIds.Count + revisionIdentifiers.Count +
                lineIdentifiers.Count + localPaths.Count + fileBlockers.Count + capabilityIdentifiers.Count +
                opportunityIdentifiers.Count + wonOpportunityIdentifiers.Length,
            Items = impactItems,
        };
        var definitions = localPaths.Select((path, index) =>
                new HardDeleteItemDefinition(HardDeleteItemKind.LocalFile, path, index))
            .Append(new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, "delete-tender-aggregate", localPaths.Count))
            .ToList();
        return new CrmHardDeletePlan(impact, definitions);
    }

    public async Task<CrmHardDeletePlan?> ForQuoteAsync(int quoteId, CancellationToken ct = default)
    {
        var driveOptions = await settingsStore.GetRuntimeAsync(ct);
        var quote = await db.Quotes.AsNoTracking()
            .Where(item => item.Id == quoteId)
            .Select(item => new
            {
                item.Id,
                item.Code,
                item.OperationalProjectId,
                item.RowVersion,
            })
            .SingleOrDefaultAsync(ct);
        if (quote is null) return null;

        var itemIds = await db.QuoteItems.AsNoTracking()
            .Where(item => item.QuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var documents = await db.QuoteDocuments.AsNoTracking()
            .Where(item => item.QuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.FilePath })
            .ToListAsync(ct);
        var approvalLogIds = await db.QuoteApprovalLogs.AsNoTracking()
            .Where(item => item.QuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var snapshotIds = await db.QuoteVersionSnapshots.AsNoTracking()
            .Where(item => item.QuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var translationIds = await db.EntityTranslations.AsNoTracking()
            .Where(item => item.EntityType == EntityTypes.Quote && item.EntityId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var opportunityIds = await db.Opportunities.AsNoTracking()
            .Where(item => item.WonQuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var contractIds = await db.Contracts.AsNoTracking()
            .Where(item => item.QuoteId == quoteId)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(ct);
        var documentIds = documents.Select(item => (long)item.Id).ToList();
        var documentPaths = documents.Select(item => item.FilePath).Distinct().ToList();
        var sidecars = await db.ProjectDocuments.AsNoTracking()
            .Where(item => documentPaths.Contains(item.LocalPath) ||
                quote.OperationalProjectId.HasValue &&
                item.OperationalProjectId == quote.OperationalProjectId.Value &&
                item.SourceRecordId.HasValue && documentIds.Contains(item.SourceRecordId.Value) ||
                item.SourceModule == ProjectDocumentSourceModule.Crm &&
                item.SourceEntityType == nameof(QuoteDocument) && item.SourceSlot == "file" &&
                item.SourceRecordId.HasValue && documentIds.Contains(item.SourceRecordId.Value))
            .OrderBy(item => item.Id)
            .ToListAsync(ct);

        var localDefinitions = new List<HardDeleteItemDefinition>();
        var normalizedDocumentPaths = new Dictionary<long, string>();
        var fileBlockers = new List<string>();
        foreach (var document in documents)
        {
            try
            {
                var path = files.ValidateManagedPath(document.FilePath);
                var expectedPrefix = $"/files/quotes/{quote.Id}/";
                if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    fileBlockers.Add($"outside-root:{document.Id}:{path}");
                    continue;
                }
                normalizedDocumentPaths[document.Id] = path;
                localDefinitions.Add(new HardDeleteItemDefinition(
                    HardDeleteItemKind.LocalFile, path, localDefinitions.Count));
            }
            catch (HardDeleteFileException)
            {
                fileBlockers.Add($"invalid:{document.Id}:{document.FilePath}");
            }
        }

        var duplicatePaths = localDefinitions
            .GroupBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        fileBlockers.AddRange(duplicatePaths.Select(path => $"duplicate-local:{path}"));
        var localPathSet = localDefinitions.Select(item => item.ActionIdentifier).ToList();
        var crossQuotePaths = await db.QuoteDocuments.AsNoTracking()
            .Where(item => item.QuoteId != quoteId && localPathSet.Contains(item.FilePath))
            .Select(item => item.FilePath)
            .Distinct()
            .OrderBy(path => path)
            .ToListAsync(ct);
        fileBlockers.AddRange(crossQuotePaths.Select(path => $"shared-local:{path}"));
        localDefinitions = localDefinitions
            .Where(item => !duplicatePaths.Contains(item.ActionIdentifier, StringComparer.Ordinal) &&
                !crossQuotePaths.Contains(item.ActionIdentifier, StringComparer.Ordinal))
            .OrderBy(item => item.ActionIdentifier, StringComparer.Ordinal)
            .Select((item, index) => item with { Sequence = index })
            .ToList();

        var candidateDriveIds = sidecars
            .Where(item => !string.IsNullOrWhiteSpace(item.DriveFileId))
            .Select(item => item.DriveFileId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var matchingDriveIds = await db.ProjectDocuments.AsNoTracking()
            .Where(item => item.DriveFileId != null && candidateDriveIds.Contains(item.DriveFileId))
            .Select(item => item.DriveFileId!)
            .ToListAsync(ct);
        var duplicateDriveIds = matchingDriveIds
            .GroupBy(item => item, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var sidecarIdentifiers = new List<string>();
        var sidecarBlockers = new List<string>();
        var driveDefinitions = new List<HardDeleteItemDefinition>();
        var driveIdentifiers = new List<string>();
        foreach (var sidecar in sidecars)
        {
            var identity = SidecarIdentity(sidecar);
            if (!IsSafeQuoteSidecar(
                sidecar, normalizedDocumentPaths, quote.OperationalProjectId, driveOptions.InstanceId) ||
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
                localDefinitions.Count + driveDefinitions.Count,
                new Dictionary<string, string>
                {
                    ["niconInstance"] = driveOptions.InstanceId,
                    ["niconReplicaKey"] = $"project-document:{sidecar.Id}",
                    ["niconGeneration"] = sidecar.Generation.ToString(CultureInfo.InvariantCulture),
                },
                sidecar.DriveFolderId));
        }

        fileBlockers = fileBlockers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        sidecarIdentifiers.Sort(StringComparer.Ordinal);
        sidecarBlockers.Sort(StringComparer.Ordinal);
        driveIdentifiers.Sort(StringComparer.Ordinal);
        var itemIdentifiers = itemIds.Select(Identifier).ToList();
        var documentIdentifiers = documents.Select(item => Identifier(item.Id)).ToList();
        var approvalLogIdentifiers = approvalLogIds.Select(Identifier).ToList();
        var snapshotIdentifiers = snapshotIds.Select(Identifier).ToList();
        var translationIdentifiers = translationIds.Select(Identifier).ToList();
        var opportunityIdentifiers = opportunityIds.Select(Identifier).ToList();
        var contractIdentifiers = contractIds.Select(Identifier).ToList();
        var localPaths = localDefinitions.Select(item => item.ActionIdentifier).ToList();

        var impactItems = new List<DeletionImpactItemResponse>();
        AddImpact(impactItems, "quote.items", itemIdentifiers);
        AddImpact(impactItems, "quote.documents", documentIdentifiers);
        AddImpact(impactItems, "quote.approvalLogs", approvalLogIdentifiers);
        AddImpact(impactItems, "quote.versionSnapshots", snapshotIdentifiers);
        AddImpact(impactItems, "quote.translations", translationIdentifiers);
        AddImpact(impactItems, "quote.localFiles", localPaths);
        AddImpact(impactItems, "quote.fileBlockers", fileBlockers, DeletionImpactActions.Block);
        AddImpact(impactItems, "quote.driveFiles", driveIdentifiers);
        AddImpact(impactItems, "quote.projectDocumentSidecars", sidecarIdentifiers, DeletionImpactActions.Unlink);
        AddImpact(impactItems, "quote.projectDocumentSidecarBlockers", sidecarBlockers, DeletionImpactActions.Block);
        AddImpact(impactItems, "quote.winningOpportunities", opportunityIdentifiers, DeletionImpactActions.Unlink);
        AddImpact(impactItems, "quote.contracts", contractIdentifiers, DeletionImpactActions.Unlink);

        var rowVersion = CrmConcurrency.Encode(quote.RowVersion);
        var tokenSource = string.Join('|',
            $"quote.root:{quote.Id}:{quote.Code}:{rowVersion}",
            $"quote.items:{string.Join(',', itemIdentifiers)}",
            $"quote.documents:{string.Join(',', documentIdentifiers)}",
            $"quote.approvalLogs:{string.Join(',', approvalLogIdentifiers)}",
            $"quote.versionSnapshots:{string.Join(',', snapshotIdentifiers)}",
            $"quote.translations:{string.Join(',', translationIdentifiers)}",
            $"quote.localFiles:{string.Join(',', localPaths)}",
            $"quote.fileBlockers:{string.Join(',', fileBlockers)}",
            $"quote.driveInstance:{driveOptions.InstanceId}",
            $"quote.driveFiles:{string.Join(',', driveIdentifiers)}",
            $"quote.projectDocumentSidecars:{string.Join(',', sidecarIdentifiers)}",
            $"quote.projectDocumentSidecarBlockers:{string.Join(',', sidecarBlockers)}",
            $"quote.winningOpportunities:{string.Join(',', opportunityIdentifiers)}",
            $"quote.contracts:{string.Join(',', contractIdentifiers)}");
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{EntityTypes.Quote}:{tokenSource}"))).ToLowerInvariant();
        var impact = new DeletionImpactResponse
        {
            ResourceType = EntityTypes.Quote,
            ResourceId = quote.Id,
            ResourceLabel = quote.Code,
            RequiredConfirmation = quote.Code,
            PlanToken = token,
            CanDelete = fileBlockers.Count == 0 && sidecarBlockers.Count == 0,
            TotalAffected = 1 + itemIdentifiers.Count + documentIdentifiers.Count +
                approvalLogIdentifiers.Count + snapshotIdentifiers.Count + translationIdentifiers.Count +
                localPaths.Count + fileBlockers.Count + driveIdentifiers.Count +
                sidecarIdentifiers.Count + sidecarBlockers.Count +
                opportunityIdentifiers.Count + contractIdentifiers.Count,
            Items = impactItems,
        };
        var definitions = localDefinitions
            .Concat(driveDefinitions)
            .Append(new HardDeleteItemDefinition(
                HardDeleteItemKind.DatabaseAggregate, "delete-quote-aggregate",
                localDefinitions.Count + driveDefinitions.Count))
            .ToList();
        return new CrmHardDeletePlan(impact, definitions);
    }

    private static bool IsSafeQuoteSidecar(
        ProjectDocument sidecar,
        IReadOnlyDictionary<long, string> normalizedDocumentPaths,
        int? operationalProjectId,
        string instanceId)
    {
        if (!operationalProjectId.HasValue ||
            sidecar.OperationalProjectId != operationalProjectId.Value ||
            sidecar.SourceModule != ProjectDocumentSourceModule.Crm ||
            sidecar.SourceType != ProjectDocumentSourceType.ExistingManagedFile ||
            !string.Equals(sidecar.SourceEntityType, nameof(QuoteDocument), StringComparison.Ordinal) ||
            !string.Equals(sidecar.SourceSlot, "file", StringComparison.Ordinal) ||
            sidecar.Origin != ProjectDocumentOrigin.Nicon ||
            sidecar.ConflictState != ProjectDocumentConflictState.None ||
            sidecar.ConflictWithDocumentId.HasValue ||
            !string.IsNullOrWhiteSpace(sidecar.ConflictObservedDriveFileId) ||
            !string.IsNullOrWhiteSpace(sidecar.ConflictObservedDriveVersion) ||
            sidecar.DesiredOperation != ProjectDocumentDesiredOperation.None ||
            sidecar.SyncStatus == ProjectDocumentSyncStatus.Processing ||
            sidecar.ClaimToken.HasValue ||
            sidecar.ClaimExpiresAt.HasValue ||
            !sidecar.SourceRecordId.HasValue ||
            !normalizedDocumentPaths.TryGetValue(sidecar.SourceRecordId.Value, out var documentPath) ||
            !string.Equals(sidecar.LocalPath, documentPath, StringComparison.Ordinal))
        {
            return false;
        }

        return sidecar.SyncStatus switch
        {
            ProjectDocumentSyncStatus.Synced =>
                !string.IsNullOrWhiteSpace(instanceId) &&
                !string.IsNullOrWhiteSpace(sidecar.DriveFileId) &&
                !string.IsNullOrWhiteSpace(sidecar.DriveFolderId) &&
                sidecar.Generation > 0,
            ProjectDocumentSyncStatus.Deleted => string.IsNullOrWhiteSpace(sidecar.DriveFileId),
            _ => false,
        };
    }

    private static void AddImpact(
        ICollection<DeletionImpactItemResponse> items,
        string key,
        IReadOnlyList<string> identifiers,
        string action = DeletionImpactActions.Delete,
        string? resolutionUrl = null,
        IReadOnlyList<DeletionImpactLinkResponse>? resolutionLinks = null)
    {
        if (identifiers.Count == 0) return;
        items.Add(new DeletionImpactItemResponse
        {
            Key = key,
            Action = action,
            Count = identifiers.Count,
            Examples = identifiers.Take(3).ToList(),
            ResolutionLinks = resolutionLinks?.Take(3).ToList() ?? [],
            ResolutionUrl = resolutionUrl,
        });
    }

    private static DeletionImpactLinkResponse DetailLink(string label, string url) => new()
    {
        Label = label,
        Url = url,
    };

    private static string Identifier(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static string SidecarIdentity(ProjectDocument document) => string.Join(':',
        "project-document", document.Id, document.OperationalProjectId, document.SourceModule,
        document.SourceType, document.SourceEntityType, document.SourceSlot, document.SourceRecordId,
        document.LocalPath, document.Origin, document.Generation, document.SyncStatus,
        document.DesiredOperation, document.ConflictState, document.DriveFileId,
        document.DriveFolderId, document.ClaimToken, document.ClaimExpiresAt?.ToString("O"),
        document.ConflictWithDocumentId, document.ConflictObservedDriveFileId,
        document.ConflictObservedDriveVersion,
        document.SyncAttemptCount, document.SyncError, document.NextSyncAttemptAt?.ToString("O"),
        document.LastSyncAttemptAt?.ToString("O"), document.DeletedAt?.ToString("O"),
        document.DeletedByUserId);
}

public sealed class CustomerHardDeleteHandler(
    AppDbContext db,
    ICrmHardDeletePlanService plans,
    IPermissionService permissions) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.Customer;

    public async Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var customerId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteAuthorizationException("Thông tin phân quyền tác vụ xóa không hợp lệ.");
        var canManage = await permissions.HasAsync(requestedBy, "crm.customers.manage", ct);
        var canSeeAll = await permissions.HasAsync(requestedBy, "crm.customers.view.all", ct);
        var customer = await db.Customers.AsNoTracking()
            .Where(item => item.Id == customerId)
            .Select(item => new { item.OwnerUserId }).SingleOrDefaultAsync(ct);
        if (!canManage)
            throw new HardDeleteAuthorizationException(
                "Quyền xóa khách hàng hoặc phạm vi sở hữu đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");
        if (customer is null && context.IsForwardRecovery) return;
        if (customer is null || !canSeeAll && customer.OwnerUserId != requestedBy)
            throw new HardDeleteAuthorizationException(
                "Quyền xóa khách hàng hoặc phạm vi sở hữu đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");
    }

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var customerId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Id == customerId, ct);
        if (customer is null)
        {
            AddCompletionAuditIfMissing(context, requestedBy, customerId);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }

        var current = await plans.ForCustomerAsync(customerId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy khách hàng.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        if (!current.Impact.CanDelete)
            throw new CustomerOperationException(
                "Không thể xoá khách hàng khi còn dữ liệu nghiệp vụ độc lập hoặc tệp chưa an toàn.");
        try
        {
            var leads = await db.Leads.Where(item => item.ConvertedCustomerId == customerId).ToListAsync(ct);
            foreach (var lead in leads) lead.ConvertedCustomerId = null;
            var projectDocuments = await db.ProjectDocuments
                .Where(item => item.CustomerId == customerId).ToListAsync(ct);
            foreach (var document in projectDocuments) document.CustomerId = null;

            await db.CustomerContacts.Where(item => item.CustomerId == customerId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            await db.CustomerActivities.Where(item => item.CustomerId == customerId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            await db.CustomerDocuments.Where(item => item.CustomerId == customerId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            await db.EntityTranslations
                .Where(item => item.EntityType == EntityTypes.Customer && item.EntityId == customerId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            if (customer.Name.StartsWith("[SAMPLE]", StringComparison.Ordinal) &&
                !await db.SeededRootDeletions.AnyAsync(item =>
                    item.ResourceType == EntityTypes.Customer && item.ResourceKey == customer.Name, ct))
            {
                db.SeededRootDeletions.Add(new SeededRootDeletion
                {
                    ResourceType = EntityTypes.Customer,
                    ResourceKey = customer.Name,
                    DeletedByUserId = requestedBy,
                });
            }
            AddCompletionAuditIfMissing(context, requestedBy, customerId);
            db.Customers.Remove(customer);
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

    private void AddCompletionAuditIfMissing(
        HardDeleteResourceContext context, int requestedBy, int customerId)
    {
        var auditId = context.OperationId.ToString("N");
        if (db.AuditLogs.Local.Any(item => item.AuditId == auditId) ||
            db.AuditLogs.Any(item => item.AuditId == auditId)) return;
        db.AuditLogs.Add(new AuditLog
        {
            AuditId = auditId,
            CreatedAt = DateTime.UtcNow,
            ActorUserId = requestedBy,
            ActorType = "user",
            Action = "customer.delete",
            ResourceType = EntityTypes.Customer,
            ResourceId = customerId.ToString(CultureInfo.InvariantCulture),
            Message = $"Customer #{customerId} durable deletion completed.",
            Channel = "job",
            Status = "success",
            CorrelationId = context.OperationId.ToString(),
        });
    }
}

public sealed class LeadHardDeleteHandler(
    AppDbContext db,
    ICrmHardDeletePlanService plans) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.Lead;

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var leadId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var lead = await db.Leads.SingleOrDefaultAsync(item => item.Id == leadId, ct);
        if (lead is null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }

        var current = await plans.ForLeadAsync(leadId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy Lead.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        try
        {
            await db.LeadActivities.Where(item => item.LeadId == leadId).ExecuteDeleteOrRemoveAsync(db, ct);
            await db.EntityTranslations
                .Where(item => item.EntityType == EntityTypes.Lead && item.EntityId == leadId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            db.Leads.Remove(lead);
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

public sealed class TenderHardDeleteHandler(
    AppDbContext db,
    ICrmHardDeletePlanService plans) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.Tender;

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var tenderId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var tender = await db.Tenders.SingleOrDefaultAsync(item => item.Id == tenderId, ct);
        if (tender is null)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }

        var current = await plans.ForTenderAsync(tenderId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy gói thầu.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        if (!current.Impact.CanDelete)
            throw new TenderOperationException("Chỉ gói thầu đang Chuẩn bị mới có thể bị xóa.");
        try
        {
            var revisionIds = db.TenderEstimateRevisions
                .Where(item => item.TenderId == tenderId)
                .Select(item => item.Id);
            await db.TenderEstimateLines
                .Where(item => revisionIds.Contains(item.RevisionId))
                .ExecuteDeleteOrRemoveAsync(db, ct);
            await db.TenderEstimateRevisions
                .Where(item => item.TenderId == tenderId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            await db.TenderChecklistItems
                .Where(item => item.TenderId == tenderId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            var winningOpportunities = await db.Opportunities
                .Where(item => item.WonTenderId == tenderId)
                .ToListAsync(ct);
            foreach (var opportunity in winningOpportunities) opportunity.WonTenderId = null;
            db.Tenders.Remove(tender);
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

public sealed class QuoteHardDeleteHandler(
    AppDbContext db,
    ICrmHardDeletePlanService plans,
    IPermissionService permissions) : IHardDeleteResourceHandler
{
    public string ResourceType => EntityTypes.Quote;

    public async Task AuthorizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var quoteId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteAuthorizationException("Thông tin phân quyền tác vụ xóa không hợp lệ.");
        var canManage = await permissions.HasAsync(requestedBy, "crm.quotes.manage", ct);
        var canSeeAll = await permissions.HasAsync(requestedBy, "crm.quotes.view.all", ct);
        var quote = await db.Quotes.AsNoTracking()
            .Where(item => item.Id == quoteId)
            .Select(item => new { item.OwnerUserId })
            .SingleOrDefaultAsync(ct);
        if (quote is null && context.IsForwardRecovery) return;
        if (!canManage || quote is null || !canSeeAll && quote.OwnerUserId != requestedBy)
            throw new HardDeleteAuthorizationException(
                "Quyền xóa báo giá hoặc phạm vi sở hữu đã thay đổi. Cần người có thẩm quyền xem xét tác vụ.");
    }

    public async Task FinalizeAsync(HardDeleteResourceContext context, CancellationToken ct = default)
    {
        if (!int.TryParse(context.ResourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var quoteId) ||
            !int.TryParse(context.RequestedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var requestedBy))
            throw new HardDeleteOperationException("invalid_resource_context", "Thông tin tác vụ xóa không hợp lệ.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            : null;
        var quote = await db.Quotes.SingleOrDefaultAsync(item => item.Id == quoteId, ct);
        if (quote is null)
        {
            AddCompletionAuditIfMissing(context, requestedBy, quoteId);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return;
        }

        var current = await plans.ForQuoteAsync(quoteId, ct)
            ?? throw new HardDeleteOperationException("resource_not_found", "Không tìm thấy báo giá.");
        DesignProjectHardDeleteHandler.EnsurePlan(context.PlanToken, current.Impact.PlanToken);
        if (!current.Impact.CanDelete)
            throw new QuoteOperationException("Không thể xoá báo giá vì còn tệp cần được xử lý an toàn.");
        try
        {
            var documentIds = await db.QuoteDocuments
                .Where(item => item.QuoteId == quoteId)
                .Select(item => (long)item.Id)
                .ToListAsync(ct);
            var sidecars = await db.ProjectDocuments
                .Where(item => item.SourceModule == ProjectDocumentSourceModule.Crm &&
                    item.SourceEntityType == nameof(QuoteDocument) && item.SourceSlot == "file" &&
                    item.SourceRecordId.HasValue && documentIds.Contains(item.SourceRecordId.Value))
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

            var opportunities = await db.Opportunities
                .Where(item => item.WonQuoteId == quoteId).ToListAsync(ct);
            foreach (var opportunity in opportunities) opportunity.WonQuoteId = null;
            var contracts = await db.Contracts
                .Where(item => item.QuoteId == quoteId).ToListAsync(ct);
            foreach (var contract in contracts) contract.QuoteId = null;

            await db.QuoteItems.Where(item => item.QuoteId == quoteId).ExecuteDeleteOrRemoveAsync(db, ct);
            await db.QuoteDocuments.Where(item => item.QuoteId == quoteId).ExecuteDeleteOrRemoveAsync(db, ct);
            await db.QuoteApprovalLogs.Where(item => item.QuoteId == quoteId).ExecuteDeleteOrRemoveAsync(db, ct);
            await db.QuoteVersionSnapshots.Where(item => item.QuoteId == quoteId).ExecuteDeleteOrRemoveAsync(db, ct);
            await db.EntityTranslations
                .Where(item => item.EntityType == EntityTypes.Quote && item.EntityId == quoteId)
                .ExecuteDeleteOrRemoveAsync(db, ct);
            if (IsSeededQuoteCode(quote.Code) && !await db.SeededRootDeletions.AnyAsync(item =>
                item.ResourceType == EntityTypes.Quote && item.ResourceKey == quote.Code, ct))
            {
                db.SeededRootDeletions.Add(new SeededRootDeletion
                {
                    ResourceType = EntityTypes.Quote,
                    ResourceKey = quote.Code,
                    DeletedByUserId = requestedBy,
                });
            }
            AddCompletionAuditIfMissing(context, requestedBy, quoteId);
            db.Quotes.Remove(quote);
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

    private static bool IsSeededQuoteCode(string code) =>
        code.StartsWith("QT-SAMPLE-", StringComparison.Ordinal);

    private void AddCompletionAuditIfMissing(
        HardDeleteResourceContext context, int requestedBy, int quoteId)
    {
        var auditId = context.OperationId.ToString("N");
        if (db.AuditLogs.Local.Any(item => item.AuditId == auditId) ||
            db.AuditLogs.Any(item => item.AuditId == auditId)) return;
        db.AuditLogs.Add(new AuditLog
        {
            AuditId = auditId,
            CreatedAt = DateTime.UtcNow,
            ActorUserId = requestedBy,
            ActorType = "user",
            Action = "quote.delete",
            ResourceType = EntityTypes.Quote,
            ResourceId = quoteId.ToString(CultureInfo.InvariantCulture),
            Message = $"Quote #{quoteId} durable deletion completed.",
            Channel = "job",
            Status = "success",
            CorrelationId = context.OperationId.ToString(),
        });
    }
}

internal static class HardDeleteQueryExtensions
{
    public static async Task ExecuteDeleteOrRemoveAsync<TEntity>(
        this IQueryable<TEntity> query,
        AppDbContext db,
        CancellationToken ct) where TEntity : class
    {
        if (db.Database.IsRelational())
        {
            await query.ExecuteDeleteAsync(ct);
            return;
        }

        db.RemoveRange(await query.ToListAsync(ct));
    }
}
