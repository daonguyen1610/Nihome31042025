using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// M2 DesignProject service — see <see cref="IDesignProjectService"/>.
/// </summary>
public class DesignProjectService(
    AppDbContext db,
    IPermitChecklistService permitChecklistService,
    ILogger<DesignProjectService> logger) : IDesignProjectService
{
    private const int MaxPageSize = 100;

    public async Task<DesignProjectListResponse> ListAsync(DesignProjectListParams p, CancellationToken ct = default)
    {
        var page = p.Page < 1 ? 1 : p.Page;
        var pageSize = Math.Clamp(p.PageSize <= 0 ? 20 : p.PageSize, 1, MaxPageSize);

        var q = db.DesignProjects
            .AsNoTracking()
            .Include(dp => dp.Customer)
            .Include(dp => dp.Contract)
            .Include(dp => dp.ProjectManager)
            .Include(dp => dp.DesignLead)
            .AsQueryable();

        if (p.CustomerId.HasValue) q = q.Where(dp => dp.CustomerId == p.CustomerId.Value);
        if (p.ContractId.HasValue) q = q.Where(dp => dp.ContractId == p.ContractId.Value);
        if (p.ProjectManagerUserId.HasValue) q = q.Where(dp => dp.ProjectManagerUserId == p.ProjectManagerUserId.Value);
        if (p.DesignLeadUserId.HasValue) q = q.Where(dp => dp.DesignLeadUserId == p.DesignLeadUserId.Value);

        if (!string.IsNullOrWhiteSpace(p.Stage))
        {
            var stages = ParseEnumCsv<DesignProjectStage>(p.Stage);
            if (stages.Count > 0) q = q.Where(dp => stages.Contains(dp.CurrentStage));
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = ParseEnumCsv<DesignProjectStatus>(p.Status);
            if (statuses.Count > 0) q = q.Where(dp => statuses.Contains(dp.Status));
        }

        if (p.DeadlineFrom.HasValue) q = q.Where(dp => dp.Deadline >= p.DeadlineFrom.Value);
        if (p.DeadlineTo.HasValue) q = q.Where(dp => dp.Deadline <= p.DeadlineTo.Value);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim();
            q = q.Where(dp => EF.Functions.Like(dp.Name, $"%{term}%")
                            || EF.Functions.Like(dp.ProjectCode, $"%{term}%"));
        }

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(dp => dp.UpdatedAt)
            .ThenByDescending(dp => dp.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new DesignProjectListResponse
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(MapListItem).ToList(),
        };
    }

    public async Task<DesignProjectResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.DesignProjects
            .AsNoTracking()
            .Include(dp => dp.Customer)
            .Include(dp => dp.Contract)
            .Include(dp => dp.ProjectManager)
            .Include(dp => dp.DesignLead)
            .FirstOrDefaultAsync(dp => dp.Id == id, ct);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<DesignProjectResponse> CreateAsync(CreateDesignProjectRequest request, int callerUserId, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DesignProjectOperationException("Tên dự án là bắt buộc.");
        }

        await EnsureRelationsAsync(request, ct);

        var entity = new DesignProject
        {
            ProjectCode = await NextCodeAsync(ct),
            Name = name,
            CustomerId = request.CustomerId,
            ContractId = request.ContractId,
            ProjectManagerUserId = request.ProjectManagerUserId,
            DesignLeadUserId = request.DesignLeadUserId,
            StartDate = request.StartDate,
            Deadline = request.Deadline,
            CurrentStage = DesignProjectStage.Concept,
            Status = DesignProjectStatus.Active,
            Note = TrimOrNull(request.Note),
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DesignProjects.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("DesignProject {Id} ({Code}) created by user {UserId}",
            entity.Id, entity.ProjectCode, callerUserId);

        await SeedPermitChecklistAsync(entity.Id, callerUserId, ct);

        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<DesignProjectResponse?> UpdateAsync(int id, UpdateDesignProjectRequest request,
        int callerUserId, CancellationToken ct = default)
    {
        var entity = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == id, ct);
        if (entity is null) return null;

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DesignProjectOperationException("Tên dự án là bắt buộc.");
        }

        await EnsureRelationsAsync(request, ct);

        entity.Name = name;
        entity.CustomerId = request.CustomerId;
        entity.ContractId = request.ContractId;
        entity.ProjectManagerUserId = request.ProjectManagerUserId;
        entity.DesignLeadUserId = request.DesignLeadUserId;
        entity.StartDate = request.StartDate;
        entity.Deadline = request.Deadline;
        entity.Note = TrimOrNull(request.Note);

        if (!string.IsNullOrWhiteSpace(request.CurrentStage))
        {
            if (!Enum.TryParse<DesignProjectStage>(request.CurrentStage, true, out var stage))
            {
                throw new DesignProjectOperationException($"Giai đoạn '{request.CurrentStage}' không hợp lệ.");
            }
            entity.CurrentStage = stage;
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<DesignProjectStatus>(request.Status, true, out var status))
            {
                throw new DesignProjectOperationException($"Trạng thái '{request.Status}' không hợp lệ.");
            }
            entity.Status = status;
        }

        entity.UpdatedByUserId = callerUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("DesignProject {Id} updated by user {UserId}", id, callerUserId);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.DesignProjects.FirstOrDefaultAsync(dp => dp.Id == id, ct);
        if (entity is null) return false;

        // Overview-slice guard: refuse to hard-delete a project that has
        // moved beyond Concept. Downstream stages will attach docs +
        // members, so a raw DELETE would blow away referential history.
        // NIH-114+ replaces this with a real "has any doc?" check.
        if (entity.CurrentStage != DesignProjectStage.Concept)
        {
            throw new DesignProjectOperationException(
                "Không thể xoá dự án đã qua giai đoạn Concept. Hãy chuyển trạng thái sang Tạm dừng hoặc Huỷ.");
        }

        db.DesignProjects.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("DesignProject {Id} deleted", id);
        return true;
    }

    public async Task<DesignProjectResponse> EnsureForContractAsync(Contract contract, int? callerUserId, CancellationToken ct = default)
    {
        var existing = await db.DesignProjects
            .FirstOrDefaultAsync(dp => dp.ContractId == contract.Id, ct);
        if (existing is not null)
        {
            return (await GetAsync(existing.Id, ct))!;
        }

        var entity = new DesignProject
        {
            ProjectCode = await NextCodeAsync(ct),
            // Auto-created rows get a predictable, human-friendly name
            // derived from the contract number so the operator can find
            // it in the list without opening the contract. They can
            // rename it later via the edit form.
            Name = $"Dự án hợp đồng {contract.ContractNumber}",
            CustomerId = contract.CustomerId,
            ContractId = contract.Id,
            ProjectManagerUserId = null,
            DesignLeadUserId = null,
            StartDate = contract.StartDate,
            Deadline = contract.EndDate,
            CurrentStage = DesignProjectStage.Concept,
            Status = DesignProjectStatus.Active,
            Note = $"Tạo tự động từ hợp đồng {contract.ContractNumber}.",
            CreatedByUserId = callerUserId,
            UpdatedByUserId = callerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DesignProjects.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "DesignProject {Id} ({Code}) auto-created for contract {ContractId} ({ContractNumber})",
            entity.Id, entity.ProjectCode, contract.Id, contract.ContractNumber);

        await SeedPermitChecklistAsync(entity.Id, callerUserId, ct);

        return (await GetAsync(entity.Id, ct))!;
    }

    // ------------------------------ Helpers ---------------------------------

    /// <summary>
    /// Auto-generate the M3 permit checklist for a freshly created design
    /// project. Best-effort: a downstream failure never blocks the design
    /// project create path (the operator can retry via the "Regenerate"
    /// button on the permits page).
    /// </summary>
    private async Task SeedPermitChecklistAsync(int designProjectId, int? callerUserId, CancellationToken ct)
    {
        try
        {
            await permitChecklistService.EnsureForProjectAsync(designProjectId, callerUserId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to seed permit checklist for design project {ProjectId}", designProjectId);
        }
    }

    private async Task EnsureRelationsAsync(CreateDesignProjectRequest request, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, ct))
        {
            throw new DesignProjectOperationException($"Khách hàng #{request.CustomerId} không tồn tại.");
        }
        if (request.ContractId.HasValue &&
            !await db.Contracts.AnyAsync(c => c.Id == request.ContractId.Value, ct))
        {
            throw new DesignProjectOperationException($"Hợp đồng #{request.ContractId} không tồn tại.");
        }
        if (request.ProjectManagerUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.ProjectManagerUserId.Value, ct))
        {
            throw new DesignProjectOperationException($"PM #{request.ProjectManagerUserId} không tồn tại.");
        }
        if (request.DesignLeadUserId.HasValue &&
            !await db.Users.AnyAsync(u => u.Id == request.DesignLeadUserId.Value, ct))
        {
            throw new DesignProjectOperationException($"Design Lead #{request.DesignLeadUserId} không tồn tại.");
        }
        if (request.StartDate.HasValue && request.Deadline.HasValue &&
            request.Deadline.Value < request.StartDate.Value)
        {
            throw new DesignProjectOperationException("Deadline phải sau ngày bắt đầu.");
        }
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"DP-{year}-";
        var next = 1 + await db.DesignProjects
            .Where(dp => dp.ProjectCode.StartsWith(prefix))
            .CountAsync(ct);
        return $"{prefix}{next:D4}";
    }

    private static List<TEnum> ParseEnumCsv<TEnum>(string csv) where TEnum : struct, Enum
        => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<TEnum>(s, true, out var v) ? (TEnum?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static DesignProjectListItemResponse MapListItem(DesignProject dp) => new()
    {
        Id = dp.Id,
        ProjectCode = dp.ProjectCode,
        Name = dp.Name,
        CustomerId = dp.CustomerId,
        CustomerName = dp.Customer?.Name,
        ContractId = dp.ContractId,
        ContractNumber = dp.Contract?.ContractNumber,
        ProjectManagerUserId = dp.ProjectManagerUserId,
        ProjectManagerName = dp.ProjectManager?.FullName,
        DesignLeadUserId = dp.DesignLeadUserId,
        DesignLeadName = dp.DesignLead?.FullName,
        StartDate = dp.StartDate,
        Deadline = dp.Deadline,
        CurrentStage = dp.CurrentStage.ToString(),
        Status = dp.Status.ToString(),
        UpdatedAt = dp.UpdatedAt,
    };

    private static DesignProjectResponse MapDetail(DesignProject dp) => new()
    {
        Id = dp.Id,
        ProjectCode = dp.ProjectCode,
        Name = dp.Name,
        CustomerId = dp.CustomerId,
        CustomerName = dp.Customer?.Name,
        ContractId = dp.ContractId,
        ContractNumber = dp.Contract?.ContractNumber,
        ProjectManagerUserId = dp.ProjectManagerUserId,
        ProjectManagerName = dp.ProjectManager?.FullName,
        DesignLeadUserId = dp.DesignLeadUserId,
        DesignLeadName = dp.DesignLead?.FullName,
        StartDate = dp.StartDate,
        Deadline = dp.Deadline,
        CurrentStage = dp.CurrentStage.ToString(),
        Status = dp.Status.ToString(),
        Note = dp.Note,
        CreatedAt = dp.CreatedAt,
        UpdatedAt = dp.UpdatedAt,
    };
}
