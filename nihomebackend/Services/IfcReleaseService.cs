using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 IFC Release service (NIH-118 slice 1) — see
/// <see cref="IIfcReleaseService"/>.
/// </summary>
public class IfcReleaseService(
    AppDbContext db,
    ILogger<IfcReleaseService> logger) : IIfcReleaseService
{
    private const int MaxPageSize = 200;
    private const string DisciplineCategory = "design_discipline";
    private const string RecipientCategory = "ifc_recipient_type";

    public async Task<IfcReleaseListResponse> ListAsync(IfcReleaseListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.IfcReleases
            .AsNoTracking()
            .Include(r => r.DesignProject)
            .Include(r => r.IssuedBy)
            .Include(r => r.Items).ThenInclude(i => i.ShopDrawing)
            .Include(r => r.Recipients)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(r => r.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<IfcReleaseStatus>(s, true, out var v) ? (IfcReleaseStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(r => statuses.Contains(r.Status));
        }
        if (p.DateFrom.HasValue)
        {
            var from = p.DateFrom.Value;
            q = q.Where(r => r.ReleaseDate >= from);
        }
        if (p.DateTo.HasValue)
        {
            var to = p.DateTo.Value;
            q = q.Where(r => r.ReleaseDate <= to);
        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(r => EF.Functions.Like(r.Title, $"%{term}%")
                          || EF.Functions.Like(r.ReleaseNumber, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(r => r.ReleaseDate ?? r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var labels = await LookupLabelsAsync(rows, ct);
        var scope = db.IfcReleases.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(r => r.DesignProjectId == p.DesignProjectId.Value);
        var statusCounts = await scope
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);

        return new IfcReleaseListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, labels.DisciplineByCode, labels.RecipientByCode)).ToList(),
            StatusCounts = statusCounts,
        };
    }

    public async Task<IfcReleaseResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .AsNoTracking()
            .Include(r => r.DesignProject)
            .Include(r => r.IssuedBy)
            .Include(r => r.Items).ThenInclude(i => i.ShopDrawing)
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return null;

        var labels = await LookupLabelsAsync(new[] { entity }, ct);
        return Map(entity, labels.DisciplineByCode, labels.RecipientByCode);
    }

    public async Task<IfcReleaseResponse> CreateAsync(CreateIfcReleaseRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new IfcReleaseOperationException("Tên phiếu là bắt buộc.");
        }
        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new IfcReleaseOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }
        // Guard: only meaningful when the project has reached Shop Drawing
        // (a release requires approved shop drawings underneath).
        if (project.CurrentStage != DesignProjectStage.ShopDrawing &&
            project.CurrentStage != DesignProjectStage.Completed)
        {
            throw new IfcReleaseOperationException(
                "Chỉ tạo phiếu IFC khi dự án đang ở giai đoạn Shop Drawing.");
        }

        var releaseNumber = await AllocateReleaseNumberAsync(request.DesignProjectId, ct);
        var entity = new IfcRelease
        {
            DesignProjectId = request.DesignProjectId,
            ReleaseNumber = releaseNumber,
            Title = title,
            Note = TrimOrNull(request.Note),
            Status = IfcReleaseStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.IfcReleases.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("IfcRelease {Id} ({Number}) created for project {ProjectId} by user {UserId}",
            entity.Id, entity.ReleaseNumber, request.DesignProjectId, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<IfcReleaseResponse?> UpdateAsync(int id, UpdateIfcReleaseRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return null;
        EnsureDraft(entity, "chỉnh sửa");

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new IfcReleaseOperationException("Tên phiếu là bắt buộc.");
        }

        entity.Title = title;
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Items)
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (entity is null) return false;
        EnsureDraft(entity, "xoá");
        db.IfcReleases.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IfcReleaseResponse> AddItemsAsync(int id, AddIfcReleaseItemsRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "thêm bản vẽ");

        var distinctIds = request.ShopDrawingIds?.Distinct().ToList() ?? new List<int>();
        if (distinctIds.Count == 0)
        {
            throw new IfcReleaseOperationException("Danh sách bản vẽ bắt buộc.");
        }

        var drawings = await db.ShopDrawings
            .Where(d => distinctIds.Contains(d.Id))
            .ToListAsync(ct);
        var byId = drawings.ToDictionary(d => d.Id);

        foreach (var did in distinctIds)
        {
            if (!byId.TryGetValue(did, out var drawing))
            {
                throw new IfcReleaseOperationException($"Bản vẽ #{did} không tồn tại.");
            }
            if (drawing.DesignProjectId != entity.DesignProjectId)
            {
                throw new IfcReleaseOperationException(
                    $"Bản vẽ #{did} không thuộc dự án của phiếu.");
            }
            // Only approved or already-queued drawings can enter an IFC packet.
            if (drawing.Status != ShopDrawingStatus.Approved && drawing.Status != ShopDrawingStatus.PendingIfc)
            {
                throw new IfcReleaseOperationException(
                    $"Bản vẽ {drawing.DrawingCode} chưa ở trạng thái Đã duyệt hoặc Chờ IFC.");
            }
            if (entity.Items.Any(i => i.ShopDrawingId == did))
            {
                continue; // idempotent — silently skip duplicate adds
            }
            entity.Items.Add(new IfcReleaseItem { ShopDrawingId = did });
        }
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> RemoveItemAsync(int id, int itemId, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "gỡ bản vẽ");

        var item = entity.Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new IfcReleaseOperationException($"Dòng #{itemId} không tồn tại trong phiếu.");
        entity.Items.Remove(item);
        db.Remove(item);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> AddRecipientAsync(int id, AddIfcReleaseRecipientRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "thêm nơi nhận");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IfcReleaseOperationException("Tên nơi nhận là bắt buộc.");
        }
        var typeCode = (request.RecipientTypeCode ?? string.Empty).Trim();
        var typeExists = await db.MasterDataOptions
            .AnyAsync(m => m.Category == RecipientCategory && m.Code == typeCode && m.IsActive, ct);
        if (!typeExists)
        {
            throw new IfcReleaseOperationException($"Loại nơi nhận '{typeCode}' không hợp lệ.");
        }

        entity.Recipients.Add(new IfcReleaseRecipient
        {
            Name = name,
            RecipientTypeCode = typeCode,
        });
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> RemoveRecipientAsync(int id, int recipientId, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "gỡ nơi nhận");

        var recipient = entity.Recipients.FirstOrDefault(x => x.Id == recipientId)
            ?? throw new IfcReleaseOperationException($"Nơi nhận #{recipientId} không tồn tại.");
        entity.Recipients.Remove(recipient);
        db.Remove(recipient);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> AcknowledgeRecipientAsync(int id, int recipientId,
        AcknowledgeIfcReleaseRecipientRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        if (entity.Status != IfcReleaseStatus.Released)
        {
            throw new IfcReleaseOperationException("Chỉ xác nhận được sau khi phiếu đã được phát hành.");
        }
        var recipient = entity.Recipients.FirstOrDefault(x => x.Id == recipientId)
            ?? throw new IfcReleaseOperationException($"Nơi nhận #{recipientId} không tồn tại.");
        recipient.AcknowledgedAt = DateTime.UtcNow;
        recipient.AcknowledgementNote = TrimOrNull(request.AcknowledgementNote);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> ReleaseAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases
            .Include(r => r.Items).ThenInclude(i => i.ShopDrawing)
            .Include(r => r.Recipients)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "phát hành");

        if (entity.Items.Count == 0)
        {
            throw new IfcReleaseOperationException("Phiếu chưa có bản vẽ nào — không thể phát hành.");
        }
        if (entity.Recipients.Count == 0)
        {
            throw new IfcReleaseOperationException("Phiếu chưa có nơi nhận nào — không thể phát hành.");
        }

        var invalid = entity.Items
            .Where(i => i.ShopDrawing.Status != ShopDrawingStatus.Approved
                     && i.ShopDrawing.Status != ShopDrawingStatus.PendingIfc)
            .Select(i => i.ShopDrawing.DrawingCode)
            .ToList();
        if (invalid.Count > 0)
        {
            throw new IfcReleaseOperationException(
                $"Bản vẽ chưa sẵn sàng để phát hành: {string.Join(", ", invalid)}.");
        }

        // Atomic release: flip every bundled drawing to Released — this is
        // the only writer for that state (the /status endpoint blocks it).
        foreach (var item in entity.Items)
        {
            item.ShopDrawing.Status = ShopDrawingStatus.Released;
            item.ShopDrawing.UpdatedAt = DateTime.UtcNow;
            item.ShopDrawing.UpdatedByUserId = callerUserId;
        }

        entity.Status = IfcReleaseStatus.Released;
        entity.ReleaseDate = DateTime.UtcNow;
        entity.IssuedByUserId = callerUserId;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "IfcRelease {Id} released with {Count} drawings by user {UserId}",
            entity.Id, entity.Items.Count, callerUserId);
        return (await GetAsync(id, ct))!;
    }

    public async Task<IfcReleaseResponse> CancelAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.IfcReleases.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new IfcReleaseOperationException($"Phiếu IFC #{id} không tồn tại.");
        EnsureDraft(entity, "huỷ");
        entity.Status = IfcReleaseStatus.Cancelled;
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetAsync(id, ct))!;
    }

    // ------------------------------ Helpers ---------------------------------

    private static void EnsureDraft(IfcRelease entity, string action)
    {
        if (entity.Status != IfcReleaseStatus.Draft)
        {
            throw new IfcReleaseOperationException(
                $"Không thể {action} — phiếu đã ở trạng thái {entity.Status}.");
        }
    }

    private async Task<string> AllocateReleaseNumberAsync(int designProjectId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"IFC-{year}-";
        var used = await db.IfcReleases
            .Where(r => r.DesignProjectId == designProjectId
                     && r.ReleaseNumber.StartsWith(prefix))
            .Select(r => r.ReleaseNumber)
            .ToListAsync(ct);
        var maxSeq = used
            .Select(c =>
            {
                var idx = c.LastIndexOf('-');
                if (idx < 0 || idx == c.Length - 1) return 0;
                return int.TryParse(c[(idx + 1)..], out var n) ? n : 0;
            })
            .DefaultIfEmpty(0)
            .Max();
        return $"{prefix}{maxSeq + 1:D3}";
    }

    private record LabelBundle(
        Dictionary<string, string> DisciplineByCode,
        Dictionary<string, string> RecipientByCode);

    private async Task<LabelBundle> LookupLabelsAsync(IEnumerable<IfcRelease> rows, CancellationToken ct)
    {
        var disciplineCodes = rows.SelectMany(r => r.Items.Select(i => i.ShopDrawing?.DisciplineCode))
            .Where(c => !string.IsNullOrEmpty(c))
            .Cast<string>()
            .Distinct().ToList();
        var recipientCodes = rows.SelectMany(r => r.Recipients.Select(x => x.RecipientTypeCode))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct().ToList();
        var disciplineMap = disciplineCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == DisciplineCategory && disciplineCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);
        var recipientMap = recipientCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == RecipientCategory && recipientCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);
        return new LabelBundle(disciplineMap, recipientMap);
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IfcReleaseResponse Map(
        IfcRelease r,
        IReadOnlyDictionary<string, string> disciplineByCode,
        IReadOnlyDictionary<string, string> recipientByCode) => new()
        {
            Id = r.Id,
            DesignProjectId = r.DesignProjectId,
            DesignProjectCode = r.DesignProject?.ProjectCode,
            ReleaseNumber = r.ReleaseNumber,
            Title = r.Title,
            ReleaseDate = r.ReleaseDate,
            IssuedByUserId = r.IssuedByUserId,
            IssuedByName = r.IssuedBy?.FullName,
            Status = r.Status.ToString(),
            Note = r.Note,
            Items = r.Items.Select(i => new IfcReleaseItemResponse
            {
                Id = i.Id,
                ShopDrawingId = i.ShopDrawingId,
                DrawingCode = i.ShopDrawing?.DrawingCode,
                Title = i.ShopDrawing?.Title,
                DisciplineCode = i.ShopDrawing?.DisciplineCode,
                DisciplineLabel = i.ShopDrawing?.DisciplineCode is null ? null
                    : disciplineByCode.TryGetValue(i.ShopDrawing.DisciplineCode, out var dl) ? dl : null,
                Status = i.ShopDrawing?.Status.ToString(),
            }).ToList(),
            Recipients = r.Recipients.Select(x => new IfcReleaseRecipientResponse
            {
                Id = x.Id,
                Name = x.Name,
                RecipientTypeCode = x.RecipientTypeCode,
                RecipientTypeLabel = recipientByCode.TryGetValue(x.RecipientTypeCode, out var rl) ? rl : null,
                AcknowledgedAt = x.AcknowledgedAt,
                AcknowledgementNote = x.AcknowledgementNote,
            }).ToList(),
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
        };
}
