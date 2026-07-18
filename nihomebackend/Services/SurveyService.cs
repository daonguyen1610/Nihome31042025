using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Survey service — see <see cref="ISurveyService"/>.
/// NIH-99 slice: list + get + a minimal create used by tests and the
/// sample seeder. Later slices (NIH-100/101) will layer update, delete,
/// media, and drive-sync workflows on top.
/// </summary>
public class SurveyService(
    AppDbContext db,
    ILogger<SurveyService> logger) : ISurveyService
{
    private const int MaxPageSize = 100;
    private const string ConstructionTypeCategory = "construction_type";

    public async Task<SurveyListResponse> ListAsync(SurveyListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.Surveys
            .AsNoTracking()
            .Include(s => s.Surveyor)
            .Include(s => s.LinkedProject)
            .Include(s => s.LinkedOpportunity)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(p.ConstructionTypeCode))
        {
            var code = p.ConstructionTypeCode.Trim();
            q = q.Where(s => s.ConstructionTypeCode == code);
        }
        if (p.SurveyorUserId.HasValue) q = q.Where(s => s.SurveyorUserId == p.SurveyorUserId.Value);
        if (p.LinkedProjectId.HasValue) q = q.Where(s => s.LinkedProjectId == p.LinkedProjectId.Value);
        if (!string.IsNullOrWhiteSpace(p.DriveSyncStatus))
        {
            var statuses = p.DriveSyncStatus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<SurveyDriveSyncStatus>(s, true, out var v) ? (SurveyDriveSyncStatus?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            if (statuses.Count > 0)
            {
                q = q.Where(s => statuses.Contains(s.DriveSyncStatus));
            }
        }
        if (p.DateFrom.HasValue) q = q.Where(s => s.SurveyDate >= p.DateFrom.Value);
        if (p.DateTo.HasValue) q = q.Where(s => s.SurveyDate <= p.DateTo.Value);
        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(s => EF.Functions.Like(s.Location, $"%{term}%")
                          || EF.Functions.Like(s.Code, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        // Spec NIH-99 AC #5: default sort SurveyDate DESC so the most recent
        // visits surface first.
        var rows = await q
            .OrderByDescending(s => s.SurveyDate)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-resolve construction-type labels so we don't fire one query
        // per row. Empty when the whole page has no construction type set.
        var codes = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ConstructionTypeCode))
            .Select(r => r.ConstructionTypeCode!)
            .Distinct()
            .ToList();
        var labelByCode = codes.Count == 0
            ? new Dictionary<string, string>()
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == ConstructionTypeCategory && codes.Contains(m.Code))
                .ToDictionaryAsync(m => m.Code, m => m.Name, ct);

        return new SurveyListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(s => new SurveyListItemResponse
            {
                Id = s.Id,
                Code = s.Code,
                Location = s.Location,
                ConstructionTypeCode = s.ConstructionTypeCode,
                ConstructionTypeLabel = s.ConstructionTypeCode != null && labelByCode.TryGetValue(s.ConstructionTypeCode, out var label)
                    ? label
                    : null,
                SurveyDate = s.SurveyDate,
                SurveyorUserId = s.SurveyorUserId,
                SurveyorName = s.Surveyor?.FullName,
                LinkedProjectId = s.LinkedProjectId,
                LinkedProjectName = s.LinkedProject?.Name,
                LinkedOpportunityId = s.LinkedOpportunityId,
                LinkedOpportunityName = s.LinkedOpportunity?.Name,
                DriveSyncStatus = s.DriveSyncStatus.ToString(),
                DriveSyncError = s.DriveSyncError,
                LastSyncedAt = s.LastSyncedAt,
                UpdatedAt = s.UpdatedAt,
            }).ToList(),
        };
    }

    public async Task<SurveyResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.Surveys
            .AsNoTracking()
            .Include(s => s.Surveyor)
            .Include(s => s.LinkedProject)
            .Include(s => s.LinkedOpportunity)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return null;

        var label = string.IsNullOrWhiteSpace(entity.ConstructionTypeCode)
            ? null
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == ConstructionTypeCategory && m.Code == entity.ConstructionTypeCode)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);

        return Map(entity, label);
    }

    public async Task<SurveyResponse> CreateAsync(CreateSurveyRequest request, int callerUserId, CancellationToken ct = default)
    {
        var location = (request.Location ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new SurveyOperationException("Địa điểm khảo sát là bắt buộc.");
        }
        if (request.SurveyDate == default)
        {
            throw new SurveyOperationException("Ngày khảo sát là bắt buộc.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConstructionTypeCode))
        {
            var typeCode = request.ConstructionTypeCode.Trim();
            var exists = await db.MasterDataOptions
                .AnyAsync(m => m.Category == ConstructionTypeCategory && m.Code == typeCode && m.IsActive, ct);
            if (!exists)
            {
                throw new SurveyOperationException($"Loại công trình '{typeCode}' không hợp lệ.");
            }
        }

        if (request.SurveyorUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.SurveyorUserId.Value, ct))
        {
            throw new SurveyOperationException($"Người khảo sát #{request.SurveyorUserId} không tồn tại.");
        }

        if (request.LinkedProjectId.HasValue &&
            !await db.Projects.AnyAsync(p => p.Id == request.LinkedProjectId.Value, ct))
        {
            throw new SurveyOperationException($"Dự án #{request.LinkedProjectId} không tồn tại.");
        }
        if (request.LinkedOpportunityId.HasValue &&
            !await db.Opportunities.AnyAsync(o => o.Id == request.LinkedOpportunityId.Value, ct))
        {
            throw new SurveyOperationException($"Cơ hội #{request.LinkedOpportunityId} không tồn tại.");
        }

        var year = DateTime.UtcNow.Year;
        var nextSeq = 1 + await db.Surveys
            .Where(s => s.Code.StartsWith($"SV-{year}-"))
            .CountAsync(ct);
        var code = $"SV-{year}-{nextSeq:D4}";

        var entity = new Survey
        {
            Code = code,
            Location = location,
            ConstructionTypeCode = TrimOrNull(request.ConstructionTypeCode),
            SurveyDate = request.SurveyDate,
            SurveyorUserId = request.SurveyorUserId,
            LinkedProjectId = request.LinkedProjectId,
            LinkedOpportunityId = request.LinkedOpportunityId,
            Note = TrimOrNull(request.Note),
            DriveSyncStatus = SurveyDriveSyncStatus.NotSynced,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Surveys.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Survey {Id} ({Code}) created by user {UserId}",
            entity.Id, entity.Code, callerUserId);
        return (await GetAsync(entity.Id, ct))!;
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static SurveyResponse Map(Survey s, string? constructionTypeLabel) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Location = s.Location,
        ConstructionTypeCode = s.ConstructionTypeCode,
        ConstructionTypeLabel = constructionTypeLabel,
        SurveyDate = s.SurveyDate,
        SurveyorUserId = s.SurveyorUserId,
        SurveyorName = s.Surveyor?.FullName,
        LinkedProjectId = s.LinkedProjectId,
        LinkedProjectName = s.LinkedProject?.Name,
        LinkedOpportunityId = s.LinkedOpportunityId,
        LinkedOpportunityName = s.LinkedOpportunity?.Name,
        Note = s.Note,
        DriveSyncStatus = s.DriveSyncStatus.ToString(),
        DriveSyncError = s.DriveSyncError,
        LastSyncedAt = s.LastSyncedAt,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
