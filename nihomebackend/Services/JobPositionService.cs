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
        var query = db.JobPositions.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(j => j.IsActive);
        }

        var items = await query
            .OrderBy(j => j.SortOrder).ThenBy(j => j.Title)
            .Select(j => new { Position = j, ApplicationCount = j.Applications.Count() })
            .ToListAsync();

        var translations = await translationSvc.GetBatchTranslationsAsync(
            EntityTypes.JobPosition, items.Select(x => x.Position.Id), lang);

        return items.Select(x =>
        {
            var t = translations.GetValueOrDefault(x.Position.Id, new Dictionary<string, string>());
            return MapToResponse(x.Position, t, x.ApplicationCount);
        }).ToList();
    }

    public async Task<JobPositionResponse?> GetByIdAsync(int id, string lang = "vi")
    {
        var row = await db.JobPositions
            .AsNoTracking()
            .Where(j => j.Id == id)
            .Select(j => new { Position = j, ApplicationCount = j.Applications.Count() })
            .FirstOrDefaultAsync();
        if (row == null) return null;

        var t = await translationSvc.GetEntityTranslationsAsync(EntityTypes.JobPosition, id, lang);
        return MapToResponse(row.Position, t, row.ApplicationCount);
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
            BenefitsJson = JsonSerializer.Serialize(req.Benefits, JsonOpts),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.JobPositions.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>(), applicationCount: 0);
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
        entity.BenefitsJson = JsonSerializer.Serialize(req.Benefits, JsonOpts);
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity, new Dictionary<string, string>(), applicationCount: 0);
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

    private static JobPositionResponse MapToResponse(JobPosition j, Dictionary<string, string> t, int applicationCount)
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

        List<string> benefits;
        try { benefits = JsonSerializer.Deserialize<List<string>>(j.BenefitsJson) ?? []; }
        catch { benefits = []; }

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
            Benefits = benefits,
            IsActive = j.IsActive,
            SortOrder = j.SortOrder,
            ApplicationCount = applicationCount,
        };
    }
}
