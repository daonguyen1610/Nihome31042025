using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ConstructionOperationException(string message) : Exception(message);

public interface IConstructionOperationsService
{
    Task<ConstructionPreparationResponse?> GetPreparationAsync(int projectId, CancellationToken ct);
    Task<ConstructionPreparationResponse> UpsertPreparationAsync(int projectId, UpsertConstructionPreparationRequest request, int userId, CancellationToken ct);
    Task<ConstructionPreparationResponse> EvaluateReadinessAsync(int projectId, string rowVersion, int userId, CancellationToken ct);
    Task<ConstructionPreparationResponse> StartAsync(int projectId, string rowVersion, int userId, CancellationToken ct);
    Task<List<ConstructionResourceRequestResponse>> ListResourcesAsync(int projectId, CancellationToken ct);
    Task<ConstructionResourceReferenceDataResponse> GetResourceReferencesAsync(int projectId, CancellationToken ct);
    Task<ConstructionResourceRequestResponse?> GetResourceAsync(int projectId, int id, CancellationToken ct);
    Task<ConstructionResourceRequestResponse> CreateResourceAsync(int projectId, UpsertConstructionResourceRequest request, int userId, CancellationToken ct);
    Task<ConstructionResourceRequestResponse?> UpdateResourceAsync(int projectId, int id, UpsertConstructionResourceRequest request, int userId, CancellationToken ct);
    Task<ConstructionResourceRequestResponse?> SubmitResourceAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct);
    Task<ConstructionResourceRequestResponse?> DecideResourceAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct);
    Task<List<QaQcTemplateResponse>> ListTemplatesAsync(CancellationToken ct);
    Task<QaQcTemplateResponse> CreateTemplateAsync(CreateQaQcTemplateRequest request, int userId, CancellationToken ct);
    Task<QaQcTemplateResponse?> PublishTemplateAsync(int id, string rowVersion, CancellationToken ct);
    Task<List<QaQcDossierResponse>> ListDossiersAsync(int projectId, CancellationToken ct);
    Task<QaQcDossierResponse?> GetDossierAsync(int projectId, int id, CancellationToken ct);
    Task<QaQcDossierResponse> CreateDossierAsync(int projectId, CreateQaQcDossierRequest request, int userId, CancellationToken ct);
    Task<QaQcDossierResponse?> UpdateDossierAsync(int projectId, int id, CreateQaQcDossierRequest request, string rowVersion, int userId, CancellationToken ct);
    Task<QaQcDossierResponse?> SubmitDossierAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct);
    Task<QaQcDossierResponse?> DecideDossierAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct);
    Task<List<ConstructionQsBatchResponse>> ListQsAsync(int projectId, CancellationToken ct);
    Task<ConstructionQsReferenceDataResponse> GetQsReferencesAsync(int projectId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> GetQsAsync(int projectId, int id, CancellationToken ct);
    Task<ConstructionQsBatchResponse> CreateQsAsync(int projectId, CreateConstructionQsBatchRequest request, int userId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> UpdateQsAsync(int projectId, int id, CreateConstructionQsBatchRequest request, string rowVersion, int userId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> SubmitQsAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> DecideQsAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct);
}

public sealed class ConstructionOperationsService(AppDbContext db, IProcurementService procurement) : IConstructionOperationsService
{
    public async Task<ConstructionPreparationResponse?> GetPreparationAsync(int projectId, CancellationToken ct)
    {
        var item = await db.ConstructionPreparations.AsNoTracking().Where(x => x.OperationalProjectId == projectId)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        return item is null ? null : Map(item, await MissingReadinessAsync(projectId, item, ct));
    }

    public async Task<ConstructionPreparationResponse> UpsertPreparationAsync(int projectId, UpsertConstructionPreparationRequest request, int userId, CancellationToken ct)
    {
        EnsureRequiredReferences(request);
        await ValidatePreparationDocumentsAsync(projectId, request, ct);
        var current = await db.ConstructionPreparations.Where(x => x.OperationalProjectId == projectId)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (current is { Status: not ConstructionPreparationStatus.Draft })
        {
            if (current.Status == ConstructionPreparationStatus.InConstruction)
                throw new ConstructionOperationException("An in-construction baseline requires the approved VO/change workflow before a new preparation version can be created.");
            CrmConcurrency.Apply(db, current, request.RowVersion);
            var previous = current;
            previous.Status = ConstructionPreparationStatus.Superseded;
            previous.UpdatedAt = DateTime.UtcNow;
            previous.UpdatedByUserId = userId;
            current = new ConstructionPreparation { OperationalProjectId = projectId, Version = previous.Version + 1, SupersedesPreparationId = previous.Id, CreatedByUserId = userId };
            db.ConstructionPreparations.Add(current);
        }
        else if (current is null)
        {
            current = new ConstructionPreparation { OperationalProjectId = projectId, CreatedByUserId = userId };
            db.ConstructionPreparations.Add(current);
        }
        else CrmConcurrency.Apply(db, current, request.RowVersion);
        Apply(current, request, userId);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return Map(current)!;
    }

    public async Task<ConstructionPreparationResponse> EvaluateReadinessAsync(int projectId, string rowVersion, int userId, CancellationToken ct)
    {
        var item = await LatestPreparation(projectId, ct);
        if (item.Status == ConstructionPreparationStatus.InConstruction) return Map(item)!;
        CrmConcurrency.Apply(db, item, rowVersion);
        var missing = await MissingReadinessAsync(projectId, item, ct);
        item.BudgetBaselineLocked = !missing.Contains("locked budget baseline");
        item.SiteHandoverComplete = !item.SiteHandoverRequired || !missing.Contains("site handover evidence");
        item.MethodStatementApproved = !item.MethodStatementRequired || !missing.Contains("approved method statement evidence");
        item.HsePlanApproved = !item.HsePlanRequired || !missing.Contains("approved HSE plan evidence");
        item.ReadinessSnapshotJson = await BuildReadinessSnapshot(projectId, item, missing, userId, ct);
        item.Status = missing.Count == 0 ? ConstructionPreparationStatus.ReadyToStart : ConstructionPreparationStatus.Published;
        item.PublishedAt ??= DateTime.UtcNow;
        item.PublishedByUserId ??= userId;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedByUserId = userId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return Map(item, missing)!;
    }

    public async Task<ConstructionPreparationResponse> StartAsync(int projectId, string rowVersion, int userId, CancellationToken ct)
    {
        var item = await LatestPreparation(projectId, ct);
        if (item.Status == ConstructionPreparationStatus.InConstruction) return Map(item)!;
        if (item.Status != ConstructionPreparationStatus.ReadyToStart) throw new ConstructionOperationException("Readiness must be evaluated successfully before construction can start.");
        CrmConcurrency.Apply(db, item, rowVersion);
        var missing = await MissingReadinessAsync(projectId, item, ct);
        if (missing.Count != 0) throw new ConstructionOperationException($"Construction cannot start. Missing: {string.Join(", ", missing)}.");
        item.Status = ConstructionPreparationStatus.InConstruction;
        item.StartedAt = DateTime.UtcNow;
        item.StartedByUserId = userId;
        item.ReadinessSnapshotJson = await BuildReadinessSnapshot(projectId, item, missing, userId, ct);
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedByUserId = userId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return Map(item, missing)!;
    }

    public async Task<List<ConstructionResourceRequestResponse>> ListResourcesAsync(int projectId, CancellationToken ct) =>
        (await db.ConstructionResourceRequests.AsNoTracking().Where(x => x.OperationalProjectId == projectId).OrderByDescending(x => x.Id).ToListAsync(ct)).Select(Map).ToList();
    public async Task<ConstructionResourceReferenceDataResponse> GetResourceReferencesAsync(int projectId, CancellationToken ct)
    {
        var revisionId = await db.ProjectBoqRevisions.AsNoTracking()
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(x => x.FinalSelectedAt).ThenByDescending(x => x.ApprovedAt)
            .ThenByDescending(x => x.RevisionNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        var boqLines = new List<ConstructionResourceBoqLineReferenceResponse>();
        if (revisionId.HasValue)
        {
            var lines = await db.ProjectBoqLines.AsNoTracking()
                .Where(x => x.ProjectBoqRevisionId == revisionId.Value)
                .OrderBy(x => x.SortOrder)
                .Select(x => new { x.Id, x.ItemCode, x.Description, x.Unit, x.ApprovedQuantity })
                .ToListAsync(ct);
            var lineIds = lines.Select(x => x.Id).ToList();
            var committed = await db.MaterialRequestLines.AsNoTracking()
                .Where(x => lineIds.Contains(x.ProjectBoqLineId) &&
                    (x.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                     x.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled ||
                     x.MaterialRequest.Status == MaterialRequestStatus.Fulfilled))
                .GroupBy(x => x.ProjectBoqLineId)
                .Select(group => new { ProjectBoqLineId = group.Key, Quantity = group.Sum(x => x.RequestedQuantity) })
                .ToDictionaryAsync(x => x.ProjectBoqLineId, x => x.Quantity, ct);
            boqLines.AddRange(lines.Select(x => new ConstructionResourceBoqLineReferenceResponse(
                x.Id, x.ItemCode, x.Description, x.Unit, x.ApprovedQuantity,
                Math.Max(x.ApprovedQuantity - committed.GetValueOrDefault(x.Id), 0m))));
        }

        var projectManagerId = await db.OperationalProjects.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => x.ProjectManagerUserId)
            .SingleAsync(ct);
        var memberIds = await db.OperationalProjectMembers.AsNoTracking()
            .Where(x => x.OperationalProjectId == projectId && x.EndedAt == null)
            .Select(x => x.UserId)
            .ToListAsync(ct);
        if (projectManagerId.HasValue) memberIds.Add(projectManagerId.Value);
        memberIds = memberIds.Distinct().ToList();

        var users = await db.Users.AsNoTracking()
            .Where(x => memberIds.Contains(x.Id) && x.IsActive)
            .Select(x => new
            {
                x.Id,
                Name = x.FullName ?? x.Email,
                IsProcurement = x.RoleEntity != null && x.RoleEntity.Code == "PROCUREMENT",
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var siteUsers = users.Select(x => new ConstructionResourceUserReferenceResponse(x.Id, x.Name)).ToList();
        var procurementUsers = users.Where(x => x.IsProcurement)
            .Select(x => new ConstructionResourceUserReferenceResponse(x.Id, x.Name)).ToList();
        return new ConstructionResourceReferenceDataResponse(boqLines, siteUsers, procurementUsers);
    }
    public async Task<ConstructionResourceRequestResponse?> GetResourceAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await db.ConstructionResourceRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<ConstructionResourceRequestResponse> CreateResourceAsync(int projectId, UpsertConstructionResourceRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ValidateResource(projectId, request, ct);
        var next = await db.ConstructionResourceRequests.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new ConstructionResourceRequest { OperationalProjectId = projectId, Code = $"RR-{projectId:D4}-{next:D4}", CreatedByUserId = userId };
        Apply(item, request); db.ConstructionResourceRequests.Add(item);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    public async Task<ConstructionResourceRequestResponse?> UpdateResourceAsync(int projectId, int id, UpsertConstructionResourceRequest request, int userId, CancellationToken ct)
    {
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        if (item.Status != ConstructionReviewStatus.Draft || item.CreatedByUserId != userId) throw new ConstructionOperationException("Only the BCH author can edit a draft resource request.");
        await ValidateResource(projectId, request, ct); CrmConcurrency.Apply(db, item, request.RowVersion); Apply(item, request);
        await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }

    public async Task<ConstructionResourceRequestResponse?> SubmitResourceAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct)
    {
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        if (item.Status == ConstructionReviewStatus.PendingCmReview) return Map(item);
        if (item.Status != ConstructionReviewStatus.Draft || item.CreatedByUserId != userId) throw new ConstructionOperationException("Only the BCH author can submit a draft resource request.");
        CrmConcurrency.Apply(db, item, rowVersion); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; item.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }

    public async Task<ConstructionResourceRequestResponse?> DecideResourceAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDecision(item.Status, item.CreatedByUserId, userId, request.Reason); CrmConcurrency.Apply(db, item, request.RowVersion);
        item.Status = request.Approved ? ConstructionReviewStatus.CmApproved : ConstructionReviewStatus.CmRejected;
        item.CmReviewedAt = DateTime.UtcNow; item.CmReviewedByUserId = userId; item.CmDecisionReason = request.Reason.Trim(); item.UpdatedAt = DateTime.UtcNow;
        if (request.Approved) await CreateResourceHandoff(item, userId, ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    public async Task<List<QaQcTemplateResponse>> ListTemplatesAsync(CancellationToken ct) => (await db.QaQcTemplates.AsNoTracking().OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToListAsync(ct)).Select(Map).ToList();
    public async Task<QaQcTemplateResponse> CreateTemplateAsync(CreateQaQcTemplateRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        EnsureText(request.Code, "Template code"); EnsureText(request.NameVi, "Vietnamese template name"); EnsureText(request.Name, "English template name"); EnsureText(request.NameZh, "Chinese template name"); EnsureText(request.NameJa, "Japanese template name");
        ValidateJson(request.SchemaJson, "Template schema", JsonValueKind.Object);
        var code = request.Code.Trim().ToUpperInvariant();
        var version = (await db.QaQcTemplates.Where(x => x.Code == code).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var item = new QaQcTemplate { Code = code, Version = version, NameVi = request.NameVi.Trim(), Name = request.Name.Trim(), NameZh = request.NameZh.Trim(), NameJa = request.NameJa.Trim(), SchemaJson = request.SchemaJson, CreatedByUserId = userId };
        db.QaQcTemplates.Add(item); await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }
    public async Task<QaQcTemplateResponse?> PublishTemplateAsync(int id, string rowVersion, CancellationToken ct)
    {
        var item = await db.QaQcTemplates.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return null;
        CrmConcurrency.Apply(db, item, rowVersion); item.IsPublished = true; item.PublishedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }

    public async Task<List<QaQcDossierResponse>> ListDossiersAsync(int projectId, CancellationToken ct) => (await Dossiers().AsNoTracking().Where(x => x.OperationalProjectId == projectId).OrderByDescending(x => x.Id).ToListAsync(ct)).Select(x => Map(x)!).ToList();
    public async Task<QaQcDossierResponse?> GetDossierAsync(int projectId, int id, CancellationToken ct) => Map(await Dossiers().AsNoTracking().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct));
    public async Task<QaQcDossierResponse> CreateDossierAsync(int projectId, CreateQaQcDossierRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var template = await ValidateDossier(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList() : request.EvidenceDocumentIds, userId, ct);
        var next = await db.QaQcDossiers.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new QaQcDossier { OperationalProjectId = projectId, Code = $"QA-{projectId:D4}-{next:D4}", CreatedByUserId = userId };
        Apply(item, request, template); db.QaQcDossiers.Add(item); await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item)!;
    }
    public async Task<QaQcDossierResponse?> UpdateDossierAsync(int projectId, int id, CreateQaQcDossierRequest request, string rowVersion, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var item = await Dossiers().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId); var template = await ValidateDossier(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList() : request.EvidenceDocumentIds, userId, ct);
        CrmConcurrency.Apply(db, item, rowVersion); Apply(item, request, template);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }
    public Task<QaQcDossierResponse?> SubmitDossierAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct) => TransitionDossier(projectId, id, rowVersion, userId, null, ct);
    public Task<QaQcDossierResponse?> DecideDossierAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct) => TransitionDossier(projectId, id, request.RowVersion!, userId, request, ct);

    public async Task<List<ConstructionQsBatchResponse>> ListQsAsync(int projectId, CancellationToken ct) => (await Qs().AsNoTracking().Where(x => x.OperationalProjectId == projectId).OrderByDescending(x => x.Id).ToListAsync(ct)).Select(x => Map(x)!).ToList();
    public async Task<ConstructionQsReferenceDataResponse> GetQsReferencesAsync(int projectId, CancellationToken ct)
    {
        var contracts = await db.Contracts.AsNoTracking()
            .Where(x => x.OperationalProjectId == projectId &&
                x.Status != ContractStatus.Draft && x.Status != ContractStatus.Cancelled)
            .OrderBy(x => x.ContractNumber)
            .Select(x => new ConstructionQsContractReferenceResponse(
                x.Id, x.ContractNumber, x.Direction, x.Status,
                db.ContractPaymentMilestones.Where(m => m.ContractId == x.Id).OrderBy(m => m.Order)
                    .Select(m => new ConstructionQsMilestoneReferenceResponse(
                        m.Id, m.Order, m.Name, x.Value * m.PercentValue / 100m, m.Status)).ToList(),
                db.ContractAppendices.Where(a => a.ContractId == x.Id && a.Status == ContractAppendixStatus.Approved)
                    .OrderBy(a => a.VoNumber)
                    .Select(a => new ConstructionQsAppendixReferenceResponse(a.Id, a.VoNumber, a.Title)).ToList()))
            .ToListAsync(ct);
        var revisions = await db.ProjectBoqRevisions.AsNoTracking()
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(x => x.RevisionNumber)
            .Select(x => new ConstructionQsBoqReferenceResponse(x.Id, x.RevisionNumber, x.Currency))
            .ToListAsync(ct);
        return new ConstructionQsReferenceDataResponse(contracts, revisions);
    }
    public async Task<ConstructionQsBatchResponse?> GetQsAsync(int projectId, int id, CancellationToken ct) => Map(await Qs().AsNoTracking().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct));
    public async Task<ConstructionQsBatchResponse> CreateQsAsync(int projectId, CreateConstructionQsBatchRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ValidateQs(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.EvidenceDocumentIds, userId, ct);
        var next = await db.ConstructionQsBatches.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new ConstructionQsBatch { OperationalProjectId = projectId, Code = $"QS-{projectId:D4}-{next:D4}", CreatedByUserId = userId }; Apply(item, request);
        db.ConstructionQsBatches.Add(item); await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item)!;
    }
    public async Task<ConstructionQsBatchResponse?> UpdateQsAsync(int projectId, int id, CreateConstructionQsBatchRequest request, string rowVersion, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var item = await Qs().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId); await ValidateQs(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.EvidenceDocumentIds, userId, ct);
        CrmConcurrency.Apply(db, item, rowVersion); Apply(item, request);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }
    public Task<ConstructionQsBatchResponse?> SubmitQsAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct) => TransitionQs(projectId, id, rowVersion, userId, null, ct);
    public Task<ConstructionQsBatchResponse?> DecideQsAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct) => TransitionQs(projectId, id, request.RowVersion!, userId, request, ct);

    private async Task<QaQcDossierResponse?> TransitionDossier(int projectId, int id, string rowVersion, int userId, CmDecisionRequest? decision, CancellationToken ct)
    {
        var item = await Dossiers().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        CrmConcurrency.Apply(db, item, rowVersion);
        if (decision is null) { EnsureDraftOwner(item.Status, item.CreatedByUserId, userId); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; }
        else { EnsureDecision(item.Status, item.CreatedByUserId, userId, decision.Reason); item.Status = decision.Approved ? ConstructionReviewStatus.CmApproved : ConstructionReviewStatus.CmRejected; item.CmReviewedAt = DateTime.UtcNow; item.CmReviewedByUserId = userId; item.CmDecisionReason = decision.Reason.Trim(); }
        item.UpdatedAt = DateTime.UtcNow; await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }
    private async Task<ConstructionQsBatchResponse?> TransitionQs(int projectId, int id, string rowVersion, int userId, CmDecisionRequest? decision, CancellationToken ct)
    {
        await using var tx = decision is not null && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var item = await Qs().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        CrmConcurrency.Apply(db, item, rowVersion);
        if (decision is null) { EnsureDraftOwner(item.Status, item.CreatedByUserId, userId); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; }
        else
        {
            EnsureDecision(item.Status, item.CreatedByUserId, userId, decision.Reason);
            var predecessorId = item.SupersedesBatchId ?? item.ReversesBatchId;
            if (decision.Approved && predecessorId is int approvedPredecessorId)
            {
                var predecessor = await db.ConstructionQsBatches.SingleAsync(x => x.Id == approvedPredecessorId, ct);
                if (predecessor.RecordState != ConstructionQsRecordState.Active)
                    throw new ConstructionOperationException("The referenced QS batch already has an approved correction or reversal.");
                predecessor.RecordState = item.AdjustmentKind == ConstructionQsAdjustmentKind.Reversal
                    ? ConstructionQsRecordState.Reversed
                    : ConstructionQsRecordState.Superseded;
                predecessor.UpdatedAt = DateTime.UtcNow;
            }
            item.Status = decision.Approved ? ConstructionReviewStatus.CmApproved : ConstructionReviewStatus.CmRejected;
            item.IsActiveLineage = decision.Approved;
            item.CmReviewedAt = DateTime.UtcNow; item.CmReviewedByUserId = userId; item.CmDecisionReason = decision.Reason.Trim();
        }
        item.UpdatedAt = DateTime.UtcNow; await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    private async Task CreateResourceHandoff(ConstructionResourceRequest item, int cmUserId, CancellationToken ct)
    {
        if (item.HandoffStatus != ConstructionHandoffStatus.NotReady) return;
        if (item.Type != ConstructionResourceType.Material) { item.HandoffEntityType = "ConstructionResourceRequest"; item.HandoffEntityId = item.Id; item.HandoffStatus = ConstructionHandoffStatus.ReadyForProcurementAndFinance; return; }
        if (item.ProjectBoqLineId is null || item.ResponsibleSiteUserId is null || item.AssignedProcurementUserId is null)
            throw new ConstructionOperationException("Material requests require BOQ line, responsible site user, and assigned procurement user before CM approval.");
        var created = await procurement.CreateMaterialRequestAsync(item.OperationalProjectId, new MaterialRequestUpsertRequest
        {
            ResponsibleSiteUserId = item.ResponsibleSiteUserId.Value,
            AssignedProcurementUserId = item.AssignedProcurementUserId.Value,
            RequiredAt = item.NeedByDate,
            Note = $"CM-approved resource request {item.Code}: {item.Reason}",
            Lines = [new MaterialRequestLineRequest { ProjectBoqLineId = item.ProjectBoqLineId.Value, RequestedQuantity = item.Quantity }],
        }, item.CreatedByUserId, ct);
        await procurement.SubmitMaterialRequestAsync(item.OperationalProjectId, created.Id,
            new ProcurementTransitionRequest { RowVersion = created.RowVersion, Reason = $"CM approval by user #{cmUserId}." },
            item.CreatedByUserId, ct);
        item.HandoffEntityType = "MaterialRequest"; item.HandoffEntityId = created.Id; item.HandoffStatus = ConstructionHandoffStatus.MaterialRequestCreated;
    }

    private async Task ValidateResource(int projectId, UpsertConstructionResourceRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Type)) throw new ConstructionOperationException("Resource type must be material, equipment, or labour.");
        EnsureText(request.WbsCode, "WBS code"); EnsureText(request.Unit, "Unit"); EnsureText(request.Reason, "Reason");
        if (request.NeedByDate!.Value.Date < DateTime.UtcNow.Date) throw new ConstructionOperationException("Need-by date cannot be in the past.");
        if (request.Type == ConstructionResourceType.Material && request.ProjectBoqLineId is null) throw new ConstructionOperationException("Material resource requests require an approved Execution BOQ line.");
        if (request.Type == ConstructionResourceType.Material &&
            (request.ResponsibleSiteUserId is null || request.AssignedProcurementUserId is null))
            throw new ConstructionOperationException("Material resource requests require a responsible site user and assigned procurement user.");
        if (request.ProjectBoqLineId is not null && !await db.ProjectBoqLines.AnyAsync(x => x.Id == request.ProjectBoqLineId && x.ProjectBoqRevision.OperationalProjectId == projectId && x.ProjectBoqRevision.Status == ProjectBoqRevisionStatus.Approved, ct)) throw new ConstructionOperationException("BOQ line must belong to an approved project BOQ revision.");
        if (request.ResponsibleSiteUserId is int siteUserId &&
            !await IsActiveProjectUserAsync(projectId, siteUserId, ct))
            throw new ConstructionOperationException("Responsible site user must be an active project member.");
        if (request.AssignedProcurementUserId is int procurementUserId)
        {
            if (!await IsActiveProjectUserAsync(projectId, procurementUserId, ct))
                throw new ConstructionOperationException("Assigned procurement user must be an active project member.");
            var hasProcurementRole = await db.Users.AsNoTracking().AnyAsync(x => x.Id == procurementUserId && x.IsActive &&
                x.RoleEntity != null && x.RoleEntity.Code == "PROCUREMENT", ct);
            if (!hasProcurementRole)
                throw new ConstructionOperationException("Assigned procurement user must have the PROCUREMENT role.");
        }
    }

    private Task<bool> IsActiveProjectUserAsync(int projectId, int userId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            (db.OperationalProjects.Any(project => project.Id == projectId && project.ProjectManagerUserId == userId) ||
             db.OperationalProjectMembers.Any(member => member.OperationalProjectId == projectId &&
                 member.UserId == userId && member.EndedAt == null)), ct);

    private async Task PinEvidenceAsync(int projectId, IReadOnlyCollection<long> documentIds, int actor, CancellationToken ct)
    {
        var documents = await db.ProjectDocuments.Where(item => documentIds.Contains(item.Id) &&
            item.OperationalProjectId == projectId && item.DeletedAt == null &&
            item.DesiredOperation != ProjectDocumentDesiredOperation.Delete &&
            item.SyncStatus != ProjectDocumentSyncStatus.Deleted).ToListAsync(ct);
        if (documents.Count != documentIds.Count)
            throw new ConstructionOperationException("Every evidence document must remain active in this project.");
        foreach (var document in documents)
        {
            // Fence deletes that passed their reference check before this link
            // was committed, including source-managed file removal.
            document.UpdatedAt = DateTime.UtcNow;
            document.UpdatedByUserId = actor;
            db.Entry(document).Property(item => item.UpdatedAt).IsModified = true;
        }
    }

    private async Task<QaQcTemplate> ValidateDossier(int projectId, CreateQaQcDossierRequest request, CancellationToken ct)
    {
        EnsureText(request.WbsCode, "WBS code"); EnsureText(request.Title, "Dossier title");
        ValidateJson(request.ChecklistResultsJson, "Checklist results", JsonValueKind.Array);
        var template = await db.QaQcTemplates.SingleOrDefaultAsync(x => x.Id == request.TemplateId && x.IsPublished, ct) ?? throw new ConstructionOperationException("A published QA/QC template is required.");
        if (request.IfcReleaseId is null) throw new ConstructionOperationException("A released IFC packet is required for every QA/QC dossier.");
        if (!await db.IfcReleases.AnyAsync(x => x.Id == request.IfcReleaseId && x.Status == IfcReleaseStatus.Released && x.DesignProject.OperationalProjectId == projectId, ct)) throw new ConstructionOperationException("QA/QC dossiers may reference only a released IFC packet for the same project.");
        if (request.Evidence is null || request.EvidenceDocumentIds is null || request.Evidence.Any(x => x is null))
            throw new ConstructionOperationException("QA/QC evidence lists and items cannot be null.");
        if (request.Evidence.Count > 0 && request.EvidenceDocumentIds.Count > 0)
            throw new ConstructionOperationException("Provide either typed QA/QC evidence or legacy evidence document IDs, not both.");
        if (request.Evidence.Any(x => x.ProjectDocumentId <= 0 || !Enum.IsDefined(x.EvidenceType)))
            throw new ConstructionOperationException("Every QA/QC evidence item requires a positive document ID and a valid evidence type.");
        var evidenceIds = request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList()
            : request.EvidenceDocumentIds;
        if (evidenceIds.Count != evidenceIds.Distinct().Count() ||
            await db.ProjectDocuments.CountAsync(x => evidenceIds.Contains(x.Id) &&
                x.OperationalProjectId == projectId && x.DeletedAt == null, ct) != evidenceIds.Count)
            throw new ConstructionOperationException("Every evidence document must be an active staged document in the same project and appear only once.");
        return template;
    }

    private async Task ValidateQs(int projectId, CreateConstructionQsBatchRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Direction)) throw new ConstructionOperationException("QS direction must be Upstream or Downstream.");
        EnsureText(request.WbsCode, "WBS code"); EnsureText(request.Description, "QS description");
        if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.ItemCode) || string.IsNullOrWhiteSpace(x.Description) || string.IsNullOrWhiteSpace(x.Unit))) throw new ConstructionOperationException("Every QS line requires an item code, description, and unit.");
        if (request.PeriodFrom > request.PeriodTo) throw new ConstructionOperationException("QS period start must not be after its end.");
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ContractId && x.OperationalProjectId == projectId, ct) ?? throw new ConstructionOperationException("Contract must belong to the operational project.");
        if (contract.Status is ContractStatus.Draft or ContractStatus.Cancelled)
            throw new ConstructionOperationException("QS batches require a signed or active contract.");
        if ((request.Direction == ConstructionQsDirection.Upstream) != (contract.Direction == ContractDirection.Upstream)) throw new ConstructionOperationException("QS direction must match the linked contract direction.");
        if (request.ContractPaymentMilestoneId is not null && !await db.ContractPaymentMilestones.AnyAsync(x => x.Id == request.ContractPaymentMilestoneId && x.ContractId == request.ContractId, ct)) throw new ConstructionOperationException("Payment milestone must belong to the linked contract.");
        if (request.ContractAppendixId is not null && !await db.ContractAppendices.AnyAsync(x =>
                x.Id == request.ContractAppendixId && x.ContractId == request.ContractId &&
                x.Status == ContractAppendixStatus.Approved, ct))
            throw new ConstructionOperationException("VO/appendix must be approved and belong to the linked contract.");
        if (request.ProjectBoqRevisionId is not null && !await db.ProjectBoqRevisions.AnyAsync(x => x.Id == request.ProjectBoqRevisionId && x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved, ct)) throw new ConstructionOperationException("BOQ revision must be approved and belong to the project.");
        if (request.Direction == ConstructionQsDirection.Upstream && request.ContractPaymentMilestoneId is null) throw new ConstructionOperationException("Upstream QS requires a receivable payment milestone.");
        if (request.SupersedesBatchId is not null && request.ReversesBatchId is not null) throw new ConstructionOperationException("A QS correction may either supersede or reverse a prior batch, but not both.");
        var correctionId = request.SupersedesBatchId ?? request.ReversesBatchId;
        ConstructionQsBatch? predecessor = null;
        if (correctionId is not null)
        {
            predecessor = await db.ConstructionQsBatches.Include(x => x.Lines).SingleOrDefaultAsync(x =>
                x.Id == correctionId && x.OperationalProjectId == projectId && x.Direction == request.Direction &&
                x.Status == ConstructionReviewStatus.CmApproved && x.RecordState == ConstructionQsRecordState.Active, ct);
            if (predecessor is null) throw new ConstructionOperationException("A QS correction must reference an active approved batch in the same project and direction.");
            if (predecessor.ContractId != request.ContractId || predecessor.WbsCode != request.WbsCode.Trim())
                throw new ConstructionOperationException("A QS correction or reversal must retain the predecessor contract and WBS.");
            if (await db.ConstructionQsBatches.AnyAsync(x =>
                    (x.SupersedesBatchId == correctionId || x.ReversesBatchId == correctionId) && x.IsActiveLineage, ct))
                throw new ConstructionOperationException("The referenced QS batch already has an active successor.");
        }
        if (request.EvidenceDocumentIds.Count != request.EvidenceDocumentIds.Distinct().Count() || await db.ProjectDocuments.CountAsync(x => request.EvidenceDocumentIds.Contains(x.Id) && x.OperationalProjectId == projectId && x.DeletedAt == null, ct) != request.EvidenceDocumentIds.Count) throw new ConstructionOperationException("Every QS evidence document must be active and belong to the project.");
        var computed = request.Lines.Sum(x => x.Quantity * x.UnitPrice); if (computed != request.CertifiedAmount) throw new ConstructionOperationException("Certified amount must equal the sum of QS quantity lines.");
        if (request.ReversesBatchId is not null && predecessor is not null)
        {
            var requestedLines = request.Lines.OrderBy(x => x.ItemCode).ToList();
            var predecessorLines = predecessor.Lines.OrderBy(x => x.ItemCode).ToList();
            if (request.CertifiedAmount != predecessor.CertifiedAmount || requestedLines.Count != predecessorLines.Count ||
                requestedLines.Where((line, index) => line.ItemCode.Trim() != predecessorLines[index].ItemCode ||
                    line.Quantity != predecessorLines[index].Quantity || line.UnitPrice != predecessorLines[index].UnitPrice).Any())
                throw new ConstructionOperationException("A QS reversal must exactly mirror the predecessor amount and quantity lines.");
        }
    }

    private async Task<List<string>> MissingReadinessAsync(int projectId, ConstructionPreparation item, CancellationToken ct)
    {
        var missing = new List<string>();
        var evidenceIds = PreparationDocumentIds(item).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var activeEvidenceIds = await ActiveConstructionDocumentIdsAsync(projectId, evidenceIds, ct);
        if (!HasEvidence(item.WbsBaselineDocumentId, activeEvidenceIds)) missing.Add("published WBS baseline evidence");
        if (!HasEvidence(item.ScheduleBaselineDocumentId, activeEvidenceIds)) missing.Add("published schedule baseline evidence");
        if (!HasEvidence(item.SCurveBaselineDocumentId, activeEvidenceIds)) missing.Add("published S-curve baseline evidence");
        if (!HasEvidence(item.BchOrganizationDocumentId, activeEvidenceIds)) missing.Add("approved BCH organization evidence");
        if (!HasEvidence(item.ResourcePlanDocumentId, activeEvidenceIds)) missing.Add("approved resource plan evidence");
        if (!HasEvidence(item.BudgetBaselineDocumentId, activeEvidenceIds)) missing.Add("locked budget baseline");
        if (!item.SiteHandoverRequired || !item.MethodStatementRequired || !item.HsePlanRequired)
            missing.Add("approved project ISO conditional-requirements profile");
        if (!await db.Contracts.AnyAsync(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream && (x.Type == ContractType.Construction || x.Type == ContractType.DesignAndBuild) && x.Status != ContractStatus.Draft && x.Status != ContractStatus.Cancelled && x.SignedDate != null, ct)) missing.Add("signed upstream construction contract");
        if (!await db.IfcReleases.AnyAsync(x => x.DesignProject.OperationalProjectId == projectId && x.Status == IfcReleaseStatus.Released, ct)) missing.Add("released IFC");
        var permitEvidenceExists = await db.PermitChecklistItems.AnyAsync(x => x.OperationalProjectId == projectId, ct);
        var permitEvidenceInvalid = await db.PermitChecklistItems.AnyAsync(x => x.OperationalProjectId == projectId &&
            (x.Status != PermitStatus.Issued || (x.ExpiresAt != null && x.ExpiresAt < DateTime.UtcNow)), ct);
        if (!permitEvidenceExists || permitEvidenceInvalid) missing.Add("valid required permits");
        if (item.SiteHandoverRequired && !HasEvidence(item.SiteHandoverDocumentId, activeEvidenceIds)) missing.Add("site handover evidence");
        if (item.MethodStatementRequired && !HasEvidence(item.MethodStatementDocumentId, activeEvidenceIds)) missing.Add("approved method statement evidence");
        if (item.HsePlanRequired && !HasEvidence(item.HsePlanDocumentId, activeEvidenceIds)) missing.Add("approved HSE plan evidence");
        return missing;
    }
    private async Task<string> BuildReadinessSnapshot(int projectId, ConstructionPreparation item, IReadOnlyList<string> missing, int userId, CancellationToken ct)
    {
        var contractIds = await db.Contracts.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream && (x.Type == ContractType.Construction || x.Type == ContractType.DesignAndBuild) && x.Status != ContractStatus.Draft && x.Status != ContractStatus.Cancelled && x.SignedDate != null).Select(x => x.Id).ToListAsync(ct);
        var ifcReleaseIds = await db.IfcReleases.AsNoTracking().Where(x => x.DesignProject.OperationalProjectId == projectId && x.Status == IfcReleaseStatus.Released).Select(x => x.Id).ToListAsync(ct);
        var permits = await db.PermitChecklistItems.AsNoTracking().Where(x => x.OperationalProjectId == projectId).Select(x => new { x.Id, x.Status, x.ExpiresAt }).ToListAsync(ct);
        return JsonSerializer.Serialize(new { evaluatedAt = DateTime.UtcNow, evaluatedByUserId = userId, preparationId = item.Id, preparationVersion = item.Version, baselines = new { item.WbsBaselineDocumentId, item.ScheduleBaselineDocumentId, item.SCurveBaselineDocumentId, item.BchOrganizationDocumentId, item.ResourcePlanDocumentId, item.BudgetBaselineDocumentId, item.BudgetBaselineLocked }, conditionalEvidence = new { item.SiteHandoverDocumentId, item.MethodStatementDocumentId, item.HsePlanDocumentId }, contractIds, ifcReleaseIds, permits, missing });
    }
    private async Task ValidatePreparationDocumentsAsync(int projectId, UpsertConstructionPreparationRequest request, CancellationToken ct)
    {
        var ids = PreparationDocumentIds(request).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (ids.Count != ids.Distinct().Count()) throw new ConstructionOperationException("Each preparation condition must reference a distinct project document.");
        var active = await ActiveConstructionDocumentIdsAsync(projectId, ids, ct);
        if (active.Count != ids.Count) throw new ConstructionOperationException("Preparation evidence must be an active Construction Acceptance document in the same operational project.");
    }
    private async Task<HashSet<long>> ActiveConstructionDocumentIdsAsync(int projectId, IReadOnlyCollection<long> ids, CancellationToken ct) =>
        (await db.ProjectDocuments.AsNoTracking().Where(x => ids.Contains(x.Id) && x.OperationalProjectId == projectId &&
            x.Category == ProjectDocumentCategory.ConstructionAcceptance && x.DeletedAt == null &&
            x.DesiredOperation != ProjectDocumentDesiredOperation.Delete && x.SyncStatus != ProjectDocumentSyncStatus.Deleted)
            .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
    private static IEnumerable<long?> PreparationDocumentIds(ConstructionPreparation x) =>
        [x.WbsBaselineDocumentId,
            x.ScheduleBaselineDocumentId,
            x.SCurveBaselineDocumentId,
            x.BchOrganizationDocumentId,
            x.ResourcePlanDocumentId,
            x.BudgetBaselineDocumentId,
            x.SiteHandoverDocumentId,
            x.MethodStatementDocumentId,
            x.HsePlanDocumentId];
    private static IEnumerable<long?> PreparationDocumentIds(UpsertConstructionPreparationRequest x) =>
        [x.WbsBaselineDocumentId,
            x.ScheduleBaselineDocumentId,
            x.SCurveBaselineDocumentId,
            x.BchOrganizationDocumentId,
            x.ResourcePlanDocumentId,
            x.BudgetBaselineDocumentId,
            x.SiteHandoverDocumentId,
            x.MethodStatementDocumentId,
            x.HsePlanDocumentId];
    private static bool HasEvidence(long? id, IReadOnlySet<long> activeIds) => id.HasValue && activeIds.Contains(id.Value);
    private async Task<ConstructionPreparation> LatestPreparation(int projectId, CancellationToken ct) => await db.ConstructionPreparations.Where(x => x.OperationalProjectId == projectId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) ?? throw new ConstructionOperationException("Construction preparation has not been created.");
    private static void EnsureRequiredReferences(UpsertConstructionPreparationRequest x) { if (new[] { x.WbsBaselineReference, x.ScheduleBaselineReference, x.SCurveBaselineReference, x.BchOrganizationReference, x.ResourcePlanReference, x.BudgetBaselineReference }.Any(string.IsNullOrWhiteSpace)) throw new ConstructionOperationException("WBS, schedule, S-curve, BCH organization, resource, and budget baseline references are required."); }
    private static void EnsureDraftOwner(ConstructionReviewStatus status, int creator, int user) { if (status != ConstructionReviewStatus.Draft || creator != user) throw new ConstructionOperationException("Only the author can change or submit a draft."); }
    private static void EnsureDecision(ConstructionReviewStatus status, int creator, int reviewer, string reason) { if (status != ConstructionReviewStatus.PendingCmReview) throw new ConstructionOperationException("Only a pending record can be reviewed."); if (creator == reviewer) throw new ConstructionOperationException("The author cannot perform the independent CM review."); EnsureText(reason, "CM review reason"); }
    private static void EnsureText(string? value, string field) { if (string.IsNullOrWhiteSpace(value)) throw new ConstructionOperationException($"{field} is required and cannot contain only whitespace."); }
    private static void ValidateJson(string value, string field, JsonValueKind expectedKind) { try { using var document = JsonDocument.Parse(value); if (document.RootElement.ValueKind != expectedKind) throw new ConstructionOperationException($"{field} must be a JSON {expectedKind.ToString().ToLowerInvariant()}."); } catch (JsonException) { throw new ConstructionOperationException($"{field} must be valid JSON."); } }
    private static void Apply(ConstructionPreparation x, UpsertConstructionPreparationRequest r, int user) { x.WbsBaselineReference = r.WbsBaselineReference.Trim(); x.ScheduleBaselineReference = r.ScheduleBaselineReference.Trim(); x.SCurveBaselineReference = r.SCurveBaselineReference.Trim(); x.BchOrganizationReference = r.BchOrganizationReference.Trim(); x.ResourcePlanReference = r.ResourcePlanReference.Trim(); x.BudgetBaselineReference = r.BudgetBaselineReference.Trim(); x.WbsBaselineDocumentId = r.WbsBaselineDocumentId; x.ScheduleBaselineDocumentId = r.ScheduleBaselineDocumentId; x.SCurveBaselineDocumentId = r.SCurveBaselineDocumentId; x.BchOrganizationDocumentId = r.BchOrganizationDocumentId; x.ResourcePlanDocumentId = r.ResourcePlanDocumentId; x.BudgetBaselineDocumentId = r.BudgetBaselineDocumentId; x.BudgetBaselineLocked = false; x.SiteHandoverRequired = r.SiteHandoverRequired; x.SiteHandoverComplete = false; x.SiteHandoverDocumentId = r.SiteHandoverDocumentId; x.MethodStatementRequired = r.MethodStatementRequired; x.MethodStatementApproved = false; x.MethodStatementDocumentId = r.MethodStatementDocumentId; x.HsePlanRequired = r.HsePlanRequired; x.HsePlanApproved = false; x.HsePlanDocumentId = r.HsePlanDocumentId; x.UpdatedAt = DateTime.UtcNow; x.UpdatedByUserId = user; }
    private static void Apply(ConstructionResourceRequest x, UpsertConstructionResourceRequest r) { x.Type = r.Type; x.WbsCode = r.WbsCode.Trim(); x.NeedByDate = r.NeedByDate!.Value; x.Quantity = r.Quantity; x.Unit = r.Unit.Trim(); x.Reason = r.Reason.Trim(); x.ProjectBoqLineId = r.ProjectBoqLineId; x.ResponsibleSiteUserId = r.ResponsibleSiteUserId; x.AssignedProcurementUserId = r.AssignedProcurementUserId; x.UpdatedAt = DateTime.UtcNow; }
    private static void Apply(QaQcDossier x, CreateQaQcDossierRequest r, QaQcTemplate t)
    {
        x.QaQcTemplateId = t.Id;
        x.TemplateVersion = t.Version;
        x.TemplateSnapshotJson = t.SchemaJson;
        x.IfcReleaseId = r.IfcReleaseId;
        x.ChecklistResultsJson = r.ChecklistResultsJson;
        x.WbsCode = r.WbsCode.Trim();
        x.Title = r.Title.Trim();
        var requestedTypes = r.Evidence.Count > 0
            ? r.Evidence.ToDictionary(item => item.ProjectDocumentId, item => item.EvidenceType)
            : r.EvidenceDocumentIds.ToDictionary(id => id, id => x.Evidence
                .FirstOrDefault(item => item.ProjectDocumentId == id)?.EvidenceType ?? QaQcEvidenceType.General);
        x.Evidence.RemoveAll(item => !requestedTypes.ContainsKey(item.ProjectDocumentId));
        foreach (var (documentId, evidenceType) in requestedTypes)
        {
            var existing = x.Evidence.FirstOrDefault(item => item.ProjectDocumentId == documentId);
            if (existing is not null) existing.EvidenceType = evidenceType;
            else x.Evidence.Add(new QaQcDossierEvidence
            {
                ProjectDocumentId = documentId,
                EvidenceType = evidenceType,
            });
        }
        x.UpdatedAt = DateTime.UtcNow;
    }
    private static void Apply(ConstructionQsBatch x, CreateConstructionQsBatchRequest r) { x.Direction = r.Direction; x.ContractId = r.ContractId; x.WbsCode = r.WbsCode.Trim(); x.PeriodFrom = r.PeriodFrom!.Value; x.PeriodTo = r.PeriodTo!.Value; x.ProjectBoqRevisionId = r.ProjectBoqRevisionId; x.ContractAppendixId = r.ContractAppendixId; x.ContractPaymentMilestoneId = r.ContractPaymentMilestoneId; x.CertifiedAmount = r.CertifiedAmount; x.Description = r.Description.Trim(); x.SupersedesBatchId = r.SupersedesBatchId; x.ReversesBatchId = r.ReversesBatchId; x.AdjustmentKind = r.SupersedesBatchId is not null ? ConstructionQsAdjustmentKind.SupersedingCorrection : r.ReversesBatchId is not null ? ConstructionQsAdjustmentKind.Reversal : ConstructionQsAdjustmentKind.Original; x.IsActiveLineage = false; x.Lines.Clear(); x.Lines.AddRange(r.Lines.Select((line, i) => new ConstructionQsBatchLine { ItemCode = line.ItemCode.Trim(), Description = line.Description.Trim(), Unit = line.Unit.Trim(), Quantity = line.Quantity, UnitPrice = line.UnitPrice, Amount = line.Quantity * line.UnitPrice, SortOrder = i })); x.Evidence.Clear(); x.Evidence.AddRange(r.EvidenceDocumentIds.Select(id => new ConstructionQsBatchEvidence { ProjectDocumentId = id })); x.UpdatedAt = DateTime.UtcNow; }
    private IQueryable<QaQcDossier> Dossiers() => db.QaQcDossiers.Include(x => x.Evidence);
    private IQueryable<ConstructionQsBatch> Qs() => db.ConstructionQsBatches.Include(x => x.Lines).Include(x => x.Evidence);
    private static ConstructionPreparationResponse? Map(ConstructionPreparation? x, IReadOnlyList<string>? missing = null) => x is null ? null : new(x.Id, x.OperationalProjectId, x.Version, x.Status, x.WbsBaselineReference, x.ScheduleBaselineReference, x.SCurveBaselineReference, x.BchOrganizationReference, x.ResourcePlanReference, x.BudgetBaselineReference, x.WbsBaselineDocumentId, x.ScheduleBaselineDocumentId, x.SCurveBaselineDocumentId, x.BchOrganizationDocumentId, x.ResourcePlanDocumentId, x.BudgetBaselineDocumentId, x.BudgetBaselineLocked, x.SiteHandoverRequired, x.SiteHandoverComplete, x.MethodStatementRequired, x.MethodStatementApproved, x.HsePlanRequired, x.HsePlanApproved, x.SiteHandoverDocumentId, x.MethodStatementDocumentId, x.HsePlanDocumentId, missing ?? [], x.ReadinessSnapshotJson, CrmConcurrency.Encode(x.RowVersion));
    private static ConstructionResourceRequestResponse Map(ConstructionResourceRequest x) => new(x.Id, x.Code, x.Type, x.Status, x.WbsCode, x.NeedByDate, x.Quantity, x.Unit, x.Reason, x.ProjectBoqLineId, x.ResponsibleSiteUserId, x.AssignedProcurementUserId, x.CreatedByUserId, x.CmDecisionReason, x.HandoffEntityType, x.HandoffEntityId, x.HandoffStatus, CrmConcurrency.Encode(x.RowVersion), x.ProcurementContractId, x.SuppliedAt, x.SuppliedByUserId, x.SupplyDocumentId);
    private static QaQcTemplateResponse Map(QaQcTemplate x) => new(x.Id, x.Code, x.Version, x.NameVi, x.Name, x.NameZh, x.NameJa, x.SchemaJson, x.IsPublished, CrmConcurrency.Encode(x.RowVersion));
    private static QaQcDossierResponse? Map(QaQcDossier? x) => x is null ? null : new(x.Id, x.Code, x.QaQcTemplateId, x.TemplateVersion, x.TemplateSnapshotJson, x.IfcReleaseId, x.ChecklistResultsJson, x.WbsCode, x.Title, x.Status, x.CreatedByUserId, x.Evidence.Select(e => e.ProjectDocumentId).ToList(), x.CmDecisionReason, CrmConcurrency.Encode(x.RowVersion), x.Evidence.Select(e => new QaQcEvidenceResponse(e.ProjectDocumentId, e.EvidenceType)).ToList());
    private static ConstructionQsBatchResponse? Map(ConstructionQsBatch? x) => x is null ? null : new(x.Id, x.Code, x.Direction, x.Status, x.ContractId, x.WbsCode, x.PeriodFrom, x.PeriodTo, x.ProjectBoqRevisionId, x.ContractAppendixId, x.ContractPaymentMilestoneId, x.CertifiedAmount, x.Description, x.CreatedByUserId, x.CmDecisionReason, x.SupersedesBatchId, x.ReversesBatchId, x.AdjustmentKind, x.RecordState, x.Lines.OrderBy(l => l.SortOrder).Select(l => new ConstructionQsBatchLineResponse(l.Id, l.ItemCode, l.Description, l.Unit, l.Quantity, l.UnitPrice, l.Amount, l.SortOrder)).ToList(), x.Evidence.Select(e => e.ProjectDocumentId).ToList(), CrmConcurrency.Encode(x.RowVersion));
}
