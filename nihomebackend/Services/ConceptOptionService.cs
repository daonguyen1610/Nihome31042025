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
            PresentedAt = request.PresentedAt,
            Status = ConceptOptionStatus.Drafting,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ConceptOptions.Add(entity);
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

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new ConceptOptionOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        entity.Name = name;
        entity.Description = TrimOrNull(request.Description);
        entity.InternalNote = TrimOrNull(request.InternalNote);
        entity.OwnerUserId = request.OwnerUserId;
        entity.PresentedAt = request.PresentedAt;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.ConceptOptions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return false;

        if (entity.Status != ConceptOptionStatus.Drafting)
        {
            throw new ConceptOptionOperationException(
                "Chỉ có thể xoá phương án khi còn ở trạng thái Đang thiết kế. Hãy loại bỏ (Discard) thay vì xoá.");
        }

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
            entity.Status = next;
            entity.UpdatedAt = now;
            entity.UpdatedByUserId = callerUserId;
        }

        await db.SaveChangesAsync(ct);
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
