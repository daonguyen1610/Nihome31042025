using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class EmploymentTypeService(AppDbContext db, ILogger<EmploymentTypeService> logger)
{

    public async Task<List<EmploymentTypeResponse>> GetAllAsync(bool includeInactive = false)
    {
        var query = db.EmploymentTypes.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<EmploymentTypeResponse> CreateAsync(UpsertEmploymentTypeRequest req)
    {
        var normalizedCode = NormalizeCode(req.Code);
        var normalizedName = NormalizeName(req.Name);
        await EnsureCodeUniqueAsync(normalizedCode);

        var entity = new EmploymentType
        {
            Code = normalizedCode,
            Name = normalizedName,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.EmploymentTypes.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<EmploymentTypeResponse?> UpdateAsync(int id, UpsertEmploymentTypeRequest req)
    {
        var entity = await db.EmploymentTypes.FindAsync(id);
        if (entity == null)
        {
            return null;
        }

        var previousCode = entity.Code;
        var normalizedCode = NormalizeCode(req.Code);
        var normalizedName = NormalizeName(req.Name);
        await EnsureCodeUniqueAsync(normalizedCode, id);

        entity.Code = normalizedCode;
        entity.Name = normalizedName;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await UpdateLinkedPositionsCodeAsync(previousCode, normalizedCode);

        await db.SaveChangesAsync();
        logger.LogInformation("Updated employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.EmploymentTypes.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        var inUse = await db.JobPositions
            .AsNoTracking()
            .AnyAsync(x => x.EmploymentType.Trim().ToLower() == entity.Code);

        if (inUse)
        {
            throw new InvalidOperationException("Hình thức làm việc đang được sử dụng trong vị trí tuyển dụng, không thể xóa.");
        }

        db.EmploymentTypes.Remove(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
        return true;
    }

    private async Task EnsureCodeUniqueAsync(string code, int? excludingId = null)
    {
        var exists = await db.EmploymentTypes
            .AsNoTracking()
            .AnyAsync(x => x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value));

        if (exists)
        {
            throw new InvalidOperationException("Mã hình thức làm việc đã tồn tại.");
        }
    }

    private async Task UpdateLinkedPositionsCodeAsync(string previousCode, string nextCode)
    {
        if (string.Equals(previousCode, nextCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var previousCodeNormalized = NormalizeCode(previousCode);
        var linkedPositions = await db.JobPositions
            .Where(x => x.EmploymentType.Trim().ToLower() == previousCodeNormalized)
            .ToListAsync();

        foreach (var position in linkedPositions)
        {
            position.EmploymentType = nextCode;
            position.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task EnsureCodeExistsAsync(string code)
    {
        var normalizedCode = NormalizeCode(code);
        var exists = await db.EmploymentTypes
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode);

        if (!exists)
        {
            throw new InvalidOperationException("Hình thức làm việc không hợp lệ.");
        }
    }

    private static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Mã hình thức làm việc không được để trống.");
        }

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeName(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Tên hình thức làm việc không được để trống.");
        }

        return normalized;
    }

    private static EmploymentTypeResponse MapToResponse(EmploymentType item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Name = item.Name,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
