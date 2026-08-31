using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Workflow rules (NIH-104):
/// <list type="bullet">
///   <item>Anyone with <c>crm.contracts.manage</c> may Create/Update the
///     draft, Submit it, or Delete a VO in any workflow status.</item>
///   <item>Only <c>view.all</c> callers (Sales Manager / Legal / BOD /
///     Admin) may Approve / Reject a Submitted VO. The controller wires
///     this by only exposing the approve/reject endpoints when the
///     caller has the manager permission.</item>
///   <item>An Approved VO becomes read-only. Reject to unlock it.</item>
/// </list></summary>
public class ContractAppendixService(
    AppDbContext db,
    ILogger<ContractAppendixService> logger,
    IProjectDocumentStagingService projectDocuments,
    IWebHostEnvironment? env = null)
    : IContractAppendixService
{
    private readonly string _contentRoot = env?.ContentRootPath ?? Directory.GetCurrentDirectory();
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

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await AcquireNumberAllocationLockAsync(contractId, ct);

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
        try
        {
            await db.SaveChangesAsync(ct);
            if (contract.OperationalProjectId.HasValue && !string.IsNullOrWhiteSpace(entity.FilePath))
            {
                await projectDocuments.StageExistingManagedFileAsync(
                    contract.OperationalProjectId.Value, ProjectDocumentCategory.FinanceContracts,
                    ProjectDocumentSourceModule.Crm, nameof(ContractAppendix), "file", entity.Id,
                    entity.FilePath, entity.OriginalFileName ?? Path.GetFileName(entity.FilePath),
                    contract.CustomerId, contract.Id, callerUserId, ct);
                await db.SaveChangesAsync(ct);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            else if (entity.Id > 0)
            {
                db.ContractAppendices.Remove(entity);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            DeleteManagedFile(entity.FilePath);
            throw;
        }
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

        var previousFilePath = vo.FilePath;
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

        try
        {
            if (contract.OperationalProjectId.HasValue &&
                !string.Equals(previousFilePath, vo.FilePath, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(previousFilePath))
                    await projectDocuments.StageExistingManagedFileDeleteAsync(
                        contract.OperationalProjectId.Value, ProjectDocumentSourceModule.Crm,
                        nameof(ContractAppendix), "file", vo.Id, previousFilePath, callerUserId, ct);
                if (!string.IsNullOrWhiteSpace(vo.FilePath))
                    await projectDocuments.StageExistingManagedFileAsync(
                        contract.OperationalProjectId.Value, ProjectDocumentCategory.FinanceContracts,
                        ProjectDocumentSourceModule.Crm, nameof(ContractAppendix), "file", vo.Id,
                        vo.FilePath, vo.OriginalFileName ?? Path.GetFileName(vo.FilePath),
                        contract.CustomerId, contract.Id, callerUserId, ct);
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (!string.Equals(previousFilePath, vo.FilePath, StringComparison.Ordinal))
                DeleteManagedFile(vo.FilePath);
            throw;
        }
        if (!string.Equals(previousFilePath, vo.FilePath, StringComparison.Ordinal))
            DeleteManagedFile(previousFilePath);
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

        var filePath = vo.FilePath;
        if (contract.OperationalProjectId.HasValue && !string.IsNullOrWhiteSpace(filePath))
        {
            await projectDocuments.StageExistingManagedFileDeleteAsync(
                contract.OperationalProjectId.Value, ProjectDocumentSourceModule.Crm,
                nameof(ContractAppendix), "file", vo.Id, filePath, callerUserId, ct);
        }
        db.ContractAppendices.Remove(vo);
        await db.SaveChangesAsync(ct);
        DeleteManagedFile(filePath);
        return true;
    }

    // -------- helpers --------

    private void DeleteManagedFile(string? filePath)
    {
        const string expectedPrefix = "/files/contracts/";
        if (string.IsNullOrWhiteSpace(filePath)
            || !filePath.StartsWith(expectedPrefix, StringComparison.Ordinal)) return;
        var fileName = Path.GetFileName(filePath);
        if (!string.Equals(filePath, $"{expectedPrefix}{fileName}", StringComparison.Ordinal)) return;
        var fullPath = Path.Combine(_contentRoot, "wwwroot", "files", "contracts", fileName);
        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ValidatePayload(UpsertContractAppendixRequest req)
    {
        if (req.ValueDelta == 0m)
        {
            throw new ContractValidationException("Giá trị điều chỉnh (ValueDelta) phải khác 0.");
        }
    }

    private async Task AcquireNumberAllocationLockAsync(int contractId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;

        var resource = $"contracts:appendix-number:{contractId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {resource},
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            IF @result < 0
                THROW 51000, 'Unable to allocate a contract appendix number.', 1;
            """, ct);
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
