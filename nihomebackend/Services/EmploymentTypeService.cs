using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class EmploymentTypeService(AppDbContext db)
{
    private ILogger<EmploymentTypeService> Logger => db.GetService<ILoggerFactory>().CreateLogger<EmploymentTypeService>();

    public async Task<List<EmploymentTypeResponse>> GetAllAsync(bool includeInactive = false)
    {
        await SeedFromJobPositionsIfEmptyAsync();

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
        Logger.LogInformation("Created employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
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
        Logger.LogInformation("Updated employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
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
        Logger.LogInformation("Deleted employment type {EmploymentTypeId} ({EmploymentTypeCode})", entity.Id, entity.Code);
        return true;
    }

    private async Task SeedFromJobPositionsIfEmptyAsync()
    {
        if (await db.EmploymentTypes.AsNoTracking().AnyAsync())
        {
            return;
        }

        var typeCodes = await db.JobPositions
            .AsNoTracking()
            .Select(x => x.EmploymentType)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToListAsync();

        var defaults = new[]
        {
            new EmploymentType { Code = "full-time", Name = "Toàn thời gian", IsActive = true, SortOrder = 1 },
            new EmploymentType { Code = "part-time", Name = "Bán thời gian", IsActive = true, SortOrder = 2 },
            new EmploymentType { Code = "intern", Name = "Thực tập sinh", IsActive = true, SortOrder = 3 },
        };

        var normalizedCodes = typeCodes
            .Select(NormalizeCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entities = defaults.ToList();
        var nextSort = entities.Count + 1;

        foreach (var code in normalizedCodes)
        {
            if (entities.Any(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            entities.Add(new EmploymentType
            {
                Code = code,
                Name = code,
                IsActive = true,
                SortOrder = nextSort++,
            });
        }

        db.EmploymentTypes.AddRange(entities);
        try
        {
            await db.SaveChangesAsync();
            Logger.LogInformation("Seeded {Count} employment types", entities.Count);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent first-requests may race on the same defaults.
            // Treat duplicate-key as benign and continue with existing rows.
            db.ChangeTracker.Clear();
            Logger.LogInformation("Skipped employment type seeding due to concurrent insert race.");
        }
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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Number == 2601 || sqlException.Number == 2627;
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
