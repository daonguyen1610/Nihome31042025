using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Concept option service — see <see cref="IConceptOptionService"/>.
/// </summary>
public class ConceptOptionService(
    AppDbContext db,
    ILogger<ConceptOptionService> logger) : IConceptOptionService
{
    private const int MaxPageSize = 100;

    public async Task<ConceptOptionListResponse> ListAsync(ConceptOptionListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.ConceptOptions
            .AsNoTracking()
            .Include(c => c.DesignProject)
            .Include(c => c.Owner)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(c => c.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<ConceptOptionStatus>(s, true, out var v) ? (ConceptOptionStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(c => statuses.Contains(c.Status));
        }

        var total = await q.CountAsync(ct);

        // Finalized surfaces first, then in-progress rows by newest update.
        var rows = await q
            .OrderBy(c => c.Status == ConceptOptionStatus.Finalized ? 0 : 1)
            .ThenByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new ConceptOptionListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(Map).ToList(),
        };
    }

    public async Task<ConceptOptionResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ConceptOptions
            .AsNoTracking()
            .Include(c => c.DesignProject)
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : Map(entity);
    }

    public async Task<ConceptOptionResponse> CreateAsync(CreateConceptOptionRequest request, int callerUserId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ConceptOptionOperationException("Tên phương án là bắt buộc.");
        }

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new ConceptOptionOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        // Guard: once the parent has moved past Concept the option list is
        // frozen — new options can't be created retroactively.
        if (project.CurrentStage != DesignProjectStage.Concept)
        {
            throw new ConceptOptionOperationException(
                "Không thể tạo phương án Concept khi dự án đã chuyển sang giai đoạn khác.");
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ConceptOptionOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        var entity = new ConceptOption
        {
            DesignProjectId = request.DesignProjectId,
            Name = name,
            Description = TrimOrNull(request.Description),
            InternalNote = TrimOrNull(request.InternalNote),
            OwnerUserId = request.OwnerUserId,
            PresentedAt = null,
            Status = ConceptOptionStatus.Drafting,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        if (request.PresentedAt.HasValue)
            throw new ConceptOptionOperationException("Ngày trình khách do sự kiện trình hồ sơ ghi nhận, không nhập tay.");
        db.ConceptOptions.Add(entity);
        db.ConceptRevisions.Add(new ConceptRevision
        {
            ConceptOption = entity,
            Number = 0,
            CreatedByUserId = callerUserId,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("ConceptOption {Id} created for project {ProjectId} by user {UserId}",
            entity.Id, request.DesignProjectId, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<ConceptOptionResponse?> UpdateAsync(int id, UpdateConceptOptionRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.ConceptOptions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ConceptOptionOperationException("Tên phương án là bắt buộc.");
        }

        // Locked once the option (or the parent project) is past the Concept
        // window — the FE hides the edit button but re-check server-side.
        if (entity.Status is ConceptOptionStatus.Finalized or ConceptOptionStatus.Discarded)
        {
            throw new ConceptOptionOperationException(
                "Phương án đã chốt / loại bỏ không thể chỉnh sửa.");
        }
        if (request.PresentedAt.HasValue || await db.ConceptRevisions.AnyAsync(item =>
            item.ConceptOptionId == id && item.FrozenAt != null, ct))
            throw new ConceptOptionOperationException("Hồ sơ đã trình được giữ bất biến; hãy tạo revision mới.");

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ConceptOptionOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        entity.Name = name;
        entity.Description = TrimOrNull(request.Description);
        entity.InternalNote = TrimOrNull(request.InternalNote);
        entity.OwnerUserId = request.OwnerUserId;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ConceptOptions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status != ConceptOptionStatus.Discarded)
            throw new ConceptOptionOperationException(
                "Chỉ phương án Concept đã bị loại mới được xóa vĩnh viễn.");

        db.ConceptOptions.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("ConceptOption {Id} deleted", id);
        return true;
    }

    public async Task<ConceptOptionResponse?> TransitionStatusAsync(int id,
        TransitionConceptOptionStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ConceptOptionStatus>(request.Status, true, out var next))
        {
            throw new ConceptOptionOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }

        var entity = await db.ConceptOptions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, next);

        var now = DateTime.UtcNow;

        if (next == ConceptOptionStatus.Finalized)
        {
            var latestRevisionId = await db.ConceptRevisions.AsNoTracking()
                .Where(item => item.ConceptOptionId == id)
                .OrderByDescending(item => item.Number)
                .Select(item => (int?)item.Id).FirstOrDefaultAsync(ct);
            if (!latestRevisionId.HasValue || await db.ConceptFeedback.AsNoTracking().AnyAsync(item =>
                item.ConceptRevisionId == latestRevisionId.Value && item.Response == null, ct))
                throw new ConceptOptionOperationException("Phải phản hồi mọi ý kiến khách trước khi chốt phương án.");
            var clientApproved = await db.DesignReviews.AsNoTracking()
                .Where(review => review.DesignProjectId == entity.DesignProjectId &&
                    review.Stage == DesignReviewStage.Concept &&
                    review.ConceptRevisionId == latestRevisionId &&
                    review.ClientAcceptanceDocumentId != null &&
                    review.ClientAcceptanceSignedBy != null &&
                    review.ClientAcceptanceSignedAt != null &&
                    review.ClientAcceptanceDocument!.DeletedAt == null &&
                    review.ClientAcceptanceDocument.SyncStatus == ProjectDocumentSyncStatus.Synced &&
                    review.ClientAcceptanceDocument.SourceEntityType == nameof(ConceptOption) &&
                    review.ClientAcceptanceDocument.SourceRecordId == id &&
                    review.Documents.All(document =>
                        document.ProjectDocument.DeletedAt == null &&
                        document.ProjectDocument.SyncStatus == ProjectDocumentSyncStatus.Synced &&
                        document.ProjectDocument.Sha256 == document.Sha256Snapshot))
                .OrderByDescending(review => review.RevisionNumber)
                .Select(review => review.Status == DesignReviewStatus.ClientApproved)
                .FirstOrDefaultAsync(ct);
            if (!clientApproved)
                throw new ConceptOptionOperationException(
                    "Phải ghi nhận khách hàng duyệt hồ sơ Concept mới được chốt phương án.");

            // Guard: at most one Finalized per project.
            var alreadyFinal = await db.ConceptOptions.AsNoTracking()
                .AnyAsync(c => c.DesignProjectId == entity.DesignProjectId
                            && c.Id != entity.Id
                            && c.Status == ConceptOptionStatus.Finalized, ct);
            if (alreadyFinal)
            {
                throw new ConceptOptionOperationException(
                    "Dự án đã có phương án Concept được chốt.");
            }

            entity.Status = ConceptOptionStatus.Finalized;
            entity.UpdatedAt = now;
            entity.UpdatedByUserId = callerUserId;

            // Discard all sibling options that are still active. Already-
            // Discarded rows stay as-is (idempotency).
            var siblings = await db.ConceptOptions
                .Where(c => c.DesignProjectId == entity.DesignProjectId
                         && c.Id != entity.Id
                         && c.Status != ConceptOptionStatus.Discarded
                         && c.Status != ConceptOptionStatus.Finalized)
                .ToListAsync(ct);
            foreach (var sib in siblings)
            {
                sib.Status = ConceptOptionStatus.Discarded;
                sib.UpdatedAt = now;
                sib.UpdatedByUserId = callerUserId;
            }

            // Unlock the parent's Basic Design stage.
            var project = await db.DesignProjects.FirstAsync(dp => dp.Id == entity.DesignProjectId, ct);
            if (project.CurrentStage == DesignProjectStage.Concept)
            {
                project.CurrentStage = DesignProjectStage.BasicDesign;
                project.UpdatedAt = now;
                project.UpdatedByUserId = callerUserId;
            }
        }
        else
        {
            if (next != ConceptOptionStatus.Discarded && next != entity.Status)
                throw new ConceptOptionOperationException("Trạng thái trình duyệt do hồ sơ và revision Concept ghi nhận.");
            entity.Status = next;
            entity.UpdatedAt = now;
            entity.UpdatedByUserId = callerUserId;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (next == ConceptOptionStatus.Finalized)
        {
            throw new ConceptOptionOperationException("Dự án đã có phương án Concept được chốt.");
        }
        logger.LogInformation(
            "ConceptOption {Id} transitioned to {Status} by user {UserId}", id, next, callerUserId);
        return await GetAsync(id, ct);
    }

    // ------------------------------ Helpers ---------------------------------

    /// <summary>
    /// State machine for a Concept option:
    /// <list type="bullet">
    ///   <item>Drafting → PendingInternalReview | Discarded</item>
    ///   <item>PendingInternalReview → Drafting | PresentedToClient | Discarded</item>
    ///   <item>PresentedToClient → ClientRequestedChanges | Finalized | Discarded</item>
    ///   <item>ClientRequestedChanges → Drafting | Discarded</item>
    /// </list>
    /// Finalized + Discarded are terminal.
    /// </summary>
    private static void EnsureTransitionAllowed(ConceptOptionStatus from, ConceptOptionStatus to)
    {
        if (from == to) return; // idempotent no-op
        bool ok = (from, to) switch
        {
            (ConceptOptionStatus.Drafting, ConceptOptionStatus.PendingInternalReview) => true,
            (ConceptOptionStatus.Drafting, ConceptOptionStatus.Discarded) => true,
            (ConceptOptionStatus.PendingInternalReview, ConceptOptionStatus.Drafting) => true,
            (ConceptOptionStatus.PendingInternalReview, ConceptOptionStatus.PresentedToClient) => true,
            (ConceptOptionStatus.PendingInternalReview, ConceptOptionStatus.Discarded) => true,
            (ConceptOptionStatus.PresentedToClient, ConceptOptionStatus.ClientRequestedChanges) => true,
            (ConceptOptionStatus.PresentedToClient, ConceptOptionStatus.Finalized) => true,
            (ConceptOptionStatus.PresentedToClient, ConceptOptionStatus.Discarded) => true,
            (ConceptOptionStatus.ClientRequestedChanges, ConceptOptionStatus.Drafting) => true,
            (ConceptOptionStatus.ClientRequestedChanges, ConceptOptionStatus.Discarded) => true,
            _ => false,
        };
        if (!ok)
        {
            throw new ConceptOptionOperationException(
                $"Không thể chuyển từ '{from}' sang '{to}'.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ConceptOptionResponse Map(ConceptOption c) => new()
    {
        Id = c.Id,
        DesignProjectId = c.DesignProjectId,
        DesignProjectCode = c.DesignProject?.ProjectCode,
        Name = c.Name,
        Description = c.Description,
        InternalNote = c.InternalNote,
        OwnerUserId = c.OwnerUserId,
        OwnerName = c.Owner?.FullName,
        PresentedAt = c.PresentedAt,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
