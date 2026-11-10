using System.Text.Json;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

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
    Task<DeletionImpactResponse?> GetResourceDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct);
    Task<ConstructionResourceRequestResponse> CreateResourceAsync(int projectId, UpsertConstructionResourceRequest request, int userId, CancellationToken ct);
    Task<ConstructionResourceRequestResponse?> UpdateResourceAsync(int projectId, int id, UpsertConstructionResourceRequest request, int userId, CancellationToken ct, bool supervisor = false);
    Task<ConstructionResourceRequestResponse?> SubmitResourceAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false);
    Task<bool> DeleteResourceAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false);
    Task<ConstructionResourceRequestResponse?> DecideResourceAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct);
    Task<List<QaQcTemplateResponse>> ListTemplatesAsync(CancellationToken ct);
    Task<QaQcTemplateResponse> CreateTemplateAsync(CreateQaQcTemplateRequest request, int userId, CancellationToken ct);
    Task<QaQcTemplateResponse?> PublishTemplateAsync(int id, string rowVersion, CancellationToken ct);
    Task<List<QaQcDossierResponse>> ListDossiersAsync(int projectId, CancellationToken ct);
    Task<QaQcDossierResponse?> GetDossierAsync(int projectId, int id, CancellationToken ct);
    Task<DeletionImpactResponse?> GetDossierDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct);
    Task<QaQcDossierResponse> CreateDossierAsync(int projectId, CreateQaQcDossierRequest request, int userId, CancellationToken ct);
    Task<QaQcDossierResponse?> UpdateDossierAsync(int projectId, int id, CreateQaQcDossierRequest request, string rowVersion, int userId, CancellationToken ct, bool supervisor = false);
    Task<QaQcDossierResponse?> SubmitDossierAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false);
    Task<bool> DeleteDossierAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false);
    Task<QaQcDossierResponse?> DecideDossierAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct);
    Task<List<ConstructionQsBatchResponse>> ListQsAsync(int projectId, CancellationToken ct);
    Task<ConstructionQsReferenceDataResponse> GetQsReferencesAsync(int projectId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> GetQsAsync(int projectId, int id, CancellationToken ct);
    Task<DeletionImpactResponse?> GetQsDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct);
    Task<ConstructionQsBatchResponse> CreateQsAsync(int projectId, CreateConstructionQsBatchRequest request, int userId, CancellationToken ct);
    Task<ConstructionQsBatchResponse?> UpdateQsAsync(int projectId, int id, CreateConstructionQsBatchRequest request, string rowVersion, int userId, CancellationToken ct, bool supervisor = false);
    Task<ConstructionQsBatchResponse?> SubmitQsAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false);
    Task<bool> DeleteQsAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false);
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
                throw new ConstructionOperationException("Baseline trong giai đoạn thi công phải đi qua quy trình VO/điều chỉnh đã được duyệt trước khi có thể tạo phiên bản chuẩn bị mới.");
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
        if (item.Status != ConstructionPreparationStatus.ReadyToStart) throw new ConstructionOperationException("Phải hoàn tất đánh giá mức độ sẵn sàng trước khi khởi công.");
        CrmConcurrency.Apply(db, item, rowVersion);
        var missing = await MissingReadinessAsync(projectId, item, ct);
        if (missing.Count != 0) throw new ConstructionOperationException($"Không thể khởi công. Còn thiếu: {string.Join(", ", missing)}.");
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
                IsProcurement = x.RoleEntity != null && x.RoleEntity.Code == RbacRoleCodes.Procurement,
                RoleCode = x.RoleEntity != null ? x.RoleEntity.Code : x.Role.ToString(),
                RoleName = x.RoleEntity != null ? x.RoleEntity.Name : x.Role.ToString(),
                RoleLabelKey = x.RoleEntity != null ? x.RoleEntity.LabelKey : null,
                DepartmentId = x.RoleEntity != null && x.RoleEntity.RoleGroup != null && x.RoleEntity.RoleGroup.IsActive ? x.RoleEntity.RoleGroupId : null,
                DepartmentLabelKey = x.RoleEntity != null && x.RoleEntity.RoleGroup != null && x.RoleEntity.RoleGroup.IsActive ? x.RoleEntity.RoleGroup.LabelKey : null,
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        var siteUsers = users.Select(x => new ConstructionResourceUserReferenceResponse(x.Id, x.Name, x.RoleCode, x.RoleName, x.RoleLabelKey, x.DepartmentId, x.DepartmentLabelKey)).ToList();
        var procurementUsers = users.Where(x => x.IsProcurement)
            .Select(x => new ConstructionResourceUserReferenceResponse(x.Id, x.Name, x.RoleCode, x.RoleName, x.RoleLabelKey, x.DepartmentId, x.DepartmentLabelKey)).ToList();
        return new ConstructionResourceReferenceDataResponse(boqLines, siteUsers, procurementUsers);
    }
    public async Task<ConstructionResourceRequestResponse?> GetResourceAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await db.ConstructionResourceRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    public async Task<DeletionImpactResponse?> GetResourceDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct)
    {
        var item = await db.ConstructionResourceRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDeletionViewer(item.CreatedByUserId, userId, supervisor);
        var ownedCount = await db.Set<ConstructionResourceHandoffEvent>()
            .CountAsync(x => x.ConstructionResourceRequestId == id, ct);
        var blocked = item.Status is not (ConstructionReviewStatus.Draft or ConstructionReviewStatus.CmRejected) ||
            item.HandoffEntityId.HasValue || item.ProcurementContractId.HasValue || item.SupplyDocumentId.HasValue;
        return BuildDeletionImpact(nameof(ConstructionResourceRequest), item.Id, item.Code,
            item.Status, item.RowVersion, blocked, ownedCount, "construction.resourceHandoffEvents");
    }

    public async Task<ConstructionResourceRequestResponse> CreateResourceAsync(int projectId, UpsertConstructionResourceRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ValidateResource(projectId, request, ct);
        var next = await db.ConstructionResourceRequests.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new ConstructionResourceRequest { OperationalProjectId = projectId, Code = $"RR-{projectId:D4}-{next:D4}", CreatedByUserId = userId, LastEditedByUserId = userId };
        Apply(item, request); db.ConstructionResourceRequests.Add(item);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    public async Task<ConstructionResourceRequestResponse?> UpdateResourceAsync(int projectId, int id, UpsertConstructionResourceRequest request, int userId, CancellationToken ct, bool supervisor = false)
    {
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor);
        await ValidateResource(projectId, request, ct); CrmConcurrency.Apply(db, item, request.RowVersion); Apply(item, request); item.LastEditedByUserId = userId;
        await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }

    public async Task<ConstructionResourceRequestResponse?> SubmitResourceAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false)
    {
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        if (item.Status == ConstructionReviewStatus.PendingCmReview) return Map(item);
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor);
        CrmConcurrency.Apply(db, item, rowVersion); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; item.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }

    public async Task<bool> DeleteResourceAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false)
    {
        var impact = await GetResourceDeletionImpactAsync(projectId, id, userId, supervisor, ct);
        if (impact is null) return false;
        ValidateDeletion(impact, request);
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return false;
        EnsureDeletable(item.Status, item.CreatedByUserId, userId, supervisor);
        CrmConcurrency.Apply(db, item, request.RowVersion!);
        db.ConstructionResourceRequests.Remove(item);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return true;
    }

    public async Task<ConstructionResourceRequestResponse?> DecideResourceAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var item = await db.ConstructionResourceRequests.SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDecision(item.Status, item.CreatedByUserId, item.LastEditedByUserId, userId, request.Reason); CrmConcurrency.Apply(db, item, request.RowVersion);
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
        EnsureText(request.Code, "Mã mẫu"); EnsureText(request.NameVi, "Tên mẫu tiếng Việt"); EnsureText(request.Name, "Tên mẫu tiếng Anh"); EnsureText(request.NameZh, "Tên mẫu tiếng Trung"); EnsureText(request.NameJa, "Tên mẫu tiếng Nhật");
        ValidateQaQcTemplateSchema(request.SchemaJson, requireAcceptanceCriteria: true);
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
    public async Task<DeletionImpactResponse?> GetDossierDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct)
    {
        var item = await Dossiers().AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDeletionViewer(item.CreatedByUserId, userId, supervisor);
        return BuildDeletionImpact(nameof(QaQcDossier), item.Id, item.Code, item.Status, item.RowVersion,
            item.Status is not (ConstructionReviewStatus.Draft or ConstructionReviewStatus.CmRejected),
            item.Evidence.Count, "construction.qaQcEvidenceLinks");
    }
    public async Task<QaQcDossierResponse> CreateDossierAsync(int projectId, CreateQaQcDossierRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var template = await ValidateDossier(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList() : request.EvidenceDocumentIds, userId, ct);
        var next = await db.QaQcDossiers.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new QaQcDossier { OperationalProjectId = projectId, Code = $"QA-{projectId:D4}-{next:D4}", CreatedByUserId = userId, LastEditedByUserId = userId };
        Apply(item, request, template); db.QaQcDossiers.Add(item); await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item)!;
    }
    public async Task<QaQcDossierResponse?> UpdateDossierAsync(int projectId, int id, CreateQaQcDossierRequest request, string rowVersion, int userId, CancellationToken ct, bool supervisor = false)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var item = await Dossiers().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor); var template = await ValidateDossier(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList() : request.EvidenceDocumentIds, userId, ct);
        CrmConcurrency.Apply(db, item, rowVersion); Apply(item, request, template); item.LastEditedByUserId = userId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }
    public Task<QaQcDossierResponse?> SubmitDossierAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false) => TransitionDossier(projectId, id, rowVersion, userId, supervisor, null, ct);
    public Task<QaQcDossierResponse?> DecideDossierAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct) => TransitionDossier(projectId, id, request.RowVersion!, userId, false, request, ct);

    public async Task<bool> DeleteDossierAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false)
    {
        var impact = await GetDossierDeletionImpactAsync(projectId, id, userId, supervisor, ct);
        if (impact is null) return false;
        ValidateDeletion(impact, request);
        var item = await Dossiers().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return false;
        EnsureDeletable(item.Status, item.CreatedByUserId, userId, supervisor);
        CrmConcurrency.Apply(db, item, request.RowVersion!);
        db.QaQcDossiers.Remove(item);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return true;
    }

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
    public async Task<DeletionImpactResponse?> GetQsDeletionImpactAsync(int projectId, int id, int userId, bool supervisor, CancellationToken ct)
    {
        var item = await Qs().AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return null;
        EnsureDeletionViewer(item.CreatedByUserId, userId, supervisor);
        return BuildDeletionImpact(nameof(ConstructionQsBatch), item.Id, item.Code, item.Status, item.RowVersion,
            item.Status is not (ConstructionReviewStatus.Draft or ConstructionReviewStatus.CmRejected),
            item.Lines.Count + item.Evidence.Count, "construction.qsOwnedRecords");
    }
    public async Task<ConstructionQsBatchResponse> CreateQsAsync(int projectId, CreateConstructionQsBatchRequest request, int userId, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await ValidateQs(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.EvidenceDocumentIds, userId, ct);
        var next = await db.ConstructionQsBatches.CountAsync(x => x.OperationalProjectId == projectId, ct) + 1;
        var item = new ConstructionQsBatch { OperationalProjectId = projectId, Code = $"QS-{projectId:D4}-{next:D4}", CreatedByUserId = userId, LastEditedByUserId = userId }; Apply(item, request);
        db.ConstructionQsBatches.Add(item); await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item)!;
    }
    public async Task<ConstructionQsBatchResponse?> UpdateQsAsync(int projectId, int id, CreateConstructionQsBatchRequest request, string rowVersion, int userId, CancellationToken ct, bool supervisor = false)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var item = await Qs().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor); await ValidateQs(projectId, request, ct);
        await PinEvidenceAsync(projectId, request.EvidenceDocumentIds, userId, ct);
        CrmConcurrency.Apply(db, item, rowVersion); Apply(item, request); item.LastEditedByUserId = userId;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }
    public Task<ConstructionQsBatchResponse?> SubmitQsAsync(int projectId, int id, string rowVersion, int userId, CancellationToken ct, bool supervisor = false) => TransitionQs(projectId, id, rowVersion, userId, supervisor, null, ct);
    public Task<ConstructionQsBatchResponse?> DecideQsAsync(int projectId, int id, CmDecisionRequest request, int userId, CancellationToken ct) => TransitionQs(projectId, id, request.RowVersion!, userId, false, request, ct);

    public async Task<bool> DeleteQsAsync(int projectId, int id, ConfirmDeletionRequest request, int userId, CancellationToken ct, bool supervisor = false)
    {
        var impact = await GetQsDeletionImpactAsync(projectId, id, userId, supervisor, ct);
        if (impact is null) return false;
        ValidateDeletion(impact, request);
        var item = await Qs().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct);
        if (item is null) return false;
        EnsureDeletable(item.Status, item.CreatedByUserId, userId, supervisor);
        CrmConcurrency.Apply(db, item, request.RowVersion!);
        db.ConstructionQsBatches.Remove(item);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return true;
    }

    private async Task<QaQcDossierResponse?> TransitionDossier(int projectId, int id, string rowVersion, int userId, bool supervisor, CmDecisionRequest? decision, CancellationToken ct)
    {
        var item = await Dossiers().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        CrmConcurrency.Apply(db, item, rowVersion);
        if (decision is null) { EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; }
        else { EnsureDecision(item.Status, item.CreatedByUserId, item.LastEditedByUserId, userId, decision.Reason); item.Status = decision.Approved ? ConstructionReviewStatus.CmApproved : ConstructionReviewStatus.CmRejected; item.CmReviewedAt = DateTime.UtcNow; item.CmReviewedByUserId = userId; item.CmDecisionReason = decision.Reason.Trim(); }
        item.UpdatedAt = DateTime.UtcNow; await CrmConcurrency.SaveChangesAsync(db, ct); return Map(item);
    }
    private async Task<ConstructionQsBatchResponse?> TransitionQs(int projectId, int id, string rowVersion, int userId, bool supervisor, CmDecisionRequest? decision, CancellationToken ct)
    {
        await using var tx = decision is not null && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        var item = await Qs().SingleOrDefaultAsync(x => x.OperationalProjectId == projectId && x.Id == id, ct); if (item is null) return null;
        CrmConcurrency.Apply(db, item, rowVersion);
        if (decision is null) { EnsureDraftOwner(item.Status, item.CreatedByUserId, userId, supervisor); item.Status = ConstructionReviewStatus.PendingCmReview; item.SubmittedAt = DateTime.UtcNow; }
        else
        {
            EnsureDecision(item.Status, item.CreatedByUserId, item.LastEditedByUserId, userId, decision.Reason);
            var predecessorId = item.SupersedesBatchId ?? item.ReversesBatchId;
            if (decision.Approved && predecessorId is int approvedPredecessorId)
            {
                var predecessor = await db.ConstructionQsBatches.SingleAsync(x => x.Id == approvedPredecessorId, ct);
                if (predecessor.RecordState != ConstructionQsRecordState.Active)
                    throw new ConstructionOperationException("Lô QS được tham chiếu đã có bản điều chỉnh hoặc đảo giá trị được duyệt.");
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
            throw new ConstructionOperationException("Yêu cầu vật tư phải có dòng BOQ, người phụ trách tại công trường và người mua hàng được phân công trước khi CM duyệt.");
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
        if (!Enum.IsDefined(request.Type)) throw new ConstructionOperationException("Loại nguồn lực phải là vật tư, thiết bị hoặc nhân công.");
        EnsureText(request.WbsCode, "Mã WBS"); EnsureText(request.Unit, "Đơn vị tính"); EnsureText(request.Reason, "Lý do");
        if (request.NeedByDate!.Value.Date < DateTime.UtcNow.Date) throw new ConstructionOperationException("Ngày cần không được ở trong quá khứ.");
        if (request.Type == ConstructionResourceType.Material && request.ProjectBoqLineId is null) throw new ConstructionOperationException("Yêu cầu vật tư phải tham chiếu một dòng BOQ thi công đã được duyệt.");
        if (request.Type == ConstructionResourceType.Material &&
            (request.ResponsibleSiteUserId is null || request.AssignedProcurementUserId is null))
            throw new ConstructionOperationException("Yêu cầu vật tư phải có người phụ trách tại công trường và người mua hàng được phân công.");
        if (request.ProjectBoqLineId is not null && !await db.ProjectBoqLines.AnyAsync(x => x.Id == request.ProjectBoqLineId && x.ProjectBoqRevision.OperationalProjectId == projectId && x.ProjectBoqRevision.Status == ProjectBoqRevisionStatus.Approved, ct)) throw new ConstructionOperationException("Dòng BOQ phải thuộc một phiên bản BOQ dự án đã được duyệt.");
        if (request.ResponsibleSiteUserId is int siteUserId &&
            !await IsActiveProjectUserAsync(projectId, siteUserId, ct))
            throw new ConstructionOperationException("Người phụ trách tại công trường phải là thành viên đang hoạt động của dự án.");
        if (request.AssignedProcurementUserId is int procurementUserId)
        {
            if (!await IsActiveProjectUserAsync(projectId, procurementUserId, ct))
                throw new ConstructionOperationException("Người mua hàng được phân công phải là thành viên đang hoạt động của dự án.");
            var hasProcurementRole = await db.Users.AsNoTracking().AnyAsync(x => x.Id == procurementUserId && x.IsActive &&
                x.RoleEntity != null && x.RoleEntity.Code == RbacRoleCodes.Procurement, ct);
            if (!hasProcurementRole)
                throw new ConstructionOperationException("Người mua hàng được phân công phải giữ vai trò PROCUREMENT.");
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
            throw new ConstructionOperationException("Mọi tài liệu minh chứng phải còn hiệu lực trong dự án này.");
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
        EnsureText(request.WbsCode, "Mã WBS"); EnsureText(request.Title, "Tên hồ sơ");
        var template = await db.QaQcTemplates.SingleOrDefaultAsync(x => x.Id == request.TemplateId && x.IsPublished, ct) ?? throw new ConstructionOperationException("Cần có mẫu QA/QC đã phát hành.");
        ValidateQaQcChecklistResults(template.SchemaJson, request.ChecklistResultsJson);
        if (request.IfcReleaseId is null) throw new ConstructionOperationException("Mỗi hồ sơ QA/QC phải tham chiếu một gói IFC đã phát hành.");
        if (!await db.IfcReleases.AnyAsync(x => x.Id == request.IfcReleaseId && x.Status == IfcReleaseStatus.Released && x.DesignProject.OperationalProjectId == projectId, ct)) throw new ConstructionOperationException("Hồ sơ QA/QC chỉ được tham chiếu gói IFC đã phát hành của cùng dự án.");
        if (request.Evidence is null || request.EvidenceDocumentIds is null || request.Evidence.Any(x => x is null))
            throw new ConstructionOperationException("Danh sách và hạng mục minh chứng QA/QC không được để trống.");
        if (request.Evidence.Count > 0 && request.EvidenceDocumentIds.Count > 0)
            throw new ConstructionOperationException("Chỉ chọn một trong hai: minh chứng QA/QC có kiểu hoặc mã tài liệu minh chứng cũ.");
        if (request.Evidence.Any(x => x.ProjectDocumentId <= 0 || !Enum.IsDefined(x.EvidenceType)))
            throw new ConstructionOperationException("Mỗi hạng mục minh chứng QA/QC cần mã tài liệu dương và kiểu minh chứng hợp lệ.");
        var evidenceIds = request.Evidence.Count > 0
            ? request.Evidence.Select(x => x.ProjectDocumentId).ToList()
            : request.EvidenceDocumentIds;
        if (evidenceIds.Count != evidenceIds.Distinct().Count() ||
            await db.ProjectDocuments.CountAsync(x => evidenceIds.Contains(x.Id) &&
                x.OperationalProjectId == projectId && x.DeletedAt == null, ct) != evidenceIds.Count)
            throw new ConstructionOperationException("Mọi tài liệu minh chứng phải là tài liệu đã đưa vào dự án, còn hiệu lực và chỉ xuất hiện một lần.");
        return template;
    }

    private async Task ValidateQs(int projectId, CreateConstructionQsBatchRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Direction)) throw new ConstructionOperationException("Chiều QS phải là Upstream hoặc Downstream.");
        EnsureText(request.WbsCode, "Mã WBS"); EnsureText(request.Description, "Mô tả QS");
        if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.ItemCode) || string.IsNullOrWhiteSpace(x.Description) || string.IsNullOrWhiteSpace(x.Unit))) throw new ConstructionOperationException("Mỗi dòng QS phải có mã hạng mục, mô tả và đơn vị tính.");
        if (request.PeriodFrom > request.PeriodTo) throw new ConstructionOperationException("Ngày bắt đầu kỳ QS không được sau ngày kết thúc.");
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ContractId && x.OperationalProjectId == projectId, ct) ?? throw new ConstructionOperationException("Hợp đồng phải thuộc dự án vận hành.");
        if (contract.Status is ContractStatus.Draft or ContractStatus.Cancelled)
            throw new ConstructionOperationException("Lô QS phải gắn với hợp đồng đã ký hoặc đang hiệu lực.");
        if ((request.Direction == ConstructionQsDirection.Upstream) != (contract.Direction == ContractDirection.Upstream)) throw new ConstructionOperationException("Chiều QS phải khớp với chiều của hợp đồng được liên kết.");
        if (request.ContractPaymentMilestoneId is not null && !await db.ContractPaymentMilestones.AnyAsync(x => x.Id == request.ContractPaymentMilestoneId && x.ContractId == request.ContractId, ct)) throw new ConstructionOperationException("Mốc thanh toán phải thuộc hợp đồng được liên kết.");
        if (request.ContractAppendixId is not null && !await db.ContractAppendices.AnyAsync(x =>
                x.Id == request.ContractAppendixId && x.ContractId == request.ContractId &&
                x.Status == ContractAppendixStatus.Approved, ct))
            throw new ConstructionOperationException("VO/phụ lục phải được duyệt và thuộc hợp đồng được liên kết.");
        if (request.ProjectBoqRevisionId is not null && !await db.ProjectBoqRevisions.AnyAsync(x => x.Id == request.ProjectBoqRevisionId && x.OperationalProjectId == projectId && x.Status == ProjectBoqRevisionStatus.Approved, ct)) throw new ConstructionOperationException("Phiên bản BOQ phải được duyệt và thuộc dự án.");
        if (request.Direction == ConstructionQsDirection.Upstream && request.ContractPaymentMilestoneId is null) throw new ConstructionOperationException("QS Đầu vào cần có mốc thanh toán phải thu.");
        if (request.SupersedesBatchId is not null && request.ReversesBatchId is not null) throw new ConstructionOperationException("Điều chỉnh QS chỉ được thay thế hoặc đảo một lô trước đó, không được làm cả hai.");
        var correctionId = request.SupersedesBatchId ?? request.ReversesBatchId;
        ConstructionQsBatch? predecessor = null;
        if (correctionId is not null)
        {
            predecessor = await db.ConstructionQsBatches.Include(x => x.Lines).SingleOrDefaultAsync(x =>
                x.Id == correctionId && x.OperationalProjectId == projectId && x.Direction == request.Direction &&
                x.Status == ConstructionReviewStatus.CmApproved && x.RecordState == ConstructionQsRecordState.Active, ct);
            if (predecessor is null) throw new ConstructionOperationException("Điều chỉnh QS phải tham chiếu một lô đã duyệt còn hiệu lực trong cùng dự án và cùng chiều.");
            if (predecessor.ContractId != request.ContractId || predecessor.WbsCode != request.WbsCode.Trim())
                throw new ConstructionOperationException("Điều chỉnh hoặc đảo QS phải giữ nguyên hợp đồng và WBS của lô trước.");
            if (await db.ConstructionQsBatches.AnyAsync(x =>
                    (x.SupersedesBatchId == correctionId || x.ReversesBatchId == correctionId) && x.IsActiveLineage, ct))
                throw new ConstructionOperationException("Lô QS được tham chiếu đã có lô kế nhiệm đang hiệu lực.");
        }
        if (request.EvidenceDocumentIds.Count != request.EvidenceDocumentIds.Distinct().Count() || await db.ProjectDocuments.CountAsync(x => request.EvidenceDocumentIds.Contains(x.Id) && x.OperationalProjectId == projectId && x.DeletedAt == null, ct) != request.EvidenceDocumentIds.Count) throw new ConstructionOperationException("Mọi tài liệu minh chứng QS phải còn hiệu lực và thuộc dự án.");
        var computed = request.Lines.Sum(x => x.Quantity * x.UnitPrice); if (computed != request.CertifiedAmount) throw new ConstructionOperationException("Giá trị xác nhận phải bằng tổng giá trị các dòng khối lượng QS.");
        if (request.ReversesBatchId is not null && predecessor is not null)
        {
            var requestedLines = request.Lines.OrderBy(x => x.ItemCode).ToList();
            var predecessorLines = predecessor.Lines.OrderBy(x => x.ItemCode).ToList();
            if (request.CertifiedAmount != predecessor.CertifiedAmount || requestedLines.Count != predecessorLines.Count ||
                requestedLines.Where((line, index) => line.ItemCode.Trim() != predecessorLines[index].ItemCode ||
                    line.Quantity != predecessorLines[index].Quantity || line.UnitPrice != predecessorLines[index].UnitPrice).Any())
                throw new ConstructionOperationException("Đảo QS phải phản chiếu chính xác giá trị và các dòng khối lượng của lô trước.");
        }
    }

    private async Task<List<string>> MissingReadinessAsync(int projectId, ConstructionPreparation item, CancellationToken ct)
    {
        var missing = new List<string>();
        var crmId = await db.ProjectScheduleRevisions.Where(x => x.OperationalProjectId == projectId && x.Status == ProjectScheduleRevisionStatus.Published).OrderByDescending(x => x.Version).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        var cmBaseline = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Status == "Published").OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        var tasks = await db.ConstructionTasks.AsNoTracking().Include(x => x.Predecessors).Where(x => x.DesignProject.OperationalProjectId == projectId && x.Status != ConstructionTaskStatus.Cancelled).ToListAsync(ct);
        if (cmBaseline is null || crmId != cmBaseline.CrmRevisionId || !CmScheduleScope.Matches(JsonSerializer.Deserialize<List<CmScheduleLine>>(cmBaseline.ItemsJson)!, tasks)) missing.Add("published CM schedule baseline");
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
        if (!await db.Contracts.AnyAsync(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream && (x.Type == ContractType.Construction || x.Type == ContractType.DesignAndBuild || x.Type == ContractType.PermitAndConstruction || x.Type == ContractType.DesignPermitAndConstruction) && x.Status != ContractStatus.Draft && x.Status != ContractStatus.Cancelled && x.SignedDate != null, ct)) missing.Add("signed upstream construction contract");
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
        var contractIds = await db.Contracts.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream && (x.Type == ContractType.Construction || x.Type == ContractType.DesignAndBuild || x.Type == ContractType.PermitAndConstruction || x.Type == ContractType.DesignPermitAndConstruction) && x.Status != ContractStatus.Draft && x.Status != ContractStatus.Cancelled && x.SignedDate != null).Select(x => x.Id).ToListAsync(ct);
        var ifcReleaseIds = await db.IfcReleases.AsNoTracking().Where(x => x.DesignProject.OperationalProjectId == projectId && x.Status == IfcReleaseStatus.Released).Select(x => x.Id).ToListAsync(ct);
        var permits = await db.PermitChecklistItems.AsNoTracking().Where(x => x.OperationalProjectId == projectId).Select(x => new { x.Id, x.Status, x.ExpiresAt }).ToListAsync(ct);
        var cmScheduleBaseline = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Status == "Published").OrderByDescending(x => x.Version)
            .Select(x => new { x.Id, x.Version, x.CrmRevisionId, x.PublishedAt, x.ReviewedByUserId }).FirstOrDefaultAsync(ct);
        return JsonSerializer.Serialize(new { cmScheduleBaseline, evaluatedAt = DateTime.UtcNow, evaluatedByUserId = userId, preparationId = item.Id, preparationVersion = item.Version, baselines = new { item.WbsBaselineDocumentId, item.ScheduleBaselineDocumentId, item.SCurveBaselineDocumentId, item.BchOrganizationDocumentId, item.ResourcePlanDocumentId, item.BudgetBaselineDocumentId, item.BudgetBaselineLocked }, conditionalEvidence = new { item.SiteHandoverDocumentId, item.MethodStatementDocumentId, item.HsePlanDocumentId }, contractIds, ifcReleaseIds, permits, missing });
    }
    private async Task ValidatePreparationDocumentsAsync(int projectId, UpsertConstructionPreparationRequest request, CancellationToken ct)
    {
        var ids = PreparationDocumentIds(request).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (ids.Count != ids.Distinct().Count()) throw new ConstructionOperationException("Mỗi điều kiện chuẩn bị phải tham chiếu một tài liệu dự án khác nhau.");
        var active = await ActiveConstructionDocumentIdsAsync(projectId, ids, ct);
        if (active.Count != ids.Count) throw new ConstructionOperationException("Minh chứng chuẩn bị phải là biên bản nghiệm thu thi công còn hiệu lực trong cùng dự án vận hành.");
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
    private async Task<ConstructionPreparation> LatestPreparation(int projectId, CancellationToken ct) => await db.ConstructionPreparations.Where(x => x.OperationalProjectId == projectId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) ?? throw new ConstructionOperationException("Chưa có thông tin chuẩn bị khởi công cho dự án này.");
    private static void EnsureRequiredReferences(UpsertConstructionPreparationRequest x) { if (new[] { x.WbsBaselineReference, x.ScheduleBaselineReference, x.SCurveBaselineReference, x.BchOrganizationReference, x.ResourcePlanReference, x.BudgetBaselineReference }.Any(string.IsNullOrWhiteSpace)) throw new ConstructionOperationException("Cần nhập đủ tham chiếu baseline WBS, tiến độ, S-curve, tổ chức BCH, nguồn lực và ngân sách."); }
    private static void EnsureDraftOwner(ConstructionReviewStatus status, int creator, int user, bool supervisor) { if (status != ConstructionReviewStatus.Draft || (!supervisor && creator != user)) throw new ConstructionOperationException("Chỉ tác giả hoặc CM giám sát mới được sửa hoặc trình duyệt bản nháp."); }
    private static void EnsureDeletable(ConstructionReviewStatus status, int creator, int user, bool supervisor)
    {
        if (status is not (ConstructionReviewStatus.Draft or ConstructionReviewStatus.CmRejected))
            throw new ConstructionOperationException("Chỉ xoá được bản ghi ở trạng thái Nháp hoặc Từ chối.");
        if (!supervisor && creator != user)
            throw new ConstructionOperationException("Chỉ tác giả được xoá bản ghi này.");
    }
    private static void EnsureDeletionViewer(int creator, int user, bool supervisor)
    {
        if (!supervisor && creator != user)
            throw new ConstructionOperationException("Chỉ tác giả được xem ảnh hưởng xoá bản ghi này.");
    }
    private static DeletionImpactResponse BuildDeletionImpact(string resourceType, int id, string code,
        ConstructionReviewStatus status, byte[] rowVersion, bool blocked, int ownedCount, string ownedKey)
    {
        var encodedVersion = CrmConcurrency.Encode(rowVersion);
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{resourceType}:{id}:{code}:{status}:{encodedVersion}:{ownedCount}:{blocked}"))).ToLowerInvariant();
        var items = new List<DeletionImpactItemResponse>();
        if (blocked)
            items.Add(new DeletionImpactItemResponse
            {
                Key = "construction.recordState",
                Action = DeletionImpactActions.Block,
                Count = 1,
                Examples = [status.ToString()],
            });
        if (ownedCount > 0)
            items.Add(new DeletionImpactItemResponse
            {
                Key = ownedKey,
                Action = DeletionImpactActions.Delete,
                Count = ownedCount,
            });
        return new DeletionImpactResponse
        {
            ResourceType = resourceType,
            ResourceId = id,
            ResourceLabel = code,
            RequiredConfirmation = code,
            PlanToken = token,
            CanDelete = !blocked,
            TotalAffected = 1 + ownedCount,
            Items = items,
        };
    }
    private static void ValidateDeletion(DeletionImpactResponse impact, ConfirmDeletionRequest request)
    {
        if (!string.Equals(request.PlanToken?.Trim(), impact.PlanToken, StringComparison.Ordinal))
            throw new DeletionPlanChangedException("Dữ liệu liên quan đã thay đổi. Vui lòng xem lại danh sách ảnh hưởng trước khi xoá.");
        if (!impact.CanDelete)
            throw new ConstructionOperationException("Chỉ có thể xoá bản ghi Nháp hoặc Từ chối chưa được bàn giao hay liên kết xử lý tiếp theo.");
        if (!string.Equals(request.Confirmation, impact.RequiredConfirmation, StringComparison.Ordinal))
            throw new ConstructionOperationException($"Mã xác nhận không đúng. Vui lòng nhập chính xác '{impact.RequiredConfirmation}'.");
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            throw new ConstructionOperationException("Phiên bản dữ liệu là bắt buộc. Vui lòng tải lại ảnh hưởng xoá.");
    }
    private static void EnsureDecision(ConstructionReviewStatus status, int creator, int lastEditor, int reviewer, string reason) { if (status != ConstructionReviewStatus.PendingCmReview) throw new ConstructionOperationException("Chỉ bản ghi đang chờ duyệt mới được xem xét."); if (creator == reviewer || lastEditor == reviewer) throw new ConstructionOperationException("Người tạo hoặc chỉnh sửa gần nhất không được tự thực hiện bước duyệt CM độc lập."); EnsureText(reason, "Lý do duyệt CM"); }
    private static void EnsureText(string? value, string field) { if (string.IsNullOrWhiteSpace(value)) throw new ConstructionOperationException($"{field} là bắt buộc và không được chỉ gồm khoảng trắng."); }
    private static void ValidateJson(string value, string field, JsonValueKind expectedKind) { try { using var document = JsonDocument.Parse(value); if (document.RootElement.ValueKind != expectedKind) throw new ConstructionOperationException($"{field} phải là JSON dạng {expectedKind.ToString().ToLowerInvariant()}."); } catch (JsonException) { throw new ConstructionOperationException($"{field} phải là JSON hợp lệ."); } }
    private static HashSet<string> ValidateQaQcTemplateSchema(string value, bool requireAcceptanceCriteria)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array || items.GetArrayLength() == 0)
                throw new ConstructionOperationException("Mẫu QA/QC cần ít nhất một hạng mục kiểm tra.");

            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("code", out var codeValue) ||
                    !item.TryGetProperty("title", out var titleValue) ||
                    codeValue.ValueKind != JsonValueKind.String ||
                    titleValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(codeValue.GetString()) ||
                    string.IsNullOrWhiteSpace(titleValue.GetString()))
                    throw new ConstructionOperationException("Mỗi hạng mục QA/QC cần có mã và tên nội dung kiểm tra.");
                if (!codes.Add(codeValue.GetString()!.Trim()))
                    throw new ConstructionOperationException("Mã hạng mục kiểm tra trong mẫu QA/QC không được trùng nhau.");
                if (requireAcceptanceCriteria &&
                    (!item.TryGetProperty("acceptanceCriteria", out var criteriaValue) ||
                     criteriaValue.ValueKind != JsonValueKind.String ||
                     string.IsNullOrWhiteSpace(criteriaValue.GetString())))
                    throw new ConstructionOperationException("Mỗi hạng mục QA/QC cần ghi rõ tiêu chí đạt.");
            }
            return codes;
        }
        catch (JsonException)
        {
            throw new ConstructionOperationException("Cấu trúc mẫu QA/QC phải là JSON hợp lệ.");
        }
    }

    private static void ValidateQaQcChecklistResults(string schemaJson, string resultsJson)
    {
        var templateCodes = ValidateQaQcTemplateSchema(schemaJson, requireAcceptanceCriteria: false);
        try
        {
            using var document = JsonDocument.Parse(resultsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ConstructionOperationException("Kết quả kiểm tra phải là một danh sách.");
            var resultCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allowedResults = new HashSet<string>(["Pending", "Pass", "Fail", "NotApplicable"], StringComparer.Ordinal);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("code", out var codeValue) ||
                    !item.TryGetProperty("result", out var resultValue) ||
                    codeValue.ValueKind != JsonValueKind.String ||
                    resultValue.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(codeValue.GetString()) ||
                    !allowedResults.Contains(resultValue.GetString()!))
                    throw new ConstructionOperationException("Mỗi kết quả QA/QC cần có hạng mục và một trạng thái hợp lệ.");
                var code = codeValue.GetString()!.Trim();
                if (!templateCodes.Contains(code))
                    throw new ConstructionOperationException($"Hạng mục kiểm tra '{code}' không thuộc mẫu QA/QC đã chọn.");
                if (!resultCodes.Add(code))
                    throw new ConstructionOperationException($"Hạng mục kiểm tra '{code}' chỉ được ghi kết quả một lần.");
            }
        }
        catch (JsonException)
        {
            throw new ConstructionOperationException("Kết quả kiểm tra phải là JSON hợp lệ.");
        }
    }
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
    private static ConstructionResourceRequestResponse Map(ConstructionResourceRequest x) => new(x.Id, x.Code, x.Type, x.Status, x.WbsCode, x.NeedByDate, x.Quantity, x.Unit, x.Reason, x.ProjectBoqLineId, x.ResponsibleSiteUserId, x.AssignedProcurementUserId, x.CreatedByUserId, x.LastEditedByUserId, x.CmDecisionReason, x.HandoffEntityType, x.HandoffEntityId, x.HandoffStatus, CrmConcurrency.Encode(x.RowVersion), x.ProcurementContractId, x.SuppliedAt, x.SuppliedByUserId, x.SupplyDocumentId);
    private static QaQcTemplateResponse Map(QaQcTemplate x) => new(x.Id, x.Code, x.Version, x.NameVi, x.Name, x.NameZh, x.NameJa, x.SchemaJson, x.IsPublished, CrmConcurrency.Encode(x.RowVersion));
    private static QaQcDossierResponse? Map(QaQcDossier? x) => x is null ? null : new(x.Id, x.Code, x.QaQcTemplateId, x.TemplateVersion, x.TemplateSnapshotJson, x.IfcReleaseId, x.ChecklistResultsJson, x.WbsCode, x.Title, x.Status, x.CreatedByUserId, x.LastEditedByUserId, x.Evidence.Select(e => e.ProjectDocumentId).ToList(), x.CmDecisionReason, CrmConcurrency.Encode(x.RowVersion), x.Evidence.Select(e => new QaQcEvidenceResponse(e.ProjectDocumentId, e.EvidenceType)).ToList());
    private static ConstructionQsBatchResponse? Map(ConstructionQsBatch? x) => x is null ? null : new(x.Id, x.Code, x.Direction, x.Status, x.ContractId, x.WbsCode, x.PeriodFrom, x.PeriodTo, x.ProjectBoqRevisionId, x.ContractAppendixId, x.ContractPaymentMilestoneId, x.CertifiedAmount, x.Description, x.CreatedByUserId, x.LastEditedByUserId, x.CmDecisionReason, x.SupersedesBatchId, x.ReversesBatchId, x.AdjustmentKind, x.RecordState, x.Lines.OrderBy(l => l.SortOrder).Select(l => new ConstructionQsBatchLineResponse(l.Id, l.ItemCode, l.Description, l.Unit, l.Quantity, l.UnitPrice, l.Amount, l.SortOrder)).ToList(), x.Evidence.Select(e => e.ProjectDocumentId).ToList(), CrmConcurrency.Encode(x.RowVersion));
}
