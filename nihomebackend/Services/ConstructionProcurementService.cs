using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class ConstructionProcurementService(AppDbContext db)
{
    public Task<List<ConstructionSupplyDocumentResponse>> DocumentsAsync(int projectId, CancellationToken ct) =>
        db.ProjectDocuments.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.DeletedAt == null &&
            x.DesiredOperation != ProjectDocumentDesiredOperation.Delete && x.SyncStatus != ProjectDocumentSyncStatus.Deleted)
            .OrderBy(x => x.OriginalFileName)
            .Select(x => new ConstructionSupplyDocumentResponse(x.Id, x.OriginalFileName)).ToListAsync(ct);

    public async Task<NihomeBackend.Models.DTOs.Responses.ContractResponse> CreateContractAsync(
        int projectId, CreateConstructionProcurementContractRequest request, int actor,
        IContractService contracts, CancellationToken ct)
    {
        if (!await Requests(projectId).AnyAsync(ct))
            throw new ConstructionOperationException("A CM-approved equipment or labour request is required before drafting a procurement contract.");
        var customerId = await db.OperationalProjects.Where(x => x.Id == projectId).Select(x => x.CustomerId).SingleAsync(ct);
        // Reuse the canonical contract lifecycle and validation; never grant upstream management.
        return await contracts.CreateAsync(new UpsertContractRequest
        {
            OperationalProjectId = projectId, CustomerId = customerId, Direction = ContractDirection.Downstream,
            Status = ContractStatus.Draft, Type = request.Type, VendorId = request.VendorId,
            Value = request.Value!.Value, ScopeOfWork = request.ScopeOfWork?.Trim(), OwnerUserId = actor,
        }, actor, canReassignOwner: true, ct);
    }
    private IQueryable<ConstructionResourceRequest> Requests(int projectId) => db.ConstructionResourceRequests
        .Include(x => x.ProcurementContract).Include(x => x.HandoffEvents)
        .Where(x => x.OperationalProjectId == projectId && x.Status == ConstructionReviewStatus.CmApproved &&
            (x.Type == ConstructionResourceType.Equipment || x.Type == ConstructionResourceType.Labour));

    public async Task<List<ConstructionProcurementResponse>> ListAsync(
        int projectId, ConstructionResourceType? type, string? state, CancellationToken ct)
    {
        if (type.HasValue && type != ConstructionResourceType.Equipment && type != ConstructionResourceType.Labour)
            throw new ConstructionOperationException("Type must be Equipment or Labour.");
        if (state is not null and not "AwaitingContract" and not "ContractLinked" and not "Supplied")
            throw new ConstructionOperationException("State must be AwaitingContract, ContractLinked or Supplied.");
        var query = Requests(projectId).AsNoTracking();
        if (type.HasValue) query = query.Where(x => x.Type == type);
        if (state == "AwaitingContract") query = query.Where(x => x.ProcurementContractId == null);
        if (state == "ContractLinked") query = query.Where(x => x.ProcurementContractId != null && x.SuppliedAt == null);
        if (state == "Supplied") query = query.Where(x => x.SuppliedAt != null);
        return (await query.OrderBy(x => x.NeedByDate).ThenBy(x => x.Id).ToListAsync(ct)).Select(Map).ToList();
    }

    public Task<List<ConstructionProcurementContractResponse>> ContractsAsync(int projectId, CancellationToken ct) =>
        db.Contracts.AsNoTracking().Where(x => x.OperationalProjectId == projectId &&
            x.Direction == ContractDirection.Downstream && x.VendorId != null &&
            (x.Type == ContractType.Supply || x.Type == ContractType.Subcontract) &&
            (x.Status == ContractStatus.Draft || x.Status == ContractStatus.Signed || x.Status == ContractStatus.InProgress))
            .OrderBy(x => x.ContractNumber)
            .Select(x => new ConstructionProcurementContractResponse(x.Id, x.ContractNumber, x.Status)).ToListAsync(ct);

    public async Task<ConstructionProcurementResponse?> LinkAsync(
        int projectId, int id, LinkConstructionContractRequest request, int actor, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = await Requests(projectId).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        CrmConcurrency.EnsureMatches(item.RowVersion, request.RowVersion);
        CrmConcurrency.Apply(db, item, request.RowVersion!);
        if (item.SuppliedAt.HasValue) throw new ConstructionOperationException("A supplied request cannot change contracts.");
        var contract = await db.Contracts.SingleOrDefaultAsync(x => x.Id == request.ContractId, ct);
        if (!ValidContract(contract, projectId, allowDraft: true))
            throw new ConstructionOperationException("Select a valid Downstream Supply/Subcontract contract for this project.");
        if (item.ProcurementContractId != request.ContractId)
        {
            item.HandoffEvents.Add(new ConstructionResourceHandoffEvent
            {
                PreviousContractId = item.ProcurementContractId, ContractId = request.ContractId, ActorUserId = actor,
            });
            item.ProcurementContract = contract;
            item.ProcurementContractId = request.ContractId;
            item.UpdatedAt = DateTime.UtcNow;
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    public async Task<ConstructionProcurementResponse?> ConfirmAsync(
        int projectId, int id, ConfirmConstructionSupplyRequest request, int actor, CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
        var item = await Requests(projectId).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        CrmConcurrency.EnsureMatches(item.RowVersion, request.RowVersion);
        CrmConcurrency.Apply(db, item, request.RowVersion!);
        if (item.SuppliedAt.HasValue) throw new ConstructionOperationException("Supply has already been confirmed.");
        if (!ValidContract(item.ProcurementContract, projectId, allowDraft: false))
            throw new ConstructionOperationException("Supply requires a signed or active Downstream contract for this project.");
        if (!await db.ProjectDocuments.AsNoTracking().AnyAsync(x => x.Id == request.DocumentId &&
            x.OperationalProjectId == projectId && x.DeletedAt == null &&
            x.DesiredOperation != ProjectDocumentDesiredOperation.Delete && x.SyncStatus != ProjectDocumentSyncStatus.Deleted, ct))
            throw new ConstructionOperationException("Supply evidence must be an active document in this project.");
        item.SuppliedAt = DateTime.UtcNow;
        item.SuppliedByUserId = actor;
        item.SupplyDocumentId = request.DocumentId;
        item.UpdatedAt = item.SuppliedAt.Value;
        item.HandoffEvents.Add(new ConstructionResourceHandoffEvent
        {
            PreviousContractId = item.ProcurementContractId, ContractId = item.ProcurementContractId!.Value,
            SupplyDocumentId = request.DocumentId, ActorUserId = actor,
        });
        // A single SaveChanges atomically persists confirmation and its history.
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Map(item);
    }

    private static bool ValidContract(Contract? contract, int projectId, bool allowDraft) =>
        contract is not null && contract.OperationalProjectId == projectId && contract.VendorId.HasValue &&
        contract.Direction == ContractDirection.Downstream &&
        (contract.Type == ContractType.Supply || contract.Type == ContractType.Subcontract) &&
        (contract.Status == ContractStatus.Signed || contract.Status == ContractStatus.InProgress ||
            (allowDraft && contract.Status == ContractStatus.Draft));

    private static ConstructionProcurementResponse Map(ConstructionResourceRequest x) => new(
        x.Id, x.OperationalProjectId, x.Code, x.Type, x.WbsCode, x.NeedByDate, x.Quantity, x.Unit, x.Reason,
        x.ProcurementContractId, x.ProcurementContract?.ContractNumber,
        ValidContract(x.ProcurementContract, x.OperationalProjectId, allowDraft: false), x.SuppliedAt,
        x.SuppliedByUserId, x.SupplyDocumentId, CrmConcurrency.Encode(x.RowVersion),
        x.HandoffEvents.OrderBy(e => e.Id).Select(e => new ConstructionProcurementEventResponse(
            e.Id, e.PreviousContractId, e.ContractId, e.SupplyDocumentId, e.ActorUserId, e.CreatedAt)).ToList());
}
