using NihomeBackend.Constants;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class JobPositionService(
    AppDbContext db,
    RecruitmentMetadataService recruitmentMetadataService)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task<List<JobPositionResponse>> GetAllAsync(bool includeInactive = false)
    {
        var query = db.JobPositions
            .AsNoTracking()
            .Include(j => j.Applications);

        var items = await (includeInactive
            ? query.OrderBy(j => j.SortOrder).ThenBy(j => j.Title)
            : query.Where(j => j.IsActive).OrderBy(j => j.SortOrder).ThenBy(j => j.Title))
            .ToListAsync();

        return items.Select(MapToResponse).ToList();
    }

    public async Task<JobPositionResponse?> GetByIdAsync(int id)
    {
        var entity = await db.JobPositions
            .AsNoTracking()
            .Include(j => j.Applications)
            .FirstOrDefaultAsync(j => j.Id == id);
        return entity == null ? null : MapToResponse(entity);
    }

    public async Task<JobPositionResponse> CreateAsync(UpsertJobPositionRequest req)
    {
        await ValidateMetadataAsync(req);

        var entity = new JobPosition
        {
            Title = req.Title.Trim(),
            Department = req.Department.Trim(),
            Location = req.Location.Trim(),
            EmploymentType = req.EmploymentType,
            ExperienceLevel = req.ExperienceLevel,
            Description = req.Description?.Trim(),
            RequirementsJson = JsonSerializer.Serialize(req.Requirements, JsonOpts),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
        };

        db.JobPositions.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<JobPositionResponse?> UpdateAsync(int id, UpsertJobPositionRequest req)
    {
        var entity = await db.JobPositions.FindAsync(id);
        if (entity == null) return null;

        await ValidateMetadataAsync(req);

        entity.Title = req.Title.Trim();
        entity.Department = req.Department.Trim();
        entity.Location = req.Location.Trim();
        entity.EmploymentType = req.EmploymentType;
        entity.ExperienceLevel = req.ExperienceLevel;
        entity.Description = req.Description?.Trim();
        entity.RequirementsJson = JsonSerializer.Serialize(req.Requirements, JsonOpts);
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    private async Task ValidateMetadataAsync(UpsertJobPositionRequest req)
    {
        await recruitmentMetadataService.EnsureOptionExistsAsync(
            RecruitmentMetadataGroups.EmploymentType,
            req.EmploymentType);
        await recruitmentMetadataService.EnsureOptionExistsAsync(
            RecruitmentMetadataGroups.ExperienceLevel,
            req.ExperienceLevel);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.JobPositions.FindAsync(id);
        if (entity == null) return false;

        db.JobPositions.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static JobPositionResponse MapToResponse(JobPosition j)
    {
        List<string> reqs;
        try { reqs = JsonSerializer.Deserialize<List<string>>(j.RequirementsJson) ?? []; }
        catch { reqs = []; }

        return new JobPositionResponse
        {
            Id = j.Id,
            Title = j.Title,
            Department = j.Department,
            Location = j.Location,
            EmploymentType = j.EmploymentType,
            ExperienceLevel = j.ExperienceLevel,
            Description = j.Description,
            Requirements = reqs,
            IsActive = j.IsActive,
            SortOrder = j.SortOrder,
            ApplicationCount = j.Applications?.Count ?? 0,
        };
    }
}
