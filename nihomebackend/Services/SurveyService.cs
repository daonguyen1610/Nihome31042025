using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Survey CRUD and detail projection. Media and Drive mutations are delegated
/// to <see cref="ISurveyMediaService"/>.
/// </summary>
public class SurveyService(
    AppDbContext db,
    ILogger<SurveyService> logger,
    IProjectDocumentStagingService projectDocuments) : ISurveyService
{
    private const int MaxPageSize = 100;
    private const string ConstructionTypeCategory = "construction_type";
    private const string ChecklistCategory = "survey_checklist_default";

    public async Task<SurveyListResponse> ListAsync(
        SurveyListParams p, int callerUserId, bool canViewAll,
        CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.Surveys
            .AsNoTracking()
            .Include(s => s.Surveyor)
            .Include(s => s.LinkedProject)
            .Include(s => s.LinkedOpportunity)
            .Include(s => s.OperationalProject)
            .AsQueryable();

        q = ApplyAccessScope(q, callerUserId, canViewAll);

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
                OperationalProjectId = s.OperationalProjectId,
                OperationalProjectName = s.OperationalProject?.Name,
                DriveSyncStatus = s.DriveSyncStatus.ToString(),
                DriveSyncError = s.DriveSyncError,
                LastSyncedAt = s.LastSyncedAt,
                UpdatedAt = s.UpdatedAt,
            }).ToList(),
        };
    }

    public async Task<SurveyResponse?> GetAsync(
        int id, int callerUserId, bool canViewAll, CancellationToken ct = default)
    {
        var entity = db.Surveys
            .AsNoTracking()
            .Include(s => s.Surveyor)
            .Include(s => s.LinkedProject)
            .Include(s => s.LinkedOpportunity)
            .Include(s => s.OperationalProject)
            .Include(s => s.Media)
            .Include(s => s.ChecklistResults)
            .Include(s => s.SiteConditions)
            .Where(s => s.Id == id);
        entity = ApplyAccessScope(entity, callerUserId, canViewAll);
        var found = await entity.FirstOrDefaultAsync(ct);
        if (found is null) return null;

        var label = string.IsNullOrWhiteSpace(found.ConstructionTypeCode)
            ? null
            : await db.MasterDataOptions.AsNoTracking()
                .Where(m => m.Category == ConstructionTypeCategory && m.Code == found.ConstructionTypeCode)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);

        return Map(found, label);
    }

    public async Task<SurveyResponse> CreateAsync(
        CreateSurveyRequest request, int callerUserId, bool canManageAll = false,
        CancellationToken ct = default)
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
        var routing = await ResolveRoutingAsync(
            request.OperationalProjectId, request.LinkedOpportunityId, requireProject: true, ct);
        if (!await CanAccessProjectAsync(routing.ProjectId!.Value, callerUserId, canManageAll, ct))
        {
            throw new SurveyOperationException(
                "Bạn chỉ có thể tạo phiếu khảo sát trong dự án do mình tạo hoặc phụ trách.");
        }
        await ValidateSurveyorAssignmentAsync(
            request.SurveyorUserId, routing.ProjectId.Value, callerUserId, canManageAll, ct);

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
            OperationalProjectId = routing.ProjectId!.Value,
            Note = TrimOrNull(request.Note),
            DriveSyncStatus = SurveyDriveSyncStatus.NotSynced,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Surveys.Add(entity);
        await db.SaveChangesAsync(ct);

        var checklistTemplates = await db.MasterDataOptions.AsNoTracking()
            .Where(option => option.Category == ChecklistCategory && option.IsActive)
            .OrderBy(option => option.SortOrder)
            .ToListAsync(ct);
        db.SurveyChecklistResults.AddRange(checklistTemplates.Select(template => new SurveyChecklistResult
        {
            SurveyId = entity.Id,
            TemplateCode = template.Code,
            TemplateTitle = template.Name,
            SortOrder = template.SortOrder,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.CreatedAt,
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
        }));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Survey {Id} ({Code}) created by user {UserId}",
            entity.Id, entity.Code, callerUserId);
        return (await GetAsync(entity.Id, callerUserId, canManageAll, ct))!;
    }

    // ------------------------------ Update ----------------------------------

    public async Task<SurveyResponse?> UpdateAsync(
        int id, UpdateSurveyRequest request, int callerUserId, bool canManageAll = false,
        CancellationToken ct = default)
    {
        var entity = await ApplyAccessScope(db.Surveys, callerUserId, canManageAll)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return null;

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
        var previousProjectId = entity.OperationalProjectId;
        var requestedProjectId = request.OperationalProjectId ?? previousProjectId;
        var routing = await ResolveRoutingAsync(
            requestedProjectId, request.LinkedOpportunityId, requireProject: true, ct);
        var newProjectId = routing.ProjectId;
        if (previousProjectId != newProjectId &&
            !await CanAccessProjectAsync(newProjectId!.Value, callerUserId, canManageAll, ct))
        {
            throw new SurveyOperationException(
                "Bạn chỉ có thể chuyển phiếu khảo sát sang dự án do mình tạo hoặc phụ trách.");
        }
        if (request.SurveyorUserId != entity.SurveyorUserId)
        {
            await ValidateSurveyorAssignmentAsync(
                request.SurveyorUserId, newProjectId!.Value, callerUserId, canManageAll, ct);
        }
        await using var transaction = previousProjectId != newProjectId && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        entity.Location = location;
        entity.ConstructionTypeCode = TrimOrNull(request.ConstructionTypeCode);
        entity.SurveyDate = request.SurveyDate;
        entity.SurveyorUserId = request.SurveyorUserId;
        entity.LinkedProjectId = request.LinkedProjectId;
        entity.LinkedOpportunityId = request.LinkedOpportunityId;
        entity.OperationalProjectId = newProjectId!.Value;
        entity.Note = TrimOrNull(request.Note);
        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        if (previousProjectId != newProjectId)
        {
            var media = await db.SurveyMedia.Where(item => item.SurveyId == entity.Id).ToListAsync(ct);
            var files = media.Select(item => new ProjectDocumentMoveDescriptor(
                ProjectDocumentCategory.Survey,
                ProjectDocumentSourceModule.Survey,
                EntityTypes.SurveyMedia,
                SurveyMediaService.ProjectDocumentSlot,
                item.Id,
                item.RelativePath,
                item.OriginalFileName,
                routing.CustomerId,
                null)).ToList();
            await projectDocuments.StageExistingManagedFilesMoveAsync(
                previousProjectId, newProjectId, files, callerUserId, ct);

            var now = DateTime.UtcNow;
            foreach (var item in media)
            {
                item.DriveFileId = null;
                item.DriveFolderId = null;
                item.DriveFolderLink = null;
                item.SyncStatus = SurveyMediaSyncStatus.Pending;
                item.SyncAttemptCount = 0;
                item.SyncError = null;
                item.NextSyncAttemptAt = now;
                item.SyncStartedAt = null;
                item.LastSyncAttemptAt = null;
                item.SyncedAt = null;
                item.ClaimToken = null;
                item.ClaimExpiresAt = null;
                item.UpdatedAt = now;
                item.UpdatedByUserId = callerUserId;
            }
            entity.DriveSyncStatus = SurveyDriveSyncStatus.NotSynced;
            entity.DriveSyncError = null;
            entity.LastSyncedAt = null;
            entity.DriveFolderId = null;
            entity.DriveFolderLink = null;
        }

        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        logger.LogInformation("Survey {Id} updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, callerUserId, canManageAll, ct);
    }

    // ------------------------------ Delete ----------------------------------

    public async Task<bool> DeleteAsync(
        int id, int callerUserId, bool canManageAll, CancellationToken ct = default)
    {
        var entity = await ApplyAccessScope(db.Surveys, callerUserId, canManageAll)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return false;

        if (await db.SurveyMedia.AnyAsync(media => media.SurveyId == id, ct))
        {
            throw new SurveyOperationException(
                "Phiếu khảo sát còn tệp phương tiện. Vui lòng xoá từng tệp để hệ thống dọn dữ liệu Google Drive và vùng lưu trữ riêng tư trước.");
        }

        db.Surveys.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Survey {Id} deleted", id);
        return true;
    }

    // ------------------------------ Timeline (NIH-101) ----------------------

    public async Task<List<SurveyTimelineEvent>?> GetTimelineAsync(
        int id, int limit, int callerUserId, bool canViewAll,
        CancellationToken ct = default)
    {
        var exists = await ApplyAccessScope(db.Surveys.AsNoTracking(), callerUserId, canViewAll)
            .AnyAsync(s => s.Id == id, ct);
        if (!exists) return null;

        if (limit < 1) limit = 1;
        if (limit > 500) limit = 500;

        var idText = id.ToString();
        var rows = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.ResourceType == EntityTypes.Survey && a.ResourceId == idText)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.CreatedAt,
                a.Action,
                a.Message,
                a.ActorUserId,
                UserName = a.ActorUserId != null
                    ? db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.FullName).FirstOrDefault()
                    : null,
            })
            .ToListAsync(ct);

        return rows.Select(a => new SurveyTimelineEvent
        {
            Id = a.Id,
            OccurredAt = a.CreatedAt,
            Action = a.Action,
            Message = a.Message,
            UserId = a.ActorUserId,
            UserName = a.UserName,
        }).ToList();
    }

    // ------------------------------ Helpers ---------------------------------

    public Task<bool> CanAccessAsync(
        int id, int callerUserId, bool canAccessAll, CancellationToken ct = default) =>
        ApplyAccessScope(db.Surveys.AsNoTracking(), callerUserId, canAccessAll)
            .AnyAsync(survey => survey.Id == id, ct);

    private static IQueryable<Survey> ApplyAccessScope(
        IQueryable<Survey> query, int callerUserId, bool canAccessAll)
    {
        if (canAccessAll) return query;
        return query.Where(survey =>
            survey.SurveyorUserId == callerUserId ||
            survey.CreatedByUserId == callerUserId ||
            survey.OperationalProject.ProjectManagerUserId == callerUserId ||
            survey.OperationalProject.CreatedByUserId == callerUserId);
    }

    private Task<bool> CanAccessProjectAsync(
        int projectId, int callerUserId, bool canAccessAll, CancellationToken ct)
    {
        if (canAccessAll) return Task.FromResult(true);
        return db.OperationalProjects.AsNoTracking().AnyAsync(project =>
            project.Id == projectId &&
            (project.ProjectManagerUserId == callerUserId ||
             project.CreatedByUserId == callerUserId), ct);
    }

    private async Task ValidateSurveyorAssignmentAsync(
        int? surveyorUserId,
        int projectId,
        int callerUserId,
        bool canManageAll,
        CancellationToken ct)
    {
        if (!surveyorUserId.HasValue || surveyorUserId.Value == callerUserId || canManageAll) return;
        if (!await CanAccessProjectAsync(projectId, callerUserId, false, ct))
        {
            throw new SurveyOperationException(
                "Chỉ người tạo/phụ trách dự án hoặc người có quyền quản lý toàn bộ mới được phân công người khảo sát khác.");
        }
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<SurveyRoutingContext> ResolveRoutingAsync(
        int? operationalProjectId,
        int? opportunityId,
        bool requireProject,
        CancellationToken ct)
    {
        if (!operationalProjectId.HasValue || operationalProjectId.Value <= 0)
        {
            if (opportunityId.HasValue)
            {
                var opportunityProjectId = await db.Opportunities.AsNoTracking()
                    .Where(opportunity => opportunity.Id == opportunityId.Value)
                    .Select(opportunity => opportunity.OperationalProjectId)
                    .FirstOrDefaultAsync(ct);
                if (!opportunityProjectId.HasValue)
                {
                    throw new SurveyOperationException(
                        $"Cơ hội #{opportunityId} chưa được gán dự án vận hành. Hãy gán dự án cho cơ hội trước khi lưu phiếu khảo sát.");
                }
                throw new SurveyOperationException(
                    $"Dự án vận hành là bắt buộc và phải là dự án #{opportunityProjectId.Value} của cơ hội đã chọn.");
            }
            if (requireProject)
            {
                throw new SurveyOperationException(
                    "Dự án vận hành là bắt buộc. Hãy chọn dự án thuộc đúng khách hàng trước khi tạo phiếu khảo sát.");
            }
            return new SurveyRoutingContext(null, null);
        }

        var project = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == operationalProjectId.Value)
            .Select(item => new { item.Id, item.CustomerId })
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            throw new SurveyOperationException($"Dự án vận hành #{operationalProjectId} không tồn tại.");
        }

        if (opportunityId.HasValue)
        {
            var opportunity = await db.Opportunities.AsNoTracking()
                .Where(item => item.Id == opportunityId.Value)
                .Select(item => new { item.OperationalProjectId, item.CustomerId })
                .FirstOrDefaultAsync(ct);
            if (opportunity is null)
            {
                throw new SurveyOperationException($"Cơ hội #{opportunityId} không tồn tại.");
            }
            if (!opportunity.OperationalProjectId.HasValue)
            {
                throw new SurveyOperationException(
                    $"Cơ hội #{opportunityId} chưa được gán dự án vận hành. Hãy gán dự án cho cơ hội trước khi lưu phiếu khảo sát.");
            }
            if (opportunity.OperationalProjectId.Value != project.Id)
            {
                throw new SurveyOperationException(
                    $"Dự án vận hành #{project.Id} không khớp dự án #{opportunity.OperationalProjectId.Value} của cơ hội #{opportunityId}.");
            }
            if (opportunity.CustomerId != project.CustomerId)
            {
                throw new SurveyOperationException(
                    "Dự án vận hành và cơ hội phải thuộc cùng một khách hàng.");
            }
        }

        return new SurveyRoutingContext(project.Id, project.CustomerId);
    }

    private sealed record SurveyRoutingContext(int? ProjectId, int? CustomerId);

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
        OperationalProjectId = s.OperationalProjectId,
        OperationalProjectName = s.OperationalProject?.Name,
        Note = s.Note,
        DriveSyncStatus = s.DriveSyncStatus.ToString(),
        DriveSyncError = s.DriveSyncError,
        LastSyncedAt = s.LastSyncedAt,
        DriveFolderLink = s.DriveFolderLink,
        Media = s.Media.OrderByDescending(m => m.CreatedAt).Select(SurveyMediaService.Map).ToList(),
        ChecklistResults = s.ChecklistResults.OrderBy(r => r.SortOrder).Select(SurveyMediaService.Map).ToList(),
        SiteConditions = s.SiteConditions
            .OrderBy(condition => condition.Category)
            .ThenBy(condition => condition.Code)
            .Select(SurveyConditionService.Map)
            .ToList(),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
