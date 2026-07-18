using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 Basic Design service — see <see cref="IBasicDesignDocService"/>.
/// </summary>
public class BasicDesignDocService(
    AppDbContext db,
    ILogger<BasicDesignDocService> logger) : IBasicDesignDocService
{
    private const int MaxPageSize = 200;
    private const string DisciplineCategory = "design_discipline";

    /// <summary>
    /// Required disciplines for the Shop Drawing unlock gate. Interior
    /// is optional — projects without a bespoke interior scope should
    /// still be able to move forward. Aligned with the M2 spec.
    /// </summary>
    private static readonly string[] RequiredDisciplines = new[] { "architecture", "structure", "mep" };

    /// <summary>Human-friendly prefixes used when auto-generating <c>DocumentCode</c>.</summary>
    private static readonly Dictionary<string, string> CodePrefix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["architecture"] = "KT-BD",
        ["structure"] = "KC-BD",
        ["mep"] = "MEP-BD",
        ["interior"] = "NT-BD",
    };

    public async Task<BasicDesignDocListResponse> ListAsync(BasicDesignDocListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.BasicDesignDocs
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.Owner)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(d => d.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.DisciplineCode))
        {
            var code = p.DisciplineCode.Trim();
            q = q.Where(d => d.DisciplineCode == code);
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<BasicDesignDocStatus>(s, true, out var v) ? (BasicDesignDocStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(d => statuses.Contains(d.Status));
        }
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.Title, $"%{term}%")
                          || EF.Functions.Like(d.DocumentCode, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(d => d.DisciplineCode)
            .ThenBy(d => d.DocumentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var disciplineCodes = rows.Select(r => r.DisciplineCode).Distinct().ToList();
        var labelByCode = disciplineCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == DisciplineCategory && disciplineCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        var readiness = p.DesignProjectId.HasValue
            ? await ComputeReadinessAsync(p.DesignProjectId.Value, ct)
            : new BasicDesignReadiness { RequiredDisciplineCodes = RequiredDisciplines.ToList() };

        return new BasicDesignDocListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, labelByCode)).ToList(),
            Readiness = readiness,
        };
    }

    public async Task<BasicDesignDocResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.BasicDesignDocs
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.Owner)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var label = await db.MasterDataOptions.AsNoTracking()
            .Where(m => m.Category == DisciplineCategory && m.Code == entity.DisciplineCode)
            .Select(m => m.Name)
            .FirstOrDefaultAsync(ct);
        var lookup = label is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [entity.DisciplineCode] = label };
        return Map(entity, lookup);
    }

    public async Task<BasicDesignDocResponse> CreateAsync(CreateBasicDesignDocRequest request, int callerUserId, CancellationToken ct = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BasicDesignDocOperationException("Tên bản vẽ là bắt buộc.");
        }

        var discipline = (request.DisciplineCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(discipline))
        {
            throw new BasicDesignDocOperationException("Bộ môn là bắt buộc.");
        }

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new BasicDesignDocOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        // Guard: only create Basic Design docs while the parent project is
        // actually at the Basic Design stage. Otherwise the doc list would
        // grow retroactively while the workflow is closed.
        if (project.CurrentStage != DesignProjectStage.BasicDesign)
        {
            throw new BasicDesignDocOperationException(
                "Chỉ tạo được hồ sơ Basic Design khi dự án đang ở giai đoạn Basic Design.");
        }

        var disciplineExists = await db.MasterDataOptions
            .AnyAsync(m => m.Category == DisciplineCategory && m.Code == discipline && m.IsActive, ct);
        if (!disciplineExists)
        {
            throw new BasicDesignDocOperationException($"Bộ môn '{discipline}' không hợp lệ.");
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new BasicDesignDocOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        var code = await AllocateDocumentCodeAsync(request.DesignProjectId, discipline, ct);

        var entity = new BasicDesignDoc
        {
            DesignProjectId = request.DesignProjectId,
            DisciplineCode = discipline,
            DocumentCode = code,
            Title = title,
            Description = TrimOrNull(request.Description),
            OwnerUserId = request.OwnerUserId,
            Note = TrimOrNull(request.Note),
            Status = BasicDesignDocStatus.InProgress,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.BasicDesignDocs.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "BasicDesignDoc {Id} ({Code}) created for project {ProjectId} by user {UserId}",
            entity.Id, entity.DocumentCode, request.DesignProjectId, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<BasicDesignDocResponse?> UpdateAsync(int id, UpdateBasicDesignDocRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.BasicDesignDocs.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new BasicDesignDocOperationException("Tên bản vẽ là bắt buộc.");
        }
        var discipline = (request.DisciplineCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(discipline))
        {
            throw new BasicDesignDocOperationException("Bộ môn là bắt buộc.");
        }

        // Locked once the drawing has hit the permit / approved rails.
        // The FE hides the edit button; we still re-check on write.
        if (entity.Status is BasicDesignDocStatus.PermitApproved or BasicDesignDocStatus.Rejected)
        {
            throw new BasicDesignDocOperationException(
                "Bản vẽ đã ở trạng thái kết thúc không thể chỉnh sửa.");
        }

        if (!string.Equals(discipline, entity.DisciplineCode, StringComparison.OrdinalIgnoreCase))
        {
            // A discipline change effectively re-classifies the drawing;
            // block once it's been through review to avoid moving history
            // rows into a category they weren't reviewed under.
            if (entity.Status != BasicDesignDocStatus.InProgress)
            {
                throw new BasicDesignDocOperationException(
                    "Không thể đổi bộ môn khi bản vẽ đã qua bước Đang thiết kế.");
            }
            var newDisciplineExists = await db.MasterDataOptions
                .AnyAsync(m => m.Category == DisciplineCategory && m.Code == discipline && m.IsActive, ct);
            if (!newDisciplineExists)
            {
                throw new BasicDesignDocOperationException($"Bộ môn '{discipline}' không hợp lệ.");
            }
            entity.DisciplineCode = discipline;
            entity.DocumentCode = await AllocateDocumentCodeAsync(entity.DesignProjectId, discipline, ct);
        }

        if (request.OwnerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.OwnerUserId.Value, ct))
        {
            throw new BasicDesignDocOperationException($"Người phụ trách #{request.OwnerUserId} không tồn tại.");
        }

        entity.Title = title;
        entity.Description = TrimOrNull(request.Description);
        entity.OwnerUserId = request.OwnerUserId;
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.BasicDesignDocs.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;

        if (entity.Status != BasicDesignDocStatus.InProgress)
        {
            throw new BasicDesignDocOperationException(
                "Chỉ xoá được bản vẽ khi còn ở trạng thái Đang thiết kế.");
        }

        db.BasicDesignDocs.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("BasicDesignDoc {Id} deleted", id);
        return true;
    }

    public async Task<BasicDesignDocResponse?> TransitionStatusAsync(int id,
        TransitionBasicDesignDocStatusRequest request, int callerUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<BasicDesignDocStatus>(request.Status, true, out var next))
        {
            throw new BasicDesignDocOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
        }

        var entity = await db.BasicDesignDocs.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        EnsureTransitionAllowed(entity.Status, next);

        entity.Status = next;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "BasicDesignDoc {Id} transitioned to {Status} by user {UserId}", id, next, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<DesignProjectResponse> UnlockShopDrawingAsync(int designProjectId, int callerUserId, CancellationToken ct = default)
    {
        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == designProjectId, ct);
        if (project is null)
        {
            throw new BasicDesignDocOperationException($"Dự án #{designProjectId} không tồn tại.");
        }
        if (project.CurrentStage != DesignProjectStage.BasicDesign)
        {
            throw new BasicDesignDocOperationException(
                "Dự án không ở giai đoạn Basic Design nên không thể mở khoá Shop Drawing.");
        }

        var readiness = await ComputeReadinessAsync(designProjectId, ct);
        if (!readiness.ReadyForShopDrawing)
        {
            var missing = readiness.RequiredDisciplineCodes
                .Except(readiness.InternallyApprovedDisciplineCodes, StringComparer.OrdinalIgnoreCase)
                .ToList();
            throw new BasicDesignDocOperationException(
                $"Chưa đủ hồ sơ duyệt nội bộ cho các bộ môn: {string.Join(", ", missing)}.");
        }

        project.CurrentStage = DesignProjectStage.ShopDrawing;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "DesignProject {Id} unlocked to ShopDrawing by user {UserId}",
            designProjectId, callerUserId);

        // Rehydrate via the design-project service-style projection so the
        // caller (controller) can return a fully-populated response.
        var refreshed = await db.DesignProjects
            .AsNoTracking()
            .Include(dp => dp.Customer)
            .Include(dp => dp.Contract)
            .Include(dp => dp.ProjectManager)
            .Include(dp => dp.DesignLead)
            .FirstAsync(dp => dp.Id == designProjectId, ct);
        return new DesignProjectResponse
        {
            Id = refreshed.Id,
            ProjectCode = refreshed.ProjectCode,
            Name = refreshed.Name,
            CustomerId = refreshed.CustomerId,
            CustomerName = refreshed.Customer?.Name,
            ContractId = refreshed.ContractId,
            ContractNumber = refreshed.Contract?.ContractNumber,
            ProjectManagerUserId = refreshed.ProjectManagerUserId,
            ProjectManagerName = refreshed.ProjectManager?.FullName,
            DesignLeadUserId = refreshed.DesignLeadUserId,
            DesignLeadName = refreshed.DesignLead?.FullName,
            StartDate = refreshed.StartDate,
            Deadline = refreshed.Deadline,
            CurrentStage = refreshed.CurrentStage.ToString(),
            Status = refreshed.Status.ToString(),
            Note = refreshed.Note,
            CreatedAt = refreshed.CreatedAt,
            UpdatedAt = refreshed.UpdatedAt,
        };
    }

    // ------------------------------ Helpers ---------------------------------

    private async Task<BasicDesignReadiness> ComputeReadinessAsync(int designProjectId, CancellationToken ct)
    {
        var approvedDisciplines = await db.BasicDesignDocs.AsNoTracking()
            .Where(d => d.DesignProjectId == designProjectId
                     && (d.Status == BasicDesignDocStatus.InternallyApproved
                      || d.Status == BasicDesignDocStatus.SubmittedForPermit
                      || d.Status == BasicDesignDocStatus.PermitApproved))
            .Select(d => d.DisciplineCode)
            .Distinct()
            .ToListAsync(ct);

        return new BasicDesignReadiness
        {
            RequiredDisciplineCodes = RequiredDisciplines.ToList(),
            InternallyApprovedDisciplineCodes = approvedDisciplines,
            ReadyForShopDrawing = RequiredDisciplines.All(rd =>
                approvedDisciplines.Contains(rd, StringComparer.OrdinalIgnoreCase)),
        };
    }

    private async Task<string> AllocateDocumentCodeAsync(int designProjectId, string discipline, CancellationToken ct)
    {
        var prefix = CodePrefix.TryGetValue(discipline, out var p) ? p : $"{discipline.ToUpperInvariant()}-BD";
        var used = await db.BasicDesignDocs
            .Where(d => d.DesignProjectId == designProjectId
                     && d.DisciplineCode == discipline)
            .Select(d => d.DocumentCode)
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
        return $"{prefix}-{maxSeq + 1:D3}";
    }

    /// <summary>
    /// State machine for a Basic Design doc:
    /// <list type="bullet">
    ///   <item>InProgress → SubmittedForReview | Rejected</item>
    ///   <item>SubmittedForReview → InternallyApproved | InProgress | Rejected</item>
    ///   <item>InternallyApproved → SubmittedForPermit | InProgress | Rejected</item>
    ///   <item>SubmittedForPermit → PermitApproved | InternallyApproved | Rejected</item>
    ///   <item>PermitApproved → Rejected (rare — permit revoked)</item>
    ///   <item>Rejected is terminal</item>
    /// </list>
    /// </summary>
    private static void EnsureTransitionAllowed(BasicDesignDocStatus from, BasicDesignDocStatus to)
    {
        if (from == to) return; // idempotent no-op
        bool ok = (from, to) switch
        {
            (BasicDesignDocStatus.InProgress, BasicDesignDocStatus.SubmittedForReview) => true,
            (BasicDesignDocStatus.InProgress, BasicDesignDocStatus.Rejected) => true,

            (BasicDesignDocStatus.SubmittedForReview, BasicDesignDocStatus.InternallyApproved) => true,
            (BasicDesignDocStatus.SubmittedForReview, BasicDesignDocStatus.InProgress) => true,
            (BasicDesignDocStatus.SubmittedForReview, BasicDesignDocStatus.Rejected) => true,

            (BasicDesignDocStatus.InternallyApproved, BasicDesignDocStatus.SubmittedForPermit) => true,
            (BasicDesignDocStatus.InternallyApproved, BasicDesignDocStatus.InProgress) => true,
            (BasicDesignDocStatus.InternallyApproved, BasicDesignDocStatus.Rejected) => true,

            (BasicDesignDocStatus.SubmittedForPermit, BasicDesignDocStatus.PermitApproved) => true,
            (BasicDesignDocStatus.SubmittedForPermit, BasicDesignDocStatus.InternallyApproved) => true,
            (BasicDesignDocStatus.SubmittedForPermit, BasicDesignDocStatus.Rejected) => true,

            (BasicDesignDocStatus.PermitApproved, BasicDesignDocStatus.Rejected) => true,
            _ => false,
        };
        if (!ok)
        {
            throw new BasicDesignDocOperationException(
                $"Không thể chuyển từ '{from}' sang '{to}'.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static BasicDesignDocResponse Map(BasicDesignDoc d, IReadOnlyDictionary<string, string> labelByCode) => new()
    {
        Id = d.Id,
        DesignProjectId = d.DesignProjectId,
        DesignProjectCode = d.DesignProject?.ProjectCode,
        DisciplineCode = d.DisciplineCode,
        DisciplineLabel = labelByCode.TryGetValue(d.DisciplineCode, out var label) ? label : null,
        DocumentCode = d.DocumentCode,
        Title = d.Title,
        Description = d.Description,
        OwnerUserId = d.OwnerUserId,
        OwnerName = d.Owner?.FullName,
        Status = d.Status.ToString(),
        Note = d.Note,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
    };
}
