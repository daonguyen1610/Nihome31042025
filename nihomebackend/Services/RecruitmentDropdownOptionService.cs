using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class RecruitmentDropdownOptionService(AppDbContext db, ILogger<RecruitmentDropdownOptionService> logger)
{
    public const string TypeExperienceLevel = "experience-level";
    public const string TypeBenefit = "benefit";

    public async Task<List<RecruitmentDropdownOptionResponse>> GetByTypeAsync(string type, bool includeInactive = false)
    {
        await SeedDefaultsIfEmptyAsync(type);

        var query = db.RecruitmentDropdownOptions
            .AsNoTracking()
            .Where(x => x.Type == type);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<RecruitmentDropdownOptionResponse> CreateAsync(UpsertRecruitmentDropdownOptionRequest req)
    {
        var type = NormalizeType(req.Type);
        var code = NormalizeCode(req.Code);
        var name = NormalizeName(req.Name);
        await EnsureCodeUniqueAsync(type, code);

        var entity = new RecruitmentDropdownOption
        {
            Type = type,
            Code = code,
            Name = name,
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.RecruitmentDropdownOptions.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Created recruitment dropdown option {Id} ({Type}/{Code})", entity.Id, entity.Type, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<RecruitmentDropdownOptionResponse?> UpdateAsync(int id, UpsertRecruitmentDropdownOptionRequest req)
    {
        var entity = await db.RecruitmentDropdownOptions.FindAsync(id);
        if (entity == null)
            return null;

        var type = NormalizeType(req.Type);
        var code = NormalizeCode(req.Code);
        var name = NormalizeName(req.Name);
        await EnsureCodeUniqueAsync(type, code, id);

        entity.Type = type;
        entity.Code = code;
        entity.Name = name;
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Updated recruitment dropdown option {Id} ({Type}/{Code})", entity.Id, entity.Type, entity.Code);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.RecruitmentDropdownOptions.FindAsync(id);
        if (entity == null)
            return false;

        db.RecruitmentDropdownOptions.Remove(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted recruitment dropdown option {Id} ({Type}/{Code})", entity.Id, entity.Type, entity.Code);
        return true;
    }

    private async Task SeedDefaultsIfEmptyAsync(string type)
    {
        var hasAny = await db.RecruitmentDropdownOptions
            .AsNoTracking()
            .AnyAsync(x => x.Type == type);

        if (hasAny)
            return;

        var defaults = type switch
        {
            TypeExperienceLevel => new[]
            {
                new RecruitmentDropdownOption { Type = type, Code = "student", Name = "Sinh viên / Thực tập", IsActive = true, SortOrder = 1 },
                new RecruitmentDropdownOption { Type = type, Code = "junior", Name = "Dưới 1 năm kinh nghiệm", IsActive = true, SortOrder = 2 },
                new RecruitmentDropdownOption { Type = type, Code = "mid", Name = "1 – 3 năm kinh nghiệm", IsActive = true, SortOrder = 3 },
                new RecruitmentDropdownOption { Type = type, Code = "senior", Name = "Trên 3 năm kinh nghiệm", IsActive = true, SortOrder = 4 },
            },
            TypeBenefit => new[]
            {
                new RecruitmentDropdownOption { Type = type, Code = "health-insurance", Name = "Bảo hiểm sức khỏe", IsActive = true, SortOrder = 1 },
                new RecruitmentDropdownOption { Type = type, Code = "training", Name = "Đào tạo & phát triển", IsActive = true, SortOrder = 2 },
                new RecruitmentDropdownOption { Type = type, Code = "friendly-culture", Name = "Môi trường thân thiện", IsActive = true, SortOrder = 3 },
                new RecruitmentDropdownOption { Type = type, Code = "project-bonus", Name = "Thưởng dự án", IsActive = true, SortOrder = 4 },
            },
            _ => Array.Empty<RecruitmentDropdownOption>()
        };

        if (defaults.Length == 0)
            return;

        db.RecruitmentDropdownOptions.AddRange(defaults);
        try
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} defaults for type {Type}", defaults.Length, type);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            db.ChangeTracker.Clear();
            logger.LogInformation("Skipped seeding {Type} options due to concurrent insert race.", type);
        }
    }

    private async Task EnsureCodeUniqueAsync(string type, string code, int? excludingId = null)
    {
        var exists = await db.RecruitmentDropdownOptions
            .AsNoTracking()
            .AnyAsync(x => x.Type == type && x.Code == code &&
                           (!excludingId.HasValue || x.Id != excludingId.Value));

        if (exists)
            throw new InvalidOperationException($"Mã '{code}' đã tồn tại trong loại '{type}'.");
    }

    private static string NormalizeType(string type)
    {
        var normalized = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Loại không được để trống.");
        return normalized;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Mã không được để trống.");
        return normalized;
    }

    private static string NormalizeName(string name)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException("Tên không được để trống.");
        return normalized;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static RecruitmentDropdownOptionResponse MapToResponse(RecruitmentDropdownOption item) => new()
    {
        Id = item.Id,
        Type = item.Type,
        Code = item.Code,
        Name = item.Name,
        IsActive = item.IsActive,
        SortOrder = item.SortOrder,
    };
}
