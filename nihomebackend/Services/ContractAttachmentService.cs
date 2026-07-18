using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Metadata store for contract attachments. Physical files are
/// written by the controller (multipart upload → wwwroot/files/contracts)
/// then registered here with the metadata.</summary>
public class ContractAttachmentService(AppDbContext db, ILogger<ContractAttachmentService> logger)
    : IContractAttachmentService
{
    public async Task<List<ContractAttachmentResponse>?> ListAsync(
        int contractId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        var rows = await db.ContractAttachments
            .AsNoTracking()
            .Where(a => a.ContractId == contractId)
            .OrderBy(a => a.Kind == ContractAttachmentKind.SignedScan ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                Att = a,
                UploaderName = a.UploadedBy != null ? a.UploadedBy.FullName : null,
            })
            .ToListAsync(ct);

        return rows.Select(r => Map(r.Att, r.UploaderName)).ToList();
    }

    public async Task<ContractAttachmentResponse?> CreateAsync(
        int contractId, CreateContractAttachmentRequest req, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return null;

        var entity = new ContractAttachment
        {
            ContractId = contractId,
            Kind = req.Kind,
            FilePath = req.FilePath.Trim(),
            OriginalFileName = req.OriginalFileName.Trim(),
            FileSize = req.FileSize,
            ContentType = string.IsNullOrWhiteSpace(req.ContentType) ? "application/octet-stream" : req.ContentType.Trim(),
            Label = string.IsNullOrWhiteSpace(req.Label) ? null : req.Label.Trim(),
            UploadedByUserId = callerUserId,
        };
        db.ContractAttachments.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Registered attachment {Id} ({Kind}) for contract {Contract}", entity.Id, entity.Kind, contractId);

        var uploader = await db.Users.AsNoTracking()
            .Where(u => u.Id == callerUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return Map(entity, uploader);
    }

    public async Task<bool> DeleteAsync(
        int contractId, int attachmentId, int callerUserId, bool canSeeAll, CancellationToken ct = default)
    {
        var contract = await FetchContractAsync(contractId, callerUserId, canSeeAll, ct);
        if (contract == null) return false;

        var entity = await db.ContractAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ContractId == contractId, ct);
        if (entity == null) return false;

        db.ContractAttachments.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted attachment {Id} for contract {Contract}", attachmentId, contractId);
        return true;
    }

    private async Task<Contract?> FetchContractAsync(int contractId, int callerUserId, bool canSeeAll, CancellationToken ct)
    {
        var c = await db.Contracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == contractId, ct);
        if (c == null) return null;
        if (!canSeeAll && c.OwnerUserId != callerUserId) return null;
        return c;
    }

    private static ContractAttachmentResponse Map(ContractAttachment a, string? uploaderName) => new()
    {
        Id = a.Id,
        ContractId = a.ContractId,
        Kind = a.Kind,
        FilePath = a.FilePath,
        OriginalFileName = a.OriginalFileName,
        FileSize = a.FileSize,
        ContentType = a.ContentType,
        Label = a.Label,
        CreatedAt = a.CreatedAt,
        UploadedByUserId = a.UploadedByUserId,
        UploadedByName = uploaderName,
    };
}
