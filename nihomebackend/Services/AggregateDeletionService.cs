using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.Services;

internal static class AggregateDeletionService
{
    public static async Task<IReadOnlyList<int>> DeleteCustomerAsync(
        AppDbContext db,
        Customer customer,
        IProjectDocumentStagingService projectDocuments,
        int? userId,
        CancellationToken ct)
    {
        var customerId = customer.Id;
        var opportunityIds = await db.Opportunities
            .Where(opportunity => opportunity.CustomerId == customerId)
            .Select(opportunity => opportunity.Id)
            .ToListAsync(ct);
        var designProjectIds = await db.DesignProjects
            .Where(project => project.CustomerId == customerId)
            .Select(project => project.Id)
            .ToListAsync(ct);
        var contractIds = await db.Contracts
            .Where(contract => contract.CustomerId == customerId)
            .Select(contract => contract.Id)
            .ToListAsync(ct);
        var tenderIds = await db.Tenders
            .Where(tender => tender.CustomerId == customerId)
            .Select(tender => tender.Id)
            .ToListAsync(ct);

        await DeleteDesignProjectsAsync(db, designProjectIds, projectDocuments, userId, ct);
        var quoteIds = await DeleteOpportunitiesAsync(db, opportunityIds, projectDocuments, userId, ct);

        var convertedLeads = await db.Leads
            .Where(lead => lead.ConvertedCustomerId == customerId)
            .ToListAsync(ct);
        foreach (var lead in convertedLeads)
        {
            lead.ConvertedCustomerId = null;
        }

        var contracts = await db.Contracts
            .Where(contract => contractIds.Contains(contract.Id))
            .ToListAsync(ct);
        await StageContractDocumentDeletesAsync(db, contracts, projectDocuments, userId, ct);
        var tenders = await db.Tenders
            .Where(tender => tenderIds.Contains(tender.Id))
            .Include(tender => tender.ChecklistItems)
            .ToListAsync(ct);

        db.TenderChecklistItems.RemoveRange(tenders.SelectMany(tender => tender.ChecklistItems));
        db.Tenders.RemoveRange(tenders);
        db.Contracts.RemoveRange(contracts);
        await RemoveTranslationsAsync(db, EntityTypes.Contract, contractIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.Tender, tenderIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.Customer, new[] { customerId }, ct);
        db.Customers.Remove(customer);
        return quoteIds;
    }

    public static async Task<IReadOnlyList<int>> DeleteOpportunitiesAsync(
        AppDbContext db,
        IReadOnlyCollection<int> opportunityIds,
        IProjectDocumentStagingService projectDocuments,
        int? userId,
        CancellationToken ct)
    {
        if (opportunityIds.Count == 0) return Array.Empty<int>();

        var opportunities = await db.Opportunities
            .Where(opportunity => opportunityIds.Contains(opportunity.Id))
            .ToListAsync(ct);
        if (opportunities.Count == 0) return Array.Empty<int>();

        var foundIds = opportunities.Select(opportunity => opportunity.Id).ToList();
        var quoteIds = await db.Quotes
            .Where(quote => foundIds.Contains(quote.OpportunityId))
            .Select(quote => quote.Id)
            .ToListAsync(ct);
        var quotes = await db.Quotes
            .Where(quote => quoteIds.Contains(quote.Id))
            .ToListAsync(ct);
        await StageQuoteDocumentDeletesAsync(db, quotes, projectDocuments, userId, ct);
        var contracts = await db.Contracts
            .Where(contract => contract.OpportunityId.HasValue
                && foundIds.Contains(contract.OpportunityId.Value))
            .ToListAsync(ct);
        var surveys = await db.Surveys
            .Where(survey => survey.LinkedOpportunityId.HasValue
                && foundIds.Contains(survey.LinkedOpportunityId.Value))
            .ToListAsync(ct);
        var convertedLeads = await db.Leads
            .Where(lead => lead.ConvertedOpportunityId.HasValue
                && foundIds.Contains(lead.ConvertedOpportunityId.Value))
            .ToListAsync(ct);
        var winningTenders = await db.Tenders
            .Where(tender => tender.WonOpportunityId.HasValue
                && foundIds.Contains(tender.WonOpportunityId.Value))
            .ToListAsync(ct);

        foreach (var contract in contracts) contract.OpportunityId = null;
        foreach (var survey in surveys) survey.LinkedOpportunityId = null;
        foreach (var lead in convertedLeads) lead.ConvertedOpportunityId = null;
        foreach (var tender in winningTenders) tender.WonOpportunityId = null;

        db.Quotes.RemoveRange(quotes);
        db.Opportunities.RemoveRange(opportunities);
        await RemoveTranslationsAsync(db, EntityTypes.Quote, quoteIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.Opportunity, foundIds, ct);
        return quoteIds;
    }

    public static async Task DeleteDesignProjectsAsync(
        AppDbContext db,
        IReadOnlyCollection<int> designProjectIds,
        IProjectDocumentStagingService projectDocuments,
        int? userId,
        CancellationToken ct)
    {
        if (designProjectIds.Count == 0) return;

        var projects = await db.DesignProjects
            .Where(project => designProjectIds.Contains(project.Id))
            .ToListAsync(ct);
        if (projects.Count == 0) return;

        var foundIds = projects.Select(project => project.Id).ToList();
        await StageDesignDocumentDeletesAsync(db, projects, projectDocuments, userId, ct);
        var conceptIds = await db.ConceptOptions
            .Where(option => foundIds.Contains(option.DesignProjectId))
            .Select(option => option.Id)
            .ToListAsync(ct);
        var permitIds = await db.PermitChecklistItems
            .Where(permit => foundIds.Contains(permit.DesignProjectId))
            .Select(permit => permit.Id)
            .ToListAsync(ct);
        var basicDesignDocIds = await db.BasicDesignDocs
            .Where(document => foundIds.Contains(document.DesignProjectId))
            .Select(document => document.Id)
            .ToListAsync(ct);
        var shopDrawingIds = await db.ShopDrawings
            .Where(drawing => foundIds.Contains(drawing.DesignProjectId))
            .Select(drawing => drawing.Id)
            .ToListAsync(ct);
        var ifcReleaseIds = await db.IfcReleases
            .Where(release => foundIds.Contains(release.DesignProjectId))
            .Select(release => release.Id)
            .ToListAsync(ct);
        var taskIds = await db.ConstructionTasks
            .Where(task => foundIds.Contains(task.DesignProjectId))
            .Select(task => task.Id)
            .ToListAsync(ct);
        var siteDiaryIds = await db.SiteDiaries
            .Where(diary => foundIds.Contains(diary.DesignProjectId))
            .Select(diary => diary.Id)
            .ToListAsync(ct);
        var punchItemIds = await db.PunchItems
            .Where(item => foundIds.Contains(item.DesignProjectId))
            .Select(item => item.Id)
            .ToListAsync(ct);
        var asBuiltDocumentIds = await db.AsBuiltDocuments
            .Where(document => foundIds.Contains(document.DesignProjectId))
            .Select(document => document.Id)
            .ToListAsync(ct);

        var revisions = await db.DrawingRevisions
            .Where(revision =>
                (revision.TargetType == DrawingRevisionTargetType.BasicDesignDoc
                    && basicDesignDocIds.Contains(revision.TargetId))
                || (revision.TargetType == DrawingRevisionTargetType.ShopDrawing
                    && shopDrawingIds.Contains(revision.TargetId)))
            .ToListAsync(ct);
        var releaseItems = await db.IfcReleaseItems
            .Where(item => shopDrawingIds.Contains(item.ShopDrawingId))
            .ToListAsync(ct);
        var taskDependencies = await db.ConstructionTaskDependencies
            .Where(dependency => taskIds.Contains(dependency.TaskId)
                || taskIds.Contains(dependency.PredecessorTaskId))
            .ToListAsync(ct);
        var acceptanceRecords = await db.AcceptanceRecords
            .Where(record => foundIds.Contains(record.DesignProjectId))
            .ToListAsync(ct);
        var handoverRecords = await db.HandoverRecords
            .Where(record => foundIds.Contains(record.DesignProjectId))
            .ToListAsync(ct);
        var revisionIds = revisions.Select(revision => revision.Id).ToList();
        var acceptanceRecordIds = acceptanceRecords.Select(record => record.Id).ToList();
        var handoverRecordIds = handoverRecords.Select(record => record.Id).ToList();

        db.DrawingRevisions.RemoveRange(revisions);
        db.IfcReleaseItems.RemoveRange(releaseItems);
        db.ConstructionTaskDependencies.RemoveRange(taskDependencies);
        db.AcceptanceRecords.RemoveRange(acceptanceRecords);
        db.HandoverRecords.RemoveRange(handoverRecords);
        db.DesignProjects.RemoveRange(projects);

        await RemoveTranslationsAsync(db, EntityTypes.PermitChecklistItem, permitIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.ConceptOption, conceptIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.BasicDesignDoc, basicDesignDocIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.ShopDrawing, shopDrawingIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.DrawingRevision, revisionIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.IfcRelease, ifcReleaseIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.ConstructionTask, taskIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.SiteDiary, siteDiaryIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.PunchItem, punchItemIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.AcceptanceRecord, acceptanceRecordIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.AsBuiltDocument, asBuiltDocumentIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.HandoverRecord, handoverRecordIds, ct);
        await RemoveTranslationsAsync(db, EntityTypes.DesignProject, foundIds, ct);
    }

    private static async Task StageQuoteDocumentDeletesAsync(
        AppDbContext db, IReadOnlyCollection<Quote> quotes,
        IProjectDocumentStagingService projectDocuments, int? userId, CancellationToken ct)
    {
        var linked = quotes.Where(quote => quote.OperationalProjectId.HasValue).ToList();
        if (linked.Count == 0) return;
        var quoteIds = linked.Select(quote => quote.Id).ToList();
        var documents = await db.QuoteDocuments
            .Where(document => quoteIds.Contains(document.QuoteId))
            .ToListAsync(ct);
        var projectByQuote = linked.ToDictionary(quote => quote.Id, quote => quote.OperationalProjectId!.Value);
        foreach (var document in documents)
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                projectByQuote[document.QuoteId], ProjectDocumentSourceModule.Crm,
                nameof(QuoteDocument), "file", document.Id, document.FilePath, userId, ct);
    }

    private static async Task StageContractDocumentDeletesAsync(
        AppDbContext db, IReadOnlyCollection<Contract> contracts,
        IProjectDocumentStagingService projectDocuments, int? userId, CancellationToken ct)
    {
        var linked = contracts.Where(contract => contract.OperationalProjectId.HasValue).ToList();
        if (linked.Count == 0) return;
        var contractIds = linked.Select(contract => contract.Id).ToList();
        var projectByContract = linked.ToDictionary(contract => contract.Id, contract => contract.OperationalProjectId!.Value);
        var attachments = await db.ContractAttachments
            .Where(document => contractIds.Contains(document.ContractId)).ToListAsync(ct);
        foreach (var document in attachments)
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                projectByContract[document.ContractId], ProjectDocumentSourceModule.Crm,
                nameof(ContractAttachment), "file", document.Id, document.FilePath, userId, ct);
        var appendices = await db.ContractAppendices
            .Where(document => contractIds.Contains(document.ContractId) && document.FilePath != null).ToListAsync(ct);
        foreach (var document in appendices)
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                projectByContract[document.ContractId], ProjectDocumentSourceModule.Crm,
                nameof(ContractAppendix), "file", document.Id, document.FilePath!, userId, ct);
    }

    private static async Task StageDesignDocumentDeletesAsync(
        AppDbContext db, IReadOnlyCollection<DesignProject> projects,
        IProjectDocumentStagingService projectDocuments, int? userId, CancellationToken ct)
    {
        var linked = projects.Where(project => project.OperationalProjectId.HasValue).ToList();
        if (linked.Count == 0) return;
        var designProjectIds = linked.Select(project => project.Id).ToList();
        var projectByDesign = linked.ToDictionary(project => project.Id, project => project.OperationalProjectId!.Value);
        var basicDocuments = await db.BasicDesignDocs
            .Where(document => designProjectIds.Contains(document.DesignProjectId) && document.FilePath != null)
            .ToListAsync(ct);
        foreach (var document in basicDocuments)
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                projectByDesign[document.DesignProjectId], ProjectDocumentSourceModule.Design,
                nameof(BasicDesignDoc), "file", document.Id, document.FilePath!, userId, ct);
        var shopDrawings = await db.ShopDrawings
            .Where(document => designProjectIds.Contains(document.DesignProjectId) && document.FilePath != null)
            .ToListAsync(ct);
        foreach (var document in shopDrawings)
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                projectByDesign[document.DesignProjectId], ProjectDocumentSourceModule.Design,
                nameof(ShopDrawing), "file", document.Id, document.FilePath!, userId, ct);
        var permits = await db.PermitChecklistItems
            .Where(document => designProjectIds.Contains(document.DesignProjectId) &&
                (document.SubmittedFilePath != null || document.IssuedFilePath != null))
            .ToListAsync(ct);
        foreach (var document in permits)
        {
            var projectId = projectByDesign[document.DesignProjectId];
            if (!string.IsNullOrWhiteSpace(document.SubmittedFilePath))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    projectId, ProjectDocumentSourceModule.Design, nameof(PermitChecklistItem),
                    "submittedPackage", document.Id, document.SubmittedFilePath, userId, ct);
            if (!string.IsNullOrWhiteSpace(document.IssuedFilePath))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    projectId, ProjectDocumentSourceModule.Design, nameof(PermitChecklistItem),
                    "issuedPermit", document.Id, document.IssuedFilePath, userId, ct);
        }
        var acceptances = await db.AcceptanceRecords
            .Where(record => designProjectIds.Contains(record.DesignProjectId) && record.Documents != null)
            .ToListAsync(ct);
        foreach (var record in acceptances)
            foreach (var path in DeserializeManagedPaths(record.Documents, "/files/business-documents/acceptance/"))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    projectByDesign[record.DesignProjectId], ProjectDocumentSourceModule.Acceptance,
                    nameof(AcceptanceRecord), "documents", record.Id, path, userId, ct);
        var asBuiltDocuments = await db.AsBuiltDocuments
            .Where(document => designProjectIds.Contains(document.DesignProjectId) && document.FileUrl != null)
            .ToListAsync(ct);
        foreach (var document in asBuiltDocuments)
            if (document.FileUrl!.StartsWith("/files/business-documents/as-built/", StringComparison.OrdinalIgnoreCase))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    projectByDesign[document.DesignProjectId], ProjectDocumentSourceModule.Acceptance,
                    nameof(AsBuiltDocument), "file", document.Id, document.FileUrl, userId, ct);
        var handovers = await db.HandoverRecords
            .Where(record => designProjectIds.Contains(record.DesignProjectId) && record.Documents != null)
            .ToListAsync(ct);
        foreach (var record in handovers)
            foreach (var path in DeserializeManagedPaths(record.Documents, "/files/business-documents/handover/"))
                await projectDocuments.StageExistingManagedFileDeleteAsync(
                    projectByDesign[record.DesignProjectId], ProjectDocumentSourceModule.Handover,
                    nameof(HandoverRecord), "documents", record.Id, path, userId, ct);
    }

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

    private static async Task RemoveTranslationsAsync(
        AppDbContext db,
        string entityType,
        IReadOnlyCollection<int> entityIds,
        CancellationToken ct)
    {
        if (entityIds.Count == 0) return;
        var translations = await db.EntityTranslations
            .Where(translation => translation.EntityType == entityType
                && entityIds.Contains(translation.EntityId))
            .ToListAsync(ct);
        db.EntityTranslations.RemoveRange(translations);
    }
}