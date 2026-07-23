using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Slice-1 implementation of <see cref="ISiteDiaryService"/> for the
/// NIH-142 daily site diary. Enforces the one-per-day-per-project rule,
/// the Draft → Submitted → Confirmed transitions and locks edits /
/// deletes when a diary is out of Draft.
/// </summary>
public class SiteDiaryService(
    AppDbContext db,
    ILogger<SiteDiaryService> logger) : ISiteDiaryService
{
    private const int MaxPageSize = 200;
    private const int MaxBulkDelete = 100;
    private const string WeatherCategory = "diary_weather";

    public async Task<SiteDiaryListResponse> ListAsync(SiteDiaryListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 50 : p.PageSize, 1, MaxPageSize);

        var q = db.SiteDiaries
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.SubmittedBy)
            .Include(d => d.ConfirmedBy)
            .AsQueryable();

        if (p.DesignProjectId.HasValue) q = q.Where(d => d.DesignProjectId == p.DesignProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.WeatherCode))
        {
            var code = p.WeatherCode.Trim();
            q = q.Where(d => d.WeatherCode == code);
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<SiteDiaryStatus>(s, true, out var v) ? (SiteDiaryStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0) q = q.Where(d => statuses.Contains(d.Status));
        }
        if (p.DateFrom.HasValue) q = q.Where(d => d.DiaryDate >= p.DateFrom.Value);
        if (p.DateTo.HasValue) q = q.Where(d => d.DiaryDate <= p.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(d => EF.Functions.Like(d.WorkPerformed, $"%{term}%")
                          || (d.Incidents != null && EF.Functions.Like(d.Incidents, $"%{term}%"))
                          || (d.MaterialsReceived != null && EF.Functions.Like(d.MaterialsReceived, $"%{term}%")));
        }

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(d => d.DiaryDate)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var weatherCodes = rows.Select(r => r.WeatherCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var weatherLabels = weatherCodes.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == WeatherCategory && weatherCodes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, StringComparer.OrdinalIgnoreCase, ct);

        // Per-status roll-up on the project-scoped set only — dropping
        // the status/date/search filters so the header pills track the
        // whole project workload, not the current filter view.
        var scope = db.SiteDiaries.AsNoTracking();
        if (p.DesignProjectId.HasValue) scope = scope.Where(d => d.DesignProjectId == p.DesignProjectId.Value);
        var statusCounts = await scope
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status.ToString(), x => x.Count, ct);

        return new SiteDiaryListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(r => Map(r, weatherLabels)).ToList(),
            StatusCounts = statusCounts,
        };
    }

    public async Task<SiteDiaryResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries
            .AsNoTracking()
            .Include(d => d.DesignProject)
            .Include(d => d.SubmittedBy)
            .Include(d => d.ConfirmedBy)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;

        var label = string.IsNullOrEmpty(entity.WeatherCode)
            ? null
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == WeatherCategory && m.Code == entity.WeatherCode)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);
        var lookup = label is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [entity.WeatherCode] = label };
        return Map(entity, lookup);
    }

    public async Task<SiteDiaryResponse> CreateAsync(CreateSiteDiaryRequest request, int callerUserId, CancellationToken ct = default)
    {
        ValidateWritable(request.WeatherCode, request.WorkPerformed,
            request.HeadcountLabor, request.HeadcountEngineers,
            request.HeadcountSupervisors, request.HeadcountSubcontractors);

        var project = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == request.DesignProjectId, ct);
        if (project is null)
        {
            throw new SiteDiaryOperationException($"Dự án #{request.DesignProjectId} không tồn tại.");
        }

        await EnsureWeatherCodeValidAsync(request.WeatherCode, ct);

        // One diary per project per calendar day — enforce here plus a
        // unique index at the DB level so a race can't sneak past.
        var duplicate = await db.SiteDiaries.AnyAsync(
            d => d.DesignProjectId == request.DesignProjectId && d.DiaryDate == request.DiaryDate, ct);
        if (duplicate)
        {
            throw new SiteDiaryOperationException(
                "Đã có nhật ký cho ngày này. Mở bản ghi cũ để chỉnh sửa thay vì tạo mới.");
        }

        var entity = new SiteDiary
        {
            DesignProjectId = request.DesignProjectId,
            DiaryDate = request.DiaryDate,
            WeatherCode = request.WeatherCode.Trim(),
            WeatherNote = TrimOrNull(request.WeatherNote),
            HeadcountLabor = request.HeadcountLabor,
            HeadcountEngineers = request.HeadcountEngineers,
            HeadcountSupervisors = request.HeadcountSupervisors,
            HeadcountSubcontractors = request.HeadcountSubcontractors,
            MachinesSummary = TrimOrNull(request.MachinesSummary),
            MaterialsReceived = TrimOrNull(request.MaterialsReceived),
            WorkPerformed = request.WorkPerformed.Trim(),
            Incidents = TrimOrNull(request.Incidents),
            Note = TrimOrNull(request.Note),
            Status = SiteDiaryStatus.Draft,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        };
        db.SiteDiaries.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SiteDiary {Id} created on project {ProjectId} for {Date:yyyy-MM-dd}",
            entity.Id, entity.DesignProjectId, entity.DiaryDate);
        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<SiteDiaryResponse?> UpdateAsync(int id, UpdateSiteDiaryRequest request, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return null;
        if (entity.Status != SiteDiaryStatus.Draft)
        {
            throw new SiteDiaryOperationException(
                "Chỉ chỉnh sửa được nhật ký ở trạng thái Nháp. Mở lại nhật ký trước khi sửa.");
        }

        ValidateWritable(request.WeatherCode, request.WorkPerformed,
            request.HeadcountLabor, request.HeadcountEngineers,
            request.HeadcountSupervisors, request.HeadcountSubcontractors);
        await EnsureWeatherCodeValidAsync(request.WeatherCode, ct);

        if (request.DiaryDate != entity.DiaryDate)
        {
            var duplicate = await db.SiteDiaries.AnyAsync(
                d => d.Id != id && d.DesignProjectId == entity.DesignProjectId && d.DiaryDate == request.DiaryDate, ct);
            if (duplicate)
            {
                throw new SiteDiaryOperationException(
                    "Đã có nhật ký cho ngày này. Chọn ngày khác hoặc mở bản ghi hiện có.");
            }
        }

        entity.DiaryDate = request.DiaryDate;
        entity.WeatherCode = request.WeatherCode.Trim();
        entity.WeatherNote = TrimOrNull(request.WeatherNote);
        entity.HeadcountLabor = request.HeadcountLabor;
        entity.HeadcountEngineers = request.HeadcountEngineers;
        entity.HeadcountSupervisors = request.HeadcountSupervisors;
        entity.HeadcountSubcontractors = request.HeadcountSubcontractors;
        entity.MachinesSummary = TrimOrNull(request.MachinesSummary);
        entity.MaterialsReceived = TrimOrNull(request.MaterialsReceived);
        entity.WorkPerformed = request.WorkPerformed.Trim();
        entity.Incidents = TrimOrNull(request.Incidents);
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<SiteDiaryResponse> SubmitAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new SiteDiaryOperationException($"Nhật ký #{id} không tồn tại.");
        if (entity.Status != SiteDiaryStatus.Draft)
        {
            throw new SiteDiaryOperationException("Chỉ gửi được nhật ký từ trạng thái Nháp.");
        }
        entity.Status = SiteDiaryStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.SubmittedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SiteDiary {Id} submitted by user {UserId}", id, callerUserId);
        return (await GetAsync(id, ct))!;
    }

    public async Task<SiteDiaryResponse> ConfirmAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new SiteDiaryOperationException($"Nhật ký #{id} không tồn tại.");
        if (entity.Status != SiteDiaryStatus.Submitted)
        {
            throw new SiteDiaryOperationException("Chỉ xác nhận được nhật ký ở trạng thái Đã gửi.");
        }
        entity.Status = SiteDiaryStatus.Confirmed;
        entity.ConfirmedAt = DateTime.UtcNow;
        entity.ConfirmedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SiteDiary {Id} confirmed by user {UserId}", id, callerUserId);
        return (await GetAsync(id, ct))!;
    }

    public async Task<SiteDiaryResponse> ReopenAsync(int id, int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new SiteDiaryOperationException($"Nhật ký #{id} không tồn tại.");
        if (entity.Status == SiteDiaryStatus.Draft)
        {
            throw new SiteDiaryOperationException("Nhật ký đã ở trạng thái Nháp.");
        }
        // Reopening blows away the confirm/submit stamps so a subsequent
        // resubmit records the fresh reviewer + timestamp.
        entity.Status = SiteDiaryStatus.Draft;
        entity.SubmittedAt = null;
        entity.SubmittedByUserId = null;
        entity.ConfirmedAt = null;
        entity.ConfirmedByUserId = null;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = callerUserId;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SiteDiary {Id} reopened by user {UserId}", id, callerUserId);
        return (await GetAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.SiteDiaries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return false;
        if (entity.Status != SiteDiaryStatus.Draft)
        {
            throw new SiteDiaryOperationException(
                "Chỉ xoá được nhật ký ở trạng thái Nháp.");
        }
        db.SiteDiaries.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("SiteDiary {Id} deleted", id);
        return true;
    }

    public async Task<SiteDiaryBulkDeleteResponse> BulkDeleteAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            throw new SiteDiaryOperationException("Danh sách nhật ký cần xoá là bắt buộc.");
        }
        if (ids.Count > MaxBulkDelete)
        {
            throw new SiteDiaryOperationException(
                $"Chỉ xoá tối đa {MaxBulkDelete} nhật ký mỗi lần.");
        }

        var distinctIds = ids.Distinct().ToList();
        var rows = await db.SiteDiaries.Where(d => distinctIds.Contains(d.Id)).ToListAsync(ct);

        var response = new SiteDiaryBulkDeleteResponse { Requested = distinctIds.Count };
        var found = rows.Select(r => r.Id).ToHashSet();
        foreach (var missing in distinctIds.Where(id => !found.Contains(id)))
        {
            response.Failures.Add(new SiteDiaryBulkDeleteFailure
            {
                Id = missing,
                Message = $"Nhật ký #{missing} không tồn tại.",
            });
        }

        var toDelete = new List<SiteDiary>();
        foreach (var row in rows)
        {
            if (row.Status != SiteDiaryStatus.Draft)
            {
                response.Failures.Add(new SiteDiaryBulkDeleteFailure
                {
                    Id = row.Id,
                    Message = "Chỉ xoá được nhật ký ở trạng thái Nháp.",
                });
                continue;
            }
            toDelete.Add(row);
        }

        if (toDelete.Count > 0)
        {
            db.SiteDiaries.RemoveRange(toDelete);
            await db.SaveChangesAsync(ct);
            response.Deleted = toDelete.Count;
            logger.LogInformation("SiteDiary bulk-deleted {Count} rows ({Ids})",
                toDelete.Count, string.Join(",", toDelete.Select(r => r.Id)));
        }
        return response;
    }

    // ------------------------------ Helpers ---------------------------------

    private static void ValidateWritable(
        string weather, string workPerformed,
        int labor, int engineers, int supervisors, int subs)
    {
        if (string.IsNullOrWhiteSpace(weather))
        {
            throw new SiteDiaryOperationException("Thời tiết là bắt buộc.");
        }
        if (string.IsNullOrWhiteSpace(workPerformed))
        {
            throw new SiteDiaryOperationException("Nội dung công việc thực hiện là bắt buộc.");
        }
        // Sanity — daily crew counts can't go negative. Not enforcing an
        // upper bound; a mega-project can have thousands on site.
        if (labor < 0 || engineers < 0 || supervisors < 0 || subs < 0)
        {
            throw new SiteDiaryOperationException("Số nhân sự không được âm.");
        }
    }

    private async Task EnsureWeatherCodeValidAsync(string code, CancellationToken ct)
    {
        var exists = await db.MasterDataOptions
            .AnyAsync(m => m.Category == WeatherCategory && m.Code == code.Trim() && m.IsActive, ct);
        if (!exists)
        {
            throw new SiteDiaryOperationException(
                $"Mã thời tiết '{code}' không hợp lệ.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static SiteDiaryResponse Map(SiteDiary d, IReadOnlyDictionary<string, string> weatherLabels) => new()
    {
        Id = d.Id,
        DesignProjectId = d.DesignProjectId,
        DesignProjectCode = d.DesignProject?.ProjectCode,
        DesignProjectName = d.DesignProject?.Name,
        DiaryDate = d.DiaryDate,
        WeatherCode = d.WeatherCode,
        WeatherLabel = weatherLabels.TryGetValue(d.WeatherCode, out var l) ? l : null,
        WeatherNote = d.WeatherNote,
        HeadcountLabor = d.HeadcountLabor,
        HeadcountEngineers = d.HeadcountEngineers,
        HeadcountSupervisors = d.HeadcountSupervisors,
        HeadcountSubcontractors = d.HeadcountSubcontractors,
        HeadcountTotal = d.HeadcountLabor + d.HeadcountEngineers + d.HeadcountSupervisors + d.HeadcountSubcontractors,
        MachinesSummary = d.MachinesSummary,
        MaterialsReceived = d.MaterialsReceived,
        WorkPerformed = d.WorkPerformed,
        Incidents = d.Incidents,
        Note = d.Note,
        Status = d.Status.ToString(),
        SubmittedAt = d.SubmittedAt,
        SubmittedByUserId = d.SubmittedByUserId,
        SubmittedByName = d.SubmittedBy?.FullName,
        ConfirmedAt = d.ConfirmedAt,
        ConfirmedByUserId = d.ConfirmedByUserId,
        ConfirmedByName = d.ConfirmedBy?.FullName,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
    };
}
