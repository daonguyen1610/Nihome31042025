using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ProcurementService(AppDbContext db) : IProcurementService
{
    public async Task<ProjectBoqRevisionListResponse> ListBoqRevisionsAsync(
        int projectId,
        ProjectBoqRevisionListQuery query,
        CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: false, ct);
        var filtered = ApplyBoqListFilters(projectId, query);
        var total = await filtered.CountAsync(ct);
        var items = await SelectBoqListItems(ApplyBoqListSort(filtered, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize))
            .ToListAsync(ct);
        return new ProjectBoqRevisionListResponse
        {
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize,
            Items = items,
        };
    }

    public async Task<IReadOnlyList<ProjectBoqRevisionListItemResponse>> ExportBoqRevisionsAsync(
        int projectId,
        ProjectBoqRevisionListQuery query,
        CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: false, ct);
        return await SelectBoqListItems(
            ApplyBoqListSort(ApplyBoqListFilters(projectId, query), query))
            .ToListAsync(ct);
    }

    public async Task<ProcurementWorkspaceResponse> GetWorkspaceAsync(int projectId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: false, ct);
        var finalRevisionId = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => item.FinalProjectBoqRevisionId)
            .SingleAsync(ct);
        var boq = await BoqQuery().Where(item => item.OperationalProjectId == projectId)
            .OrderByDescending(item => item.RevisionNumber).ToListAsync(ct);
        var requests = await RequestQuery().Where(item => item.OperationalProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).ToListAsync(ct);
        var requestQuantities = await GetMaterialRequestQuantitiesAsync(requests, ct);
        var contractLines = await ContractLineQuery().Where(item => item.Contract.OperationalProjectId == projectId)
            .OrderByDescending(item => item.Id).ToListAsync(ct);
        var receipts = await ReceiptQuery().Where(item => item.OperationalProjectId == projectId)
            .OrderByDescending(item => item.Id).ToListAsync(ct);
        var issues = await IssueQuery().Where(item => item.OperationalProjectId == projectId)
            .OrderByDescending(item => item.Id).ToListAsync(ct);
        var ratings = await RatingQuery().Where(item => item.OperationalProjectId == projectId)
            .OrderByDescending(item => item.Id).ToListAsync(ct);
        return new ProcurementWorkspaceResponse
        {
            BoqRevisions = boq.Select(item => MapBoq(item, finalRevisionId)).ToList(),
            MaterialRequests = requests.Select(item => MapRequest(item, requestQuantities)).ToList(),
            ContractLines = contractLines.Select(MapContractLine).ToList(),
            Receipts = receipts.Select(MapReceipt).ToList(),
            Issues = issues.Select(MapIssue).ToList(),
            VendorRatings = ratings.Select(MapRating).ToList(),
        };
    }

    public async Task<MaterialRequestListResponse> ListMaterialRequestsAsync(
        int projectId,
        MaterialRequestListParams parameters,
        CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: false, ct);
        if (parameters.RequiredFrom > parameters.RequiredTo)
            throw new ProcurementOperationException("Ngày bắt đầu không được sau ngày kết thúc.");
        parameters.Page = Math.Max(parameters.Page, 1);
        parameters.PageSize = Math.Clamp(parameters.PageSize, 1, 100);
        var query = RequestQuery().Where(item => item.OperationalProjectId == projectId);
        if (parameters.Status.HasValue)
            query = query.Where(item => item.Status == parameters.Status.Value);
        if (parameters.SiteRequesterUserId.HasValue)
            query = query.Where(item => item.SiteRequesterUserId == parameters.SiteRequesterUserId.Value);
        if (parameters.ResponsibleSiteUserId.HasValue)
            query = query.Where(item => item.ResponsibleSiteUserId == parameters.ResponsibleSiteUserId.Value);
        if (parameters.AssignedProcurementUserId.HasValue)
            query = query.Where(item => item.AssignedProcurementUserId == parameters.AssignedProcurementUserId.Value);
        if (parameters.RequiredFrom.HasValue)
        {
            var requiredFrom = parameters.RequiredFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.RequiredAt >= requiredFrom);
        }
        if (parameters.RequiredTo.HasValue)
        {
            var requiredTo = parameters.RequiredTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(item => item.RequiredAt <= requiredTo);
        }
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = $"%{parameters.Search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Code, pattern) ||
                (item.Note != null && EF.Functions.Like(item.Note, pattern)) ||
                EF.Functions.Like(item.SiteRequester.FullName, pattern) ||
                EF.Functions.Like(item.ResponsibleSiteUser.FullName, pattern) ||
                EF.Functions.Like(item.AssignedProcurementUser.FullName, pattern) ||
                item.Lines.Any(line =>
                    EF.Functions.Like(line.ProjectBoqLine.ItemCode, pattern) ||
                    EF.Functions.Like(line.ProjectBoqLine.Description, pattern)));
        }

        var descending = string.Equals(parameters.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<MaterialRequest> ordered = (parameters.SortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("code", false) => query.OrderBy(item => item.Code),
            ("code", true) => query.OrderByDescending(item => item.Code),
            ("status", false) => query.OrderBy(item => item.Status),
            ("status", true) => query.OrderByDescending(item => item.Status),
            ("requiredat", false) => query.OrderBy(item => item.RequiredAt),
            ("requiredat", true) => query.OrderByDescending(item => item.RequiredAt),
            ("updatedat", false) => query.OrderBy(item => item.UpdatedAt),
            _ => query.OrderByDescending(item => item.UpdatedAt),
        };

        var items = await ordered.ThenByDescending(item => item.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(ct);
        var quantities = await GetMaterialRequestQuantitiesAsync(items, ct);
        return new MaterialRequestListResponse
        {
            Total = await query.CountAsync(ct),
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            Items = items.Select(item => MapRequest(item, quantities)).ToList(),
        };
    }

    public async Task<ProjectBoqRevisionResponse> CreateBoqRevisionAsync(
        int projectId, ProjectBoqRevisionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await ValidateBoqAsync(projectId, request, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var nextRevision = (await db.ProjectBoqRevisions.Where(item => item.OperationalProjectId == projectId)
            .MaxAsync(item => (int?)item.RevisionNumber, ct) ?? 0) + 1;
        var entity = new ProjectBoqRevision
        {
            OperationalProjectId = projectId,
            RevisionNumber = nextRevision,
            PreparedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyBoq(entity, request);
        db.ProjectBoqRevisions.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetBoqAsync(projectId, entity.Id, ct))!;
    }

    public async Task<ProjectBoqRevisionResponse?> UpdateBoqRevisionAsync(
        int projectId, int id, ProjectBoqRevisionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.ProjectBoqRevisions.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status is not (ProjectBoqRevisionStatus.Draft or ProjectBoqRevisionStatus.Rejected))
            throw new ProcurementOperationException("Chỉ được sửa BOQ ở trạng thái Nháp hoặc Bị từ chối.");
        await ValidateBoqAsync(projectId, request, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        db.ProjectBoqLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        entity.Status = ProjectBoqRevisionStatus.Draft;
        entity.DecisionReason = null;
        entity.RejectedAt = null;
        entity.RejectedByUserId = null;
        entity.PreparedByUserId = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        ApplyBoq(entity, request);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetBoqAsync(projectId, id, ct);
    }

    public async Task<ProjectBoqRevisionResponse?> SubmitBoqRevisionAsync(
        int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.ProjectBoqRevisions.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == ProjectBoqRevisionStatus.Submitted) return await GetBoqAsync(projectId, id, ct);
        if (entity.Status is not (ProjectBoqRevisionStatus.Draft or ProjectBoqRevisionStatus.Rejected))
            throw new ProcurementOperationException("BOQ không thể được gửi từ trạng thái hiện tại.");
        if (entity.Lines.Count == 0) throw new ProcurementOperationException("BOQ phải có ít nhất một dòng.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = ProjectBoqRevisionStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.SubmittedByUserId = userId;
        entity.DecisionReason = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetBoqAsync(projectId, id, ct);
    }

    public async Task<ProjectBoqRevisionResponse?> DecideBoqRevisionAsync(
        int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.ProjectBoqRevisions.SingleOrDefaultAsync(item =>
            item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == (request.Approved ? ProjectBoqRevisionStatus.Approved : ProjectBoqRevisionStatus.Rejected))
            return await GetBoqAsync(projectId, id, ct);
        if (entity.Status != ProjectBoqRevisionStatus.Submitted)
            throw new ProcurementOperationException("Chỉ BOQ đã gửi mới có thể được phê duyệt hoặc từ chối.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementOperationException("Lý do từ chối BOQ là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        entity.Status = request.Approved ? ProjectBoqRevisionStatus.Approved : ProjectBoqRevisionStatus.Rejected;
        entity.ApprovedAt = request.Approved ? now : null;
        entity.ApprovedByUserId = request.Approved ? userId : null;
        entity.RejectedAt = request.Approved ? null : now;
        entity.RejectedByUserId = request.Approved ? null : userId;
        entity.DecisionReason = TrimOrNull(request.Reason);
        entity.UpdatedAt = now;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetBoqAsync(projectId, id, ct);
    }

    public async Task<MaterialRequestResponse> CreateMaterialRequestAsync(
        int projectId, MaterialRequestUpsertRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await ValidateMaterialRequestAsync(projectId, request, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = new MaterialRequest
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync<MaterialRequest>(projectId, "MR", ct),
            SiteRequesterUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyMaterialRequest(entity, request);
        db.MaterialRequests.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetRequestAsync(projectId, entity.Id, ct))!;
    }

    public async Task<MaterialRequestResponse?> UpdateMaterialRequestAsync(
        int projectId, int id, MaterialRequestUpsertRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.MaterialRequests.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status != MaterialRequestStatus.Draft)
            throw new ProcurementOperationException("Chỉ được sửa yêu cầu vật tư ở trạng thái Nháp.");
        if (entity.SiteRequesterUserId != userId)
            throw new ProcurementOperationException("Chỉ người yêu cầu tại công trường được sửa bản nháp này.");
        await ValidateMaterialRequestAsync(projectId, request, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        db.MaterialRequestLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        ApplyMaterialRequest(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetRequestAsync(projectId, id, ct);
    }

    public async Task<MaterialRequestResponse?> SubmitMaterialRequestAsync(
        int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.MaterialRequests.SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == MaterialRequestStatus.Submitted) return await GetRequestAsync(projectId, id, ct);
        if (entity.Status != MaterialRequestStatus.Draft || entity.SiteRequesterUserId != userId)
            throw new ProcurementOperationException("Chỉ người yêu cầu được gửi yêu cầu vật tư đang ở trạng thái Nháp.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = MaterialRequestStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetRequestAsync(projectId, id, ct);
    }

    public async Task<MaterialRequestResponse?> DecideMaterialRequestAsync(
        int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = await db.MaterialRequests.Include(item => item.Lines)
            .ThenInclude(line => line.ProjectBoqLine).ThenInclude(line => line.ProjectBoqRevision)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        var target = request.Approved ? MaterialRequestStatus.Approved : MaterialRequestStatus.Rejected;
        if (entity.Status == target) return await GetRequestAsync(projectId, id, ct);
        if (entity.Status != MaterialRequestStatus.Submitted)
            throw new ProcurementOperationException("Chỉ yêu cầu vật tư đã gửi mới có thể được phê duyệt hoặc từ chối.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementOperationException("Lý do từ chối yêu cầu vật tư là bắt buộc.");
        if (request.Approved) await EnsureBoqAllowanceAsync(entity, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        entity.Status = target;
        entity.ApprovedAt = request.Approved ? now : null;
        entity.ApprovedByUserId = request.Approved ? userId : null;
        entity.RejectedAt = request.Approved ? null : now;
        entity.RejectedByUserId = request.Approved ? null : userId;
        entity.DecisionReason = TrimOrNull(request.Reason);
        entity.UpdatedAt = now;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetRequestAsync(projectId, id, ct);
    }

    public async Task<MaterialRequestResponse?> CancelMaterialRequestAsync(
        int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.MaterialRequests.SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == MaterialRequestStatus.Cancelled) return await GetRequestAsync(projectId, id, ct);
        if (entity.Status is MaterialRequestStatus.Fulfilled or MaterialRequestStatus.Rejected)
            throw new ProcurementOperationException("Yêu cầu đã hoàn tất hoặc bị từ chối không thể hủy.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementOperationException("Lý do hủy yêu cầu vật tư là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = MaterialRequestStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        entity.DecisionReason = request.Reason.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetRequestAsync(projectId, id, ct);
    }

    public async Task<ContractLineResponse> CreateContractLineAsync(
        int projectId, ContractLineUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await ValidateContractLineAsync(projectId, request, null, ct);
        var entity = new ContractLine { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        ApplyContractLine(entity, request);
        db.ContractLines.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return (await GetContractLineAsync(projectId, entity.Id, ct))!;
    }

    public async Task<ContractLineResponse?> UpdateContractLineAsync(
        int projectId, int id, ContractLineUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        var entity = await db.ContractLines.Include(item => item.Contract)
            .SingleOrDefaultAsync(item => item.Id == id && item.Contract.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Contract.Status != ContractStatus.Draft)
            throw new ProcurementOperationException("Dòng hợp đồng đã ký là bất biến.");
        await ValidateContractLineAsync(projectId, request, id, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        ApplyContractLine(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetContractLineAsync(projectId, id, ct);
    }

    public async Task<WarehouseReceiptResponse> CreateReceiptAsync(
        int projectId, WarehouseReceiptCreateRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        if (request.ReceivedByUserId != userId)
            throw new ProcurementOperationException("Người nhận kho phải là người dùng đang thực hiện thao tác.");
        await ValidateReceiptLinesAsync(projectId, request.Lines, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = new WarehouseReceipt
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync<WarehouseReceipt>(projectId, "WR", ct),
            ReceivedByUserId = userId,
            InspectedAt = request.InspectedAt!.Value.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = request.Lines.Select(line => new WarehouseReceiptLine
            {
                MaterialRequestLineId = line.MaterialRequestLineId,
                ContractLineId = line.ContractLineId,
                ReceivedQuantity = line.ReceivedQuantity,
            }).ToList(),
        };
        db.WarehouseReceipts.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetReceiptAsync(projectId, entity.Id, ct))!;
    }

    public async Task<WarehouseReceiptResponse?> PostReceiptAsync(
        int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = await db.WarehouseReceipts.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == WarehouseLedgerStatus.Posted) return await GetReceiptAsync(projectId, id, ct);
        if (entity.Status != WarehouseLedgerStatus.Draft)
            throw new ProcurementOperationException("Chỉ phiếu nhập kho ở trạng thái Nháp mới được ghi sổ.");
        await EnsureReceiptDoesNotOverfillAsync(entity, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = WarehouseLedgerStatus.Posted;
        entity.PostedAt = DateTime.UtcNow;
        entity.PostedByUserId = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        await RefreshRequestStatusesAsync(entity.Lines.Select(item => item.MaterialRequestLineId), entity.PostedAt.Value, ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetReceiptAsync(projectId, id, ct);
    }

    public async Task<WarehouseReceiptResponse?> ReverseReceiptAsync(
        int projectId, int id, WarehouseReversalRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var original = await db.WarehouseReceipts.Include(item => item.Lines)
            .ThenInclude(line => line.MaterialRequestLine).ThenInclude(line => line.ProjectBoqLine)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (original is null) return null;
        if (original.Status == WarehouseLedgerStatus.Reversed)
            return await ReceiptQuery().Where(item => item.ReversalOfReceiptId == id).Select(item => MapReceipt(item)).SingleAsync(ct);
        if (original.Status != WarehouseLedgerStatus.Posted || original.ReversalOfReceiptId.HasValue)
            throw new ProcurementOperationException("Chỉ phiếu nhập kho gốc đã ghi sổ mới được đảo.");
        CrmConcurrency.Apply(db, original, request.RowVersion);
        var stock = await StockByItemCodeAsync(projectId, ct);
        foreach (var line in original.Lines)
        {
            var code = line.MaterialRequestLine.ProjectBoqLine.ItemCode;
            if (stock.GetValueOrDefault(code) < line.ReceivedQuantity)
                throw new ProcurementOperationException($"Không thể đảo phiếu nhập vì tồn kho mã '{code}' sẽ âm.");
            stock[code] -= line.ReceivedQuantity;
        }
        var reversal = new WarehouseReceipt
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync<WarehouseReceipt>(projectId, "WR", ct),
            Status = WarehouseLedgerStatus.Posted,
            ReversalOfReceiptId = original.Id,
            ReceivedByUserId = userId,
            InspectedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            PostedByUserId = userId,
            ReversalReason = request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = original.Lines.Select(line => new WarehouseReceiptLine
            {
                MaterialRequestLineId = line.MaterialRequestLineId,
                ContractLineId = line.ContractLineId,
                ReceivedQuantity = line.ReceivedQuantity,
            }).ToList(),
        };
        original.Status = WarehouseLedgerStatus.Reversed;
        original.UpdatedAt = DateTime.UtcNow;
        db.WarehouseReceipts.Add(reversal);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        await RefreshRequestStatusesAsync(original.Lines.Select(item => item.MaterialRequestLineId), reversal.PostedAt.Value, ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetReceiptAsync(projectId, reversal.Id, ct);
    }

    public async Task<WarehouseIssueResponse> CreateIssueAsync(
        int projectId, WarehouseIssueCreateRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        if (request.IssuedByUserId != userId)
            throw new ProcurementOperationException("Người xuất kho phải là người dùng đang thực hiện thao tác.");
        await ValidateProjectUserAsync(projectId, request.ResponsibleSiteUserId, null, "Người chịu trách nhiệm tại công trường", ct);
        await ValidateIssueLinesAsync(projectId, request.Lines, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = new WarehouseIssue
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync<WarehouseIssue>(projectId, "WI", ct),
            ResponsibleSiteUserId = request.ResponsibleSiteUserId,
            IssuedByUserId = userId,
            IssuedAt = request.IssuedAt!.Value.ToUniversalTime(),
            WorkItemCode = TrimOrNull(request.WorkItemCode),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = request.Lines.Select(line => new WarehouseIssueLine
            {
                ProjectBoqLineId = line.ProjectBoqLineId,
                IssuedQuantity = line.IssuedQuantity,
            }).ToList(),
        };
        db.WarehouseIssues.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return (await GetIssueAsync(projectId, entity.Id, ct))!;
    }

    public async Task<WarehouseIssueResponse?> PostIssueAsync(
        int projectId, int id, ProcurementTransitionRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = await db.WarehouseIssues.Include(item => item.Lines).ThenInclude(line => line.ProjectBoqLine)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == WarehouseLedgerStatus.Posted) return await GetIssueAsync(projectId, id, ct);
        if (entity.Status != WarehouseLedgerStatus.Draft)
            throw new ProcurementOperationException("Chỉ phiếu xuất kho ở trạng thái Nháp mới được ghi sổ.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var stock = await StockByItemCodeAsync(projectId, ct);
        foreach (var group in entity.Lines.GroupBy(line => line.ProjectBoqLine.ItemCode, StringComparer.OrdinalIgnoreCase))
        {
            var required = group.Sum(line => line.IssuedQuantity);
            if (stock.GetValueOrDefault(group.Key) < required)
                throw new ProcurementOperationException($"Tồn kho mã '{group.Key}' không đủ để ghi sổ phiếu xuất.");
        }
        entity.Status = WarehouseLedgerStatus.Posted;
        entity.PostedAt = DateTime.UtcNow;
        entity.PostedByUserId = userId;
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetIssueAsync(projectId, id, ct);
    }

    public async Task<WarehouseIssueResponse?> ReverseIssueAsync(
        int projectId, int id, WarehouseReversalRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: true, ct);
        await using var transaction = await BeginSerializableAsync(ct);
        var original = await db.WarehouseIssues.Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (original is null) return null;
        if (original.Status == WarehouseLedgerStatus.Reversed)
            return await IssueQuery().Where(item => item.ReversalOfIssueId == id).Select(item => MapIssue(item)).SingleAsync(ct);
        if (original.Status != WarehouseLedgerStatus.Posted || original.ReversalOfIssueId.HasValue)
            throw new ProcurementOperationException("Chỉ phiếu xuất kho gốc đã ghi sổ mới được đảo.");
        CrmConcurrency.Apply(db, original, request.RowVersion);
        var reversal = new WarehouseIssue
        {
            OperationalProjectId = projectId,
            Code = await AllocateCodeAsync<WarehouseIssue>(projectId, "WI", ct),
            Status = WarehouseLedgerStatus.Posted,
            ReversalOfIssueId = original.Id,
            ResponsibleSiteUserId = original.ResponsibleSiteUserId,
            IssuedByUserId = userId,
            IssuedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            PostedByUserId = userId,
            WorkItemCode = original.WorkItemCode,
            ReversalReason = request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = original.Lines.Select(line => new WarehouseIssueLine
            {
                ProjectBoqLineId = line.ProjectBoqLineId,
                IssuedQuantity = line.IssuedQuantity,
            }).ToList(),
        };
        original.Status = WarehouseLedgerStatus.Reversed;
        original.UpdatedAt = DateTime.UtcNow;
        db.WarehouseIssues.Add(reversal);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetIssueAsync(projectId, reversal.Id, ct);
    }

    public async Task<VendorRatingResponse> CreateVendorRatingAsync(
        int projectId, VendorRatingUpsertRequest request, int userId, CancellationToken ct = default)
    {
        await EnsureProjectAsync(projectId, mutable: false, ct);
        var source = await ValidateRatingAsync(projectId, request, null, ct);
        if (await db.VendorRatings.AnyAsync(item => item.ContractId == request.ContractId &&
            (item.Status == VendorRatingStatus.Draft || item.Status == VendorRatingStatus.Submitted), ct))
            throw new ProcurementOperationException("Hợp đồng đã có một bản đánh giá đang xử lý.");
        var previous = await db.VendorRatings.Where(item => item.ContractId == request.ContractId && item.Status == VendorRatingStatus.Approved)
            .OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(ct);
        var version = (await db.VendorRatings.Where(item => item.ContractId == request.ContractId)
            .MaxAsync(item => (int?)item.VersionNumber, ct) ?? 0) + 1;
        var entity = new VendorRating
        {
            OperationalProjectId = projectId,
            ContractId = request.ContractId,
            VendorId = source.VendorId,
            ProcurementOwnerUserId = source.ProcurementOwnerUserId,
            PreparedByUserId = userId,
            VersionNumber = version,
            SupersedesVendorRatingId = previous?.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyRating(entity, request);
        db.VendorRatings.Add(entity);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return (await GetRatingAsync(projectId, entity.Id, ct))!;
    }

    public async Task<VendorRatingResponse?> UpdateVendorRatingAsync(
        int projectId, int id, VendorRatingUpsertRequest request, CancellationToken ct = default)
    {
        var entity = await db.VendorRatings.SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status != VendorRatingStatus.Draft)
            throw new ProcurementOperationException("Chỉ được sửa đánh giá nhà cung cấp ở trạng thái Nháp.");
        if (request.ContractId != entity.ContractId)
            throw new ProcurementOperationException("Không được thay đổi hợp đồng của bản đánh giá.");
        await ValidateRatingAsync(projectId, request, id, ct);
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        ApplyRating(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetRatingAsync(projectId, id, ct);
    }

    public async Task<VendorRatingResponse?> SubmitVendorRatingAsync(
        int projectId, int id, ProcurementTransitionRequest request, CancellationToken ct = default)
    {
        var entity = await db.VendorRatings.SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        if (entity.Status == VendorRatingStatus.Submitted) return await GetRatingAsync(projectId, id, ct);
        if (entity.Status != VendorRatingStatus.Draft)
            throw new ProcurementOperationException("Chỉ bản đánh giá Nháp mới được gửi.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        entity.Status = VendorRatingStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return await GetRatingAsync(projectId, id, ct);
    }

    public async Task<VendorRatingResponse?> DecideVendorRatingAsync(
        int projectId, int id, ProcurementDecisionRequest request, int userId, CancellationToken ct = default)
    {
        var project = await db.OperationalProjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == projectId, ct);
        if (project is null) return null;
        if (project.ProjectManagerUserId != userId)
            throw new ProcurementOperationException("Chỉ PM chịu trách nhiệm của dự án được phê duyệt đánh giá nhà cung cấp.");
        await using var transaction = await BeginSerializableAsync(ct);
        var entity = await db.VendorRatings.SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (entity is null) return null;
        var target = request.Approved ? VendorRatingStatus.Approved : VendorRatingStatus.Rejected;
        if (entity.Status == target) return await GetRatingAsync(projectId, id, ct);
        if (entity.Status != VendorRatingStatus.Submitted)
            throw new ProcurementOperationException("Chỉ bản đánh giá đã gửi mới được phê duyệt hoặc từ chối.");
        if (entity.PreparedByUserId == userId)
            throw new ProcurementOperationException("Người lập không được tự phê duyệt đánh giá nhà cung cấp.");
        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementOperationException("Lý do từ chối đánh giá là bắt buộc.");
        CrmConcurrency.Apply(db, entity, request.RowVersion);
        var now = DateTime.UtcNow;
        if (request.Approved)
        {
            var previous = await db.VendorRatings.SingleOrDefaultAsync(item =>
                item.ContractId == entity.ContractId && item.Status == VendorRatingStatus.Approved, ct);
            if (previous is not null)
            {
                if (entity.SupersedesVendorRatingId != previous.Id)
                    throw new ProcurementOperationException("Bản đánh giá thay thế không còn khớp với phiên bản đang được duyệt.");
                previous.Status = VendorRatingStatus.Superseded;
                previous.UpdatedAt = now;
            }
        }
        entity.Status = target;
        entity.ApprovedAt = request.Approved ? now : null;
        entity.ApprovedByUserId = request.Approved ? userId : null;
        entity.RejectedAt = request.Approved ? null : now;
        entity.RejectedByUserId = request.Approved ? null : userId;
        entity.DecisionReason = TrimOrNull(request.Reason);
        entity.UpdatedAt = now;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await GetRatingAsync(projectId, id, ct);
    }

    private async Task ValidateBoqAsync(int projectId, ProjectBoqRevisionRequest request, CancellationToken ct)
    {
        var codes = request.Lines.Select(item => item.ItemCode.Trim().ToUpperInvariant()).ToList();
        if (codes.Count != codes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new ProcurementOperationException("Mã hạng mục BOQ phải duy nhất trong mỗi phiên bản.");
        if (request.SourceTenderEstimateRevisionId.HasValue)
        {
            var valid = await db.TenderEstimateRevisions.AsNoTracking().AnyAsync(revision =>
                revision.Id == request.SourceTenderEstimateRevisionId &&
                revision.Status == TenderEstimateRevisionStatus.Approved &&
                revision.Tender.WonOpportunityId != null &&
                db.Opportunities.Any(opportunity => opportunity.Id == revision.Tender.WonOpportunityId &&
                    opportunity.OperationalProjectId == projectId), ct);
            if (!valid) throw new ProcurementOperationException("Dự toán thầu nguồn phải được duyệt và thuộc dự án này.");
        }
        if (request.SourceContractAppendixId.HasValue)
        {
            var valid = await db.ContractAppendices.AsNoTracking().AnyAsync(appendix =>
                appendix.Id == request.SourceContractAppendixId &&
                appendix.Status == ContractAppendixStatus.Approved &&
                appendix.Contract.OperationalProjectId == projectId, ct);
            if (!valid) throw new ProcurementOperationException("VO nguồn phải được duyệt và thuộc dự án này.");
        }
    }

    private static void ApplyBoq(ProjectBoqRevision entity, ProjectBoqRevisionRequest request)
    {
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.SourceTenderEstimateRevisionId = request.SourceTenderEstimateRevisionId;
        entity.SourceContractAppendixId = request.SourceContractAppendixId;
        entity.Lines = request.Lines.Select((line, index) => new ProjectBoqLine
        {
            ItemCode = line.ItemCode.Trim().ToUpperInvariant(),
            Description = line.Description.Trim(),
            Unit = line.Unit.Trim(),
            ApprovedQuantity = line.ApprovedQuantity,
            BudgetUnitPrice = line.BudgetUnitPrice,
            Amount = Math.Round(line.ApprovedQuantity * line.BudgetUnitPrice, 4, MidpointRounding.AwayFromZero),
            SortOrder = index,
        }).ToList();
        entity.CostTotal = entity.Lines.Sum(item => item.Amount);
    }

    private async Task ValidateMaterialRequestAsync(int projectId, MaterialRequestUpsertRequest request, CancellationToken ct)
    {
        if (request.RequiredAt <= DateTime.UtcNow)
            throw new ProcurementOperationException("Ngày cần vật tư phải ở tương lai.");
        await ValidateProjectUserAsync(projectId, request.ResponsibleSiteUserId, null, "Người chịu trách nhiệm tại công trường", ct);
        await ValidateProjectUserAsync(projectId, request.AssignedProcurementUserId, "PROCUREMENT", "Nhân viên mua hàng", ct);
        var ids = request.Lines.Select(item => item.ProjectBoqLineId).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new ProcurementOperationException("Mỗi dòng BOQ chỉ được xuất hiện một lần trong yêu cầu vật tư.");
        var currentRevisionId = await CurrentApprovedRevisionIdAsync(projectId, ct);
        if (!currentRevisionId.HasValue)
            throw new ProcurementOperationException("Dự án chưa có BOQ thi công được duyệt.");
        var count = await db.ProjectBoqLines.CountAsync(item => ids.Contains(item.Id) && item.ProjectBoqRevisionId == currentRevisionId, ct);
        if (count != ids.Count)
            throw new ProcurementOperationException("Tất cả dòng yêu cầu phải thuộc phiên bản BOQ đang được duyệt của dự án.");
    }

    private static void ApplyMaterialRequest(MaterialRequest entity, MaterialRequestUpsertRequest request)
    {
        entity.ResponsibleSiteUserId = request.ResponsibleSiteUserId;
        entity.AssignedProcurementUserId = request.AssignedProcurementUserId;
        entity.RequiredAt = request.RequiredAt!.Value.ToUniversalTime();
        entity.Note = TrimOrNull(request.Note);
        entity.Lines = request.Lines.Select((line, index) => new MaterialRequestLine
        {
            ProjectBoqLineId = line.ProjectBoqLineId,
            RequestedQuantity = line.RequestedQuantity,
            SortOrder = index,
        }).ToList();
    }

    private async Task EnsureBoqAllowanceAsync(MaterialRequest request, CancellationToken ct)
    {
        var currentRevisionId = await CurrentApprovedRevisionIdAsync(request.OperationalProjectId, ct);
        if (!currentRevisionId.HasValue || request.Lines.Any(item => item.ProjectBoqLine.ProjectBoqRevisionId != currentRevisionId))
            throw new ProcurementOperationException("Yêu cầu phải được đối chiếu lại với phiên bản BOQ đang được duyệt.");
        foreach (var line in request.Lines)
        {
            var committed = await db.MaterialRequestLines.AsNoTracking().Where(item =>
                item.ProjectBoqLineId == line.ProjectBoqLineId && item.MaterialRequestId != request.Id &&
                (item.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                 item.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled ||
                 item.MaterialRequest.Status == MaterialRequestStatus.Fulfilled))
                .SumAsync(item => (decimal?)item.RequestedQuantity, ct) ?? 0m;
            if (committed + line.RequestedQuantity > line.ProjectBoqLine.ApprovedQuantity)
                throw new ProcurementOperationException($"Yêu cầu vượt hạn mức còn lại của mã '{line.ProjectBoqLine.ItemCode}'.");
        }
    }

    private async Task ValidateContractLineAsync(int projectId, ContractLineUpsertRequest request, int? excludeId, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ContractId, ct);
        if (contract is null || contract.OperationalProjectId != projectId || contract.Direction != ContractDirection.Downstream ||
            contract.Type is not (ContractType.Supply or ContractType.Subcontract))
            throw new ProcurementOperationException("Dòng mua sắm chỉ được gắn với hợp đồng cung ứng hoặc thầu phụ của dự án.");
        if (contract.Status != ContractStatus.Draft)
            throw new ProcurementOperationException("Chỉ được sửa dòng mua sắm khi hợp đồng còn ở trạng thái Nháp.");
        var currentRevisionId = await CurrentApprovedRevisionIdAsync(projectId, ct);
        if (!await db.ProjectBoqLines.AnyAsync(item => item.Id == request.ProjectBoqLineId && item.ProjectBoqRevisionId == currentRevisionId, ct))
            throw new ProcurementOperationException("Dòng hợp đồng phải tham chiếu BOQ đang được duyệt.");
        await ValidateActiveRoleAsync(request.ProcurementOwnerUserId, "PROCUREMENT", "Chủ sở hữu mua hàng", ct);
        if (await db.ContractLines.AnyAsync(item => item.ContractId == request.ContractId &&
            item.ProjectBoqLineId == request.ProjectBoqLineId && item.Id != excludeId, ct))
            throw new ProcurementOperationException("Hợp đồng đã có dòng cho mã BOQ này.");
    }

    private static void ApplyContractLine(ContractLine entity, ContractLineUpsertRequest request)
    {
        entity.ContractId = request.ContractId;
        entity.ProjectBoqLineId = request.ProjectBoqLineId;
        entity.ProcurementOwnerUserId = request.ProcurementOwnerUserId;
        entity.Quantity = request.Quantity;
        entity.NegotiatedUnitPrice = request.NegotiatedUnitPrice;
    }

    private async Task ValidateReceiptLinesAsync(int projectId, IReadOnlyCollection<WarehouseReceiptLineRequest> lines, CancellationToken ct)
    {
        var ids = lines.Select(item => item.MaterialRequestLineId).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new ProcurementOperationException("Mỗi dòng yêu cầu chỉ được xuất hiện một lần trên phiếu nhập.");
        var sourceLines = await db.MaterialRequestLines.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.MaterialRequest.OperationalProjectId == projectId &&
                (item.MaterialRequest.Status == MaterialRequestStatus.Approved || item.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled))
            .Select(item => new { item.Id, item.ProjectBoqLineId }).ToListAsync(ct);
        if (sourceLines.Count != ids.Count)
            throw new ProcurementOperationException("Dòng nhập kho phải thuộc yêu cầu vật tư đã duyệt và chưa hoàn tất.");
        foreach (var line in lines.Where(item => item.ContractLineId.HasValue))
        {
            var boqLineId = sourceLines.Single(item => item.Id == line.MaterialRequestLineId).ProjectBoqLineId;
            if (!await db.ContractLines.AnyAsync(item => item.Id == line.ContractLineId && item.Contract.OperationalProjectId == projectId &&
                item.ProjectBoqLineId == boqLineId && item.Contract.Status != ContractStatus.Cancelled, ct))
                throw new ProcurementOperationException("Dòng hợp đồng trên phiếu nhập không khớp với dự án và dòng BOQ.");
        }
    }

    private async Task EnsureReceiptDoesNotOverfillAsync(WarehouseReceipt receipt, CancellationToken ct)
    {
        foreach (var line in receipt.Lines)
        {
            var requested = await db.MaterialRequestLines.Where(item => item.Id == line.MaterialRequestLineId)
                .Select(item => item.RequestedQuantity).SingleAsync(ct);
            var received = await NetReceivedAsync(line.MaterialRequestLineId, receipt.Id, ct);
            if (received + line.ReceivedQuantity > requested)
                throw new ProcurementOperationException("Số lượng nhập vượt quá số lượng đã duyệt của yêu cầu vật tư.");
        }
    }

    private async Task RefreshRequestStatusesAsync(IEnumerable<int> requestLineIds, DateTime eventAt, CancellationToken ct)
    {
        var requestIds = await db.MaterialRequestLines.Where(item => requestLineIds.Contains(item.Id))
            .Select(item => item.MaterialRequestId).Distinct().ToListAsync(ct);
        foreach (var request in await db.MaterialRequests.Include(item => item.Lines)
            .Where(item => requestIds.Contains(item.Id)).ToListAsync(ct))
        {
            var received = new List<decimal>();
            foreach (var line in request.Lines) received.Add(await NetReceivedAsync(line.Id, null, ct));
            if (received.Count > 0 && received.Zip(request.Lines, (actual, line) => actual >= line.RequestedQuantity).All(value => value))
            {
                request.Status = MaterialRequestStatus.Fulfilled;
                request.FulfilledAt = eventAt;
            }
            else if (received.Any(value => value > 0))
            {
                request.Status = MaterialRequestStatus.PartiallyFulfilled;
                request.FulfilledAt = null;
            }
            else
            {
                request.Status = MaterialRequestStatus.Approved;
                request.FulfilledAt = null;
            }
            request.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<decimal> NetReceivedAsync(int requestLineId, int? excludeReceiptId, CancellationToken ct)
    {
        var rows = await db.WarehouseReceiptLines.AsNoTracking().Where(line =>
            line.MaterialRequestLineId == requestLineId && line.WarehouseReceiptId != excludeReceiptId &&
            (line.WarehouseReceipt.Status == WarehouseLedgerStatus.Posted || line.WarehouseReceipt.Status == WarehouseLedgerStatus.Reversed))
            .Select(line => new { line.ReceivedQuantity, line.WarehouseReceipt.ReversalOfReceiptId }).ToListAsync(ct);
        return rows.Sum(item => item.ReversalOfReceiptId.HasValue ? -item.ReceivedQuantity : item.ReceivedQuantity);
    }

    private async Task ValidateIssueLinesAsync(int projectId, IReadOnlyCollection<WarehouseIssueLineRequest> lines, CancellationToken ct)
    {
        var ids = lines.Select(item => item.ProjectBoqLineId).ToList();
        if (ids.Count != ids.Distinct().Count())
            throw new ProcurementOperationException("Mỗi dòng BOQ chỉ được xuất hiện một lần trên phiếu xuất.");
        var count = await db.ProjectBoqLines.CountAsync(item => ids.Contains(item.Id) &&
            item.ProjectBoqRevision.OperationalProjectId == projectId && item.ProjectBoqRevision.Status == ProjectBoqRevisionStatus.Approved, ct);
        if (count != ids.Count) throw new ProcurementOperationException("Dòng xuất kho phải tham chiếu BOQ đã được duyệt của dự án.");
    }

    private async Task<Dictionary<string, decimal>> StockByItemCodeAsync(int projectId, CancellationToken ct)
    {
        var receipts = await db.WarehouseReceiptLines.AsNoTracking().Where(line =>
            line.WarehouseReceipt.OperationalProjectId == projectId &&
            (line.WarehouseReceipt.Status == WarehouseLedgerStatus.Posted || line.WarehouseReceipt.Status == WarehouseLedgerStatus.Reversed))
            .Select(line => new { line.MaterialRequestLine.ProjectBoqLine.ItemCode, line.ReceivedQuantity, line.WarehouseReceipt.ReversalOfReceiptId })
            .ToListAsync(ct);
        var issues = await db.WarehouseIssueLines.AsNoTracking().Where(line =>
            line.WarehouseIssue.OperationalProjectId == projectId &&
            (line.WarehouseIssue.Status == WarehouseLedgerStatus.Posted || line.WarehouseIssue.Status == WarehouseLedgerStatus.Reversed))
            .Select(line => new { line.ProjectBoqLine.ItemCode, line.IssuedQuantity, line.WarehouseIssue.ReversalOfIssueId })
            .ToListAsync(ct);
        var stock = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in receipts)
            stock[line.ItemCode] = stock.GetValueOrDefault(line.ItemCode) + (line.ReversalOfReceiptId.HasValue ? -line.ReceivedQuantity : line.ReceivedQuantity);
        foreach (var line in issues)
            stock[line.ItemCode] = stock.GetValueOrDefault(line.ItemCode) + (line.ReversalOfIssueId.HasValue ? line.IssuedQuantity : -line.IssuedQuantity);
        return stock;
    }

    private async Task<(int VendorId, int ProcurementOwnerUserId)> ValidateRatingAsync(
        int projectId, VendorRatingUpsertRequest request, int? excludeId, CancellationToken ct)
    {
        var contract = await db.Contracts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.ContractId, ct);
        if (contract is null || contract.OperationalProjectId != projectId || contract.Direction != ContractDirection.Downstream ||
            contract.VendorId is null || contract.Status != ContractStatus.Completed)
            throw new ProcurementOperationException("Chỉ hợp đồng đối tác đầu ra đã hoàn thành của dự án mới được đánh giá.");
        var owners = await db.ContractLines.AsNoTracking().Where(item => item.ContractId == contract.Id)
            .Select(item => item.ProcurementOwnerUserId).Distinct().ToListAsync(ct);
        if (owners.Count != 1)
            throw new ProcurementOperationException("Hợp đồng phải có một chủ sở hữu mua hàng thống nhất trước khi đánh giá.");
        return (contract.VendorId.Value, owners[0]);
    }

    private static void ApplyRating(VendorRating entity, VendorRatingUpsertRequest request)
    {
        entity.QualityScore = request.QualityScore;
        entity.ScheduleScore = request.ScheduleScore;
        entity.CostScore = request.CostScore;
        entity.HseScore = request.HseScore;
        entity.OverallScore = Math.Round((request.QualityScore + request.ScheduleScore + request.CostScore + request.HseScore) / 4m, 2, MidpointRounding.AwayFromZero);
        entity.Comments = TrimOrNull(request.Comments);
    }

    private async Task EnsureProjectAsync(int projectId, bool mutable, CancellationToken ct)
    {
        var status = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => (OperationalProjectStatus?)item.Status).SingleOrDefaultAsync(ct);
        if (!status.HasValue) throw new ProcurementOperationException("Dự án vận hành không tồn tại.");
        if (mutable && status is OperationalProjectStatus.Completed or OperationalProjectStatus.Cancelled)
            throw new ProcurementOperationException("Không thể thay đổi dữ liệu mua sắm của dự án đã hoàn thành hoặc đã hủy.");
    }

    private async Task ValidateProjectUserAsync(int projectId, int userId, string? roleCode, string field, CancellationToken ct)
    {
        var member = await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.IsActive &&
            (db.OperationalProjects.Any(project => project.Id == projectId && project.ProjectManagerUserId == userId) ||
             db.OperationalProjectMembers.Any(item => item.OperationalProjectId == projectId && item.UserId == userId && item.EndedAt == null)), ct);
        if (!member) throw new ProcurementOperationException($"{field} phải là thành viên đang hoạt động của dự án.");
        if (roleCode is not null) await ValidateActiveRoleAsync(userId, roleCode, field, ct);
    }

    private async Task ValidateActiveRoleAsync(int userId, string roleCode, string field, CancellationToken ct)
    {
        var actual = await db.Users.AsNoTracking().Where(item => item.Id == userId && item.IsActive)
            .Select(item => item.RoleEntity != null ? item.RoleEntity.Code : item.Role.ToString()).SingleOrDefaultAsync(ct);
        if (!string.Equals(actual, roleCode, StringComparison.OrdinalIgnoreCase))
            throw new ProcurementOperationException($"{field} phải có vai trò {roleCode}.");
    }

    private async Task<int?> CurrentApprovedRevisionIdAsync(int projectId, CancellationToken ct) =>
        await db.ProjectBoqRevisions.AsNoTracking().Where(item => item.OperationalProjectId == projectId &&
                item.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(item => item.ApprovedAt).ThenByDescending(item => item.RevisionNumber)
            .Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);

    private async Task<string> AllocateCodeAsync<TEntity>(int projectId, string prefix, CancellationToken ct) where TEntity : class
    {
        List<string> codes = typeof(TEntity) == typeof(MaterialRequest)
            ? await db.MaterialRequests.Where(item => item.OperationalProjectId == projectId).Select(item => item.Code).ToListAsync(ct)
            : typeof(TEntity) == typeof(WarehouseReceipt)
                ? await db.WarehouseReceipts.Where(item => item.OperationalProjectId == projectId).Select(item => item.Code).ToListAsync(ct)
                : await db.WarehouseIssues.Where(item => item.OperationalProjectId == projectId).Select(item => item.Code).ToListAsync(ct);
        var marker = prefix + "-";
        var sequence = codes.Select(code => code.StartsWith(marker, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[marker.Length..], out var value) ? value : 0).DefaultIfEmpty(0).Max() + 1;
        return $"{prefix}-{sequence:D4}";
    }

    private async Task<IDbContextTransaction?> BeginSerializableAsync(CancellationToken ct) =>
        db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;

    private IQueryable<ProjectBoqRevision> BoqQuery() => db.ProjectBoqRevisions.AsNoTracking()
        .Include(item => item.PreparedBy).Include(item => item.Lines.OrderBy(line => line.SortOrder));

    private IQueryable<ProjectBoqRevision> ApplyBoqListFilters(
        int projectId,
        ProjectBoqRevisionListQuery query)
    {
        var result = db.ProjectBoqRevisions.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId);
        if (query.Status.HasValue) result = result.Where(item => item.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            result = result.Where(item =>
                item.RevisionNumber.ToString().Contains(term) ||
                item.PreparedBy != null && item.PreparedBy.FullName.Contains(term) ||
                item.Lines.Any(line => line.ItemCode.Contains(term) || line.Description.Contains(term)));
        }
        return result;
    }

    private static IOrderedQueryable<ProjectBoqRevision> ApplyBoqListSort(
        IQueryable<ProjectBoqRevision> query,
        ProjectBoqRevisionListQuery parameters)
    {
        var descending = string.Equals(parameters.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return parameters.SortBy switch
        {
            "status" => descending ? query.OrderByDescending(item => item.Status).ThenByDescending(item => item.Id) : query.OrderBy(item => item.Status).ThenBy(item => item.Id),
            "total" => descending ? query.OrderByDescending(item => item.CostTotal).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CostTotal).ThenBy(item => item.Id),
            "preparedBy" => descending ? query.OrderByDescending(item => item.PreparedBy.FullName).ThenByDescending(item => item.Id) : query.OrderBy(item => item.PreparedBy.FullName).ThenBy(item => item.Id),
            "createdAt" => descending ? query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id),
            "updatedAt" => descending ? query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id) : query.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id),
            _ => descending ? query.OrderByDescending(item => item.RevisionNumber).ThenByDescending(item => item.Id) : query.OrderBy(item => item.RevisionNumber).ThenBy(item => item.Id),
        };
    }

    private static IQueryable<ProjectBoqRevisionListItemResponse> SelectBoqListItems(
        IQueryable<ProjectBoqRevision> query) => query.Select(item => new ProjectBoqRevisionListItemResponse
        {
            Id = item.Id,
            OperationalProjectId = item.OperationalProjectId,
            RevisionNumber = item.RevisionNumber,
            Currency = item.Currency,
            Status = item.Status,
            CostTotal = item.CostTotal,
            LineCount = item.Lines.Count,
            ItemCodes = item.Lines.OrderBy(line => line.SortOrder).Select(line => line.ItemCode).ToList(),
            PreparedByUserId = item.PreparedByUserId,
            PreparedByName = item.PreparedBy.FullName,
            SubmittedAt = item.SubmittedAt,
            ApprovedAt = item.ApprovedAt,
            RejectedAt = item.RejectedAt,
            IsFinal = item.OperationalProject != null && item.OperationalProject.FinalProjectBoqRevisionId == item.Id,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        });
    private IQueryable<MaterialRequest> RequestQuery() => db.MaterialRequests.AsNoTracking()
        .Include(item => item.SiteRequester).Include(item => item.ResponsibleSiteUser).Include(item => item.AssignedProcurementUser)
        .Include(item => item.Lines.OrderBy(line => line.SortOrder)).ThenInclude(line => line.ProjectBoqLine);
    private IQueryable<ContractLine> ContractLineQuery() => db.ContractLines.AsNoTracking()
        .Include(item => item.Contract).Include(item => item.ProjectBoqLine).Include(item => item.ProcurementOwner);
    private IQueryable<WarehouseReceipt> ReceiptQuery() => db.WarehouseReceipts.AsNoTracking()
        .Include(item => item.Lines).ThenInclude(line => line.MaterialRequestLine).ThenInclude(line => line.ProjectBoqLine);
    private IQueryable<WarehouseIssue> IssueQuery() => db.WarehouseIssues.AsNoTracking()
        .Include(item => item.ResponsibleSiteUser).Include(item => item.Lines).ThenInclude(line => line.ProjectBoqLine);
    private IQueryable<VendorRating> RatingQuery() => db.VendorRatings.AsNoTracking()
        .Include(item => item.Contract).Include(item => item.Vendor).Include(item => item.ProcurementOwner);

    private async Task<ProjectBoqRevisionResponse?> GetBoqAsync(int projectId, int id, CancellationToken ct)
    {
        var finalId = await db.OperationalProjects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => item.FinalProjectBoqRevisionId).SingleAsync(ct);
        var item = await BoqQuery().SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        return item is null ? null : MapBoq(item, finalId);
    }
    private async Task<MaterialRequestResponse?> GetRequestAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await RequestQuery().SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (item is null) return null;
        var quantities = await GetMaterialRequestQuantitiesAsync([item], ct);
        return MapRequest(item, quantities);
    }
    private async Task<ContractLineResponse?> GetContractLineAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await ContractLineQuery().SingleOrDefaultAsync(item => item.Id == id && item.Contract.OperationalProjectId == projectId, ct);
        return item is null ? null : MapContractLine(item);
    }
    private async Task<WarehouseReceiptResponse?> GetReceiptAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await ReceiptQuery().SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        return item is null ? null : MapReceipt(item);
    }
    private async Task<WarehouseIssueResponse?> GetIssueAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await IssueQuery().SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        return item is null ? null : MapIssue(item);
    }
    private async Task<VendorRatingResponse?> GetRatingAsync(int projectId, int id, CancellationToken ct)
    {
        var item = await RatingQuery().SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        return item is null ? null : MapRating(item);
    }

    private static ProjectBoqRevisionResponse MapBoq(ProjectBoqRevision item, int? finalId) => new()
    {
        Id = item.Id,
        OperationalProjectId = item.OperationalProjectId,
        RevisionNumber = item.RevisionNumber,
        Currency = item.Currency,
        Status = item.Status.ToString(),
        SourceTenderEstimateRevisionId = item.SourceTenderEstimateRevisionId,
        SourceContractAppendixId = item.SourceContractAppendixId,
        CostTotal = item.CostTotal,
        PreparedByUserId = item.PreparedByUserId,
        PreparedByName = item.PreparedBy.FullName,
        SubmittedAt = item.SubmittedAt,
        ApprovedAt = item.ApprovedAt,
        RejectedAt = item.RejectedAt,
        DecisionReason = item.DecisionReason,
        IsFinal = finalId == item.Id,
        CreatedAt = item.CreatedAt,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Lines = item.Lines.Select(line => new ProjectBoqLineResponse
        {
            Id = line.Id,
            ItemCode = line.ItemCode,
            Description = line.Description,
            Unit = line.Unit,
            ApprovedQuantity = line.ApprovedQuantity,
            BudgetUnitPrice = line.BudgetUnitPrice,
            Amount = line.Amount,
        }).ToList(),
    };

    private async Task<Dictionary<int, (decimal Received, decimal Approved, decimal Remaining)>> GetMaterialRequestQuantitiesAsync(
        IReadOnlyCollection<MaterialRequest> requests,
        CancellationToken ct)
    {
        var lineIds = requests.SelectMany(item => item.Lines).Select(line => line.Id).ToList();
        var boqLineIds = requests.SelectMany(item => item.Lines).Select(line => line.ProjectBoqLineId).Distinct().ToList();
        var received = await db.WarehouseReceiptLines.AsNoTracking()
            .Where(line => lineIds.Contains(line.MaterialRequestLineId) &&
                (line.WarehouseReceipt.Status == WarehouseLedgerStatus.Posted ||
                 line.WarehouseReceipt.Status == WarehouseLedgerStatus.Reversed))
            .GroupBy(line => line.MaterialRequestLineId)
            .Select(group => new
            {
                LineId = group.Key,
                Quantity = group.Sum(line => line.WarehouseReceipt.ReversalOfReceiptId.HasValue
                    ? -line.ReceivedQuantity
                    : line.ReceivedQuantity),
            }).ToDictionaryAsync(item => item.LineId, item => item.Quantity, ct);
        var committed = await db.MaterialRequestLines.AsNoTracking()
            .Where(line => boqLineIds.Contains(line.ProjectBoqLineId) &&
                (line.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                 line.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled ||
                 line.MaterialRequest.Status == MaterialRequestStatus.Fulfilled))
            .GroupBy(line => line.ProjectBoqLineId)
            .Select(group => new { BoqLineId = group.Key, Quantity = group.Sum(line => line.RequestedQuantity) })
            .ToDictionaryAsync(item => item.BoqLineId, item => item.Quantity, ct);

        return requests.SelectMany(item => item.Lines).ToDictionary(
            line => line.Id,
            line =>
            {
                var approved = line.ProjectBoqLine.ApprovedQuantity;
                return (
                    received.GetValueOrDefault(line.Id),
                    approved,
                    Math.Max(approved - committed.GetValueOrDefault(line.ProjectBoqLineId), 0m));
            });
    }

    private static MaterialRequestResponse MapRequest(
        MaterialRequest item,
        IReadOnlyDictionary<int, (decimal Received, decimal Approved, decimal Remaining)> quantities) => new()
        {
            Id = item.Id,
            OperationalProjectId = item.OperationalProjectId,
            Code = item.Code,
            Status = item.Status.ToString(),
            SiteRequesterUserId = item.SiteRequesterUserId,
            SiteRequesterName = item.SiteRequester.FullName,
            ResponsibleSiteUserId = item.ResponsibleSiteUserId,
            ResponsibleSiteUserName = item.ResponsibleSiteUser.FullName,
            AssignedProcurementUserId = item.AssignedProcurementUserId,
            AssignedProcurementUserName = item.AssignedProcurementUser.FullName,
            RequiredAt = item.RequiredAt,
            Note = item.Note,
            SubmittedAt = item.SubmittedAt,
            ApprovedAt = item.ApprovedAt,
            FulfilledAt = item.FulfilledAt,
            DecisionReason = item.DecisionReason,
            RowVersion = CrmConcurrency.Encode(item.RowVersion),
            Lines = item.Lines.Select(line => new MaterialRequestLineResponse
            {
                Id = line.Id,
                ProjectBoqLineId = line.ProjectBoqLineId,
                ItemCode = line.ProjectBoqLine.ItemCode,
                Description = line.ProjectBoqLine.Description,
                Unit = line.ProjectBoqLine.Unit,
                RequestedQuantity = line.RequestedQuantity,
                ReceivedQuantity = quantities.GetValueOrDefault(line.Id).Received,
                BoqApprovedQuantity = quantities.GetValueOrDefault(line.Id).Approved,
                BoqRemainingQuantity = quantities.GetValueOrDefault(line.Id).Remaining,
            }).ToList(),
        };

    private static ContractLineResponse MapContractLine(ContractLine item) => new()
    {
        Id = item.Id,
        ContractId = item.ContractId,
        ContractNumber = item.Contract.ContractNumber,
        ProjectBoqLineId = item.ProjectBoqLineId,
        ItemCode = item.ProjectBoqLine.ItemCode,
        ProcurementOwnerUserId = item.ProcurementOwnerUserId,
        ProcurementOwnerName = item.ProcurementOwner.FullName,
        Quantity = item.Quantity,
        BudgetUnitPrice = item.ProjectBoqLine.BudgetUnitPrice,
        NegotiatedUnitPrice = item.NegotiatedUnitPrice,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
    };

    private static WarehouseReceiptResponse MapReceipt(WarehouseReceipt item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Status = item.Status.ToString(),
        ReversalOfReceiptId = item.ReversalOfReceiptId,
        InspectedAt = item.InspectedAt,
        PostedAt = item.PostedAt,
        ReversalReason = item.ReversalReason,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Lines = item.Lines.Select(line => new WarehouseReceiptLineResponse
        {
            Id = line.Id,
            MaterialRequestLineId = line.MaterialRequestLineId,
            ContractLineId = line.ContractLineId,
            ItemCode = line.MaterialRequestLine.ProjectBoqLine.ItemCode,
            ReceivedQuantity = line.ReceivedQuantity,
        }).ToList(),
    };

    private static WarehouseIssueResponse MapIssue(WarehouseIssue item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Status = item.Status.ToString(),
        ReversalOfIssueId = item.ReversalOfIssueId,
        ResponsibleSiteUserId = item.ResponsibleSiteUserId,
        ResponsibleSiteUserName = item.ResponsibleSiteUser.FullName,
        IssuedAt = item.IssuedAt,
        PostedAt = item.PostedAt,
        WorkItemCode = item.WorkItemCode,
        ReversalReason = item.ReversalReason,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Lines = item.Lines.Select(line => new WarehouseIssueLineResponse
        {
            Id = line.Id,
            ProjectBoqLineId = line.ProjectBoqLineId,
            ItemCode = line.ProjectBoqLine.ItemCode,
            IssuedQuantity = line.IssuedQuantity,
        }).ToList(),
    };

    private static VendorRatingResponse MapRating(VendorRating item) => new()
    {
        Id = item.Id,
        OperationalProjectId = item.OperationalProjectId,
        ContractId = item.ContractId,
        ContractNumber = item.Contract.ContractNumber,
        VendorId = item.VendorId,
        VendorName = item.Vendor.CompanyName,
        VersionNumber = item.VersionNumber,
        Status = item.Status.ToString(),
        ProcurementOwnerUserId = item.ProcurementOwnerUserId,
        ProcurementOwnerName = item.ProcurementOwner.FullName,
        QualityScore = item.QualityScore,
        ScheduleScore = item.ScheduleScore,
        CostScore = item.CostScore,
        HseScore = item.HseScore,
        OverallScore = item.OverallScore,
        Comments = item.Comments,
        PreparedByUserId = item.PreparedByUserId,
        SubmittedAt = item.SubmittedAt,
        ApprovedAt = item.ApprovedAt,
        DecisionReason = item.DecisionReason,
        SupersedesVendorRatingId = item.SupersedesVendorRatingId,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
    };

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}