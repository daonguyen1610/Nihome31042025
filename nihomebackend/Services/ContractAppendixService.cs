using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Workflow rules (NIH-104):
/// <list type="bullet">
///   <item>Anyone with <c>crm.contracts.manage</c> may Create/Update the
///     draft, Submit it, or Delete a Draft/Rejected row.</item>
///   <item>Only <c>view.all</c> callers (Sales Manager / Legal / BOD /
///     Admin) may Approve / Reject a Submitted VO. The controller wires
///     this by only exposing the approve/reject endpoints when the
///     caller has the manager permission.</item>
///   <item>An Approved VO becomes read-only. Reject to unlock it.</item>
/// </list></summary>
public class ContractAppendixService(AppDbContext db, ILogger<ContractAppendixService> logger)
    : IContractAppendixService
{
    public async Task<List<ContractAppendixResponse>?> ListAsync(
        int contractId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        var rows = await db.ContractAppendices
            .AsNoTracking()
            .Where(v => v.ContractId == contractId)
            .OrderBy(v => v.VoNumber)
            .Select(v => new
            {
                Vo = v,
                SubmittedByName = v.SubmittedBy != null ? v.SubmittedBy.FullName : null,
                DecidedByName = v.DecidedBy != null ? v.DecidedBy.FullName : null,
            })
            .ToListAsync(ct);

        return rows.Select(r => Map(r.Vo, r.SubmittedByName, r.DecidedByName)).ToList();
    }

    public async Task<ContractAppendixResponse?> GetAsync(
        int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        var row = await db.ContractAppendices
            .AsNoTracking()
            .Where(v => v.Id == voId && v.ContractId == contractId)
            .Select(v => new
            {
                Vo = v,
                SubmittedByName = v.SubmittedBy != null ? v.SubmittedBy.FullName : null,
                DecidedByName = v.DecidedBy != null ? v.DecidedBy.FullName : null,
            })
            .FirstOrDefaultAsync(ct);

        return row == null ? null : Map(row.Vo, row.SubmittedByName, row.DecidedByName);
    }

    public async Task<ContractAppendixResponse?> CreateAsync(
        int contractId, UpsertContractAppendixRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        ValidatePayload(req);

        var nextNumber = 1 + (await db.ContractAppendices
            .Where(v => v.ContractId == contractId)
            .Select(v => (int?)v.VoNumber)
            .MaxAsync(ct) ?? 0);

        var entity = new ContractAppendix
        {
            ContractId = contractId,
            VoNumber = nextNumber,
            Title = req.Title.Trim(),
            Reason = req.Reason.Trim(),
            ValueDelta = req.ValueDelta,
            FilePath = string.IsNullOrWhiteSpace(req.FilePath) ? null : req.FilePath.Trim(),
            OriginalFileName = string.IsNullOrWhiteSpace(req.OriginalFileName) ? null : req.OriginalFileName.Trim(),
            FileSize = req.FileSize,
            ContentType = string.IsNullOrWhiteSpace(req.ContentType) ? null : req.ContentType.Trim(),
            Status = ContractAppendixStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.ContractAppendices.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created VO {VoNumber} on contract {ContractId}", entity.VoNumber, contractId);

        return await GetAsync(contractId, entity.Id, callerUserId, canSeeAll: true, ct);
    }

    public async Task<ContractAppendixResponse?> UpdateAsync(
        int contractId, int voId, UpsertContractAppendixRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        var vo = await db.ContractAppendices
            .FirstOrDefaultAsync(v => v.Id == voId && v.ContractId == contractId, ct);
        if (vo == null) return null;

        // Only Draft and Rejected VOs are editable — once Submitted the row
        // is frozen until a reviewer acts on it.
        if (vo.Status != ContractAppendixStatus.Draft && vo.Status != ContractAppendixStatus.Rejected)
        {
            throw new ContractValidationException(
                "Chỉ có thể chỉnh sửa VO ở trạng thái Nháp hoặc Bị từ chối.");
        }

        ValidatePayload(req);

        vo.Title = req.Title.Trim();
        vo.Reason = req.Reason.Trim();
        vo.ValueDelta = req.ValueDelta;
        vo.FilePath = string.IsNullOrWhiteSpace(req.FilePath) ? null : req.FilePath.Trim();
        vo.OriginalFileName = string.IsNullOrWhiteSpace(req.OriginalFileName) ? null : req.OriginalFileName.Trim();
        vo.FileSize = req.FileSize;
        vo.ContentType = string.IsNullOrWhiteSpace(req.ContentType) ? null : req.ContentType.Trim();
        vo.UpdatedAt = DateTime.UtcNow;
        vo.UpdatedByUserId = callerUserId;

        // Editing a rejected row wipes the previous decision so the next
        // Submit starts from a clean slate.
        if (vo.Status == ContractAppendixStatus.Rejected)
        {
            vo.Status = ContractAppendixStatus.Draft;
            vo.DecidedAt = null;
            vo.DecidedByUserId = null;
            vo.DecisionNote = null;
            vo.SubmittedAt = null;
            vo.SubmittedByUserId = null;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(contractId, voId, callerUserId, canSeeAll: true, ct);
    }

    public async Task<ContractAppendixResponse?> SubmitAsync(
        int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vo = await LoadForTransitionAsync(contractId, voId, callerUserId, canSeeAll, ct);
        if (vo == null) return null;

        if (vo.Status != ContractAppendixStatus.Draft)
        {
            throw new ContractValidationException("Chỉ VO ở trạng thái Nháp mới có thể gửi duyệt.");
        }

        vo.Status = ContractAppendixStatus.Submitted;
        vo.SubmittedAt = DateTime.UtcNow;
        vo.SubmittedByUserId = callerUserId;
        vo.UpdatedAt = DateTime.UtcNow;
        vo.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Submitted VO {Vo} on contract {Contract}", voId, contractId);

        return await GetAsync(contractId, voId, callerUserId, canSeeAll: true, ct);
    }

    public async Task<ContractAppendixResponse?> ApproveAsync(
        int contractId, int voId, string? note, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vo = await LoadForTransitionAsync(contractId, voId, callerUserId, canSeeAll, ct);
        if (vo == null) return null;

        if (vo.Status != ContractAppendixStatus.Submitted)
        {
            throw new ContractValidationException("Chỉ VO đã gửi duyệt mới có thể được phê duyệt.");
        }

        vo.Status = ContractAppendixStatus.Approved;
        vo.DecidedAt = DateTime.UtcNow;
        vo.DecidedByUserId = callerUserId;
        vo.DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        vo.UpdatedAt = DateTime.UtcNow;
        vo.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Approved VO {Vo} on contract {Contract}", voId, contractId);

        return await GetAsync(contractId, voId, callerUserId, canSeeAll: true, ct);
    }

    public async Task<ContractAppendixResponse?> RejectAsync(
        int contractId, int voId, string? note, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var vo = await LoadForTransitionAsync(contractId, voId, callerUserId, canSeeAll, ct);
        if (vo == null) return null;

        if (vo.Status != ContractAppendixStatus.Submitted)
        {
            throw new ContractValidationException("Chỉ VO đã gửi duyệt mới có thể bị từ chối.");
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ContractValidationException("Vui lòng nhập lý do từ chối.");
        }

        vo.Status = ContractAppendixStatus.Rejected;
        vo.DecidedAt = DateTime.UtcNow;
        vo.DecidedByUserId = callerUserId;
        vo.DecisionNote = note.Trim();
        vo.UpdatedAt = DateTime.UtcNow;
        vo.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Rejected VO {Vo} on contract {Contract}", voId, contractId);

        return await GetAsync(contractId, voId, callerUserId, canSeeAll: true, ct);
    }

    public async Task<bool> DeleteAsync(
        int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return false;

        var vo = await db.ContractAppendices
            .FirstOrDefaultAsync(v => v.Id == voId && v.ContractId == contractId, ct);
        if (vo == null) return false;

        // Approved VOs are locked: they've already changed the contract's
        // effective value. Reject them first if a mistake needs undoing.
        if (vo.Status == ContractAppendixStatus.Approved)
        {
            throw new ContractValidationException("Không thể xoá VO đã phê duyệt.");
        }

        db.ContractAppendices.Remove(vo);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // -------- helpers --------

    private static void ValidatePayload(UpsertContractAppendixRequest req)
    {
        if (req.ValueDelta == 0m)
        {
            throw new ContractValidationException("Giá trị điều chỉnh (ValueDelta) phải khác 0.");
        }
    }

    private async Task<Contract?> FetchContractAsync(int contractId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var c = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == contractId, ct);
        if (c == null) return null;
        if (!canSeeAll && c.OwnerUserId != callerUserId) return null;
        return c;
    }

    private async Task<ContractAppendix?> LoadForTransitionAsync(int contractId, int voId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;
        return await db.ContractAppendices
            .FirstOrDefaultAsync(v => v.Id == voId && v.ContractId == contractId, ct);
    }

    private static ContractAppendixResponse Map(
        ContractAppendix vo, string? submittedByName, string? decidedByName) => new()
        {
            Id = vo.Id,
            ContractId = vo.ContractId,
            VoNumber = vo.VoNumber,
            Title = vo.Title,
            Reason = vo.Reason,
            ValueDelta = vo.ValueDelta,
            FilePath = vo.FilePath,
            OriginalFileName = vo.OriginalFileName,
            FileSize = vo.FileSize,
            ContentType = vo.ContentType,
            Status = vo.Status,
            SubmittedAt = vo.SubmittedAt,
            SubmittedByUserId = vo.SubmittedByUserId,
            SubmittedByName = submittedByName,
            DecidedAt = vo.DecidedAt,
            DecidedByUserId = vo.DecidedByUserId,
            DecidedByName = decidedByName,
            DecisionNote = vo.DecisionNote,
            CreatedAt = vo.CreatedAt,
            UpdatedAt = vo.UpdatedAt,
        };
}
