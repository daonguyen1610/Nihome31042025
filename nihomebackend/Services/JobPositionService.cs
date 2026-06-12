using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class JobPositionService(
    AppDbContext db,
    EmploymentTypeService employmentTypeService,
    EntityTranslationService translationSvc)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task<List<JobPositionResponse>> GetAllAsync(bool includeInactive = false, string lang = "vi")
    {
        var query = db.JobPositions
            .AsNoTracking()
            .Include(j => j.Applications);

        var items = await (includeInactive
            ? query.OrderBy(j => j.SortOrder).ThenBy(j => j.Title)
            : query.Where(j => j.IsActive).OrderBy(j => j.SortOrder).ThenBy(j => j.Title))
            .ToListAsync();

        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.JobPosition, items.Select(j => j.Id), lang);

        return items.Select(j =>
        {
            var t = translations.GetValueOrDefault(j.Id, new Dictionary<string, string>());
            return MapToResponse(j, t);
        }).ToList();
    }

    public async Task<JobPositionResponse?> GetByIdAsync(int id, string lang = "vi")
    {
        var entity = await db.JobPositions
            .AsNoTracking()
            .Include(j => j.Applications)
            .FirstOrDefaultAsync(j => j.Id == id);
        if (entity == null) return null;

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.JobPosition, id, lang);
        return MapToResponse(entity, t);
    }

    public async Task<JobPositionResponse> CreateAsync(UpsertJobPositionRequest req)
    {
        await employmentTypeService.EnsureCodeExistsAsync(req.EmploymentType);
        var normalizedEmploymentType = req.EmploymentType.Trim().ToLowerInvariant();

        var entity = new JobPosition
        {
            Title = req.Title.Trim(),
            Department = req.Department.Trim(),
            Location = req.Location.Trim(),
            EmploymentType = normalizedEmploymentType,
            ExperienceLevel = req.ExperienceLevel,
            Description = req.Description?.Trim(),
            RequirementsJson = JsonSerializer.Serialize(req.Requirements, JsonOpts),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.JobPositions.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<JobPositionResponse?> UpdateAsync(int id, UpsertJobPositionRequest req)
    {
        await employmentTypeService.EnsureCodeExistsAsync(req.EmploymentType);
        var normalizedEmploymentType = req.EmploymentType.Trim().ToLowerInvariant();

        var entity = await db.JobPositions.FindAsync(id);
        if (entity == null) return null;

        entity.Title = req.Title.Trim();
        entity.Department = req.Department.Trim();
        entity.Location = req.Location.Trim();
        entity.EmploymentType = normalizedEmploymentType;
        entity.ExperienceLevel = req.ExperienceLevel;
        entity.Description = req.Description?.Trim();
        entity.RequirementsJson = JsonSerializer.Serialize(req.Requirements, JsonOpts);
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.JobPositions.FindAsync(id);
        if (entity == null) return false;

        db.JobPositions.Remove(entity);
        await db.SaveChangesAsync();
        await translationSvc.DeleteEntityTranslationsAsync(EntityTypes.JobPosition, id);
        return true;
    }

    private static JobPositionResponse MapToResponse(JobPosition j, Dictionary<string, string> t)
    {
        List<string> reqs;
        try
        {
            var rawReqs = t.TryGetValue("Requirements", out var reqJson)
                ? reqJson
                : j.RequirementsJson;
            reqs = JsonSerializer.Deserialize<List<string>>(rawReqs) ?? [];
        }
        catch { reqs = []; }

        return new JobPositionResponse
        {
            Id = j.Id,
            Title = t.GetValueOrDefault("Title", j.Title),
            Department = t.GetValueOrDefault("Department", j.Department),
            Location = j.Location,
            EmploymentType = j.EmploymentType,
            ExperienceLevel = j.ExperienceLevel,
            Description = t.TryGetValue("Description", out var desc) ? desc : j.Description,
            Requirements = reqs,
            IsActive = j.IsActive,
            SortOrder = j.SortOrder,
            ApplicationCount = j.Applications?.Count ?? 0,
        };
    }
}
