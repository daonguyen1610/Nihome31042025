using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public class ProcessService(AppDbContext db)
{
    public async Task<Dictionary<string, List<ProcessResponse>>> GetAllGroupedAsync()
    {
        var all = await db.ProcessDocuments.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        return all.GroupBy(p => p.GroupKey)
            .ToDictionary(g => g.Key, g => g.Select(MapToResponse).ToList());
    }

    public async Task<ProcessResponse> CreateAsync(UpsertProcessRequest req)
    {
        var entity = new ProcessDocument
        {
            GroupKey = req.GroupKey,
            Code = req.Code,
            Title = req.Title,
            SortOrder = req.SortOrder,
        };
        db.ProcessDocuments.Add(entity);
        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<ProcessResponse?> UpdateAsync(int id, UpsertProcessRequest req)
    {
        var entity = await db.ProcessDocuments.FindAsync(id);
        if (entity == null) return null;

        entity.GroupKey = req.GroupKey;
        entity.Code = req.Code;
        entity.Title = req.Title;
        entity.SortOrder = req.SortOrder;

        await db.SaveChangesAsync();
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.ProcessDocuments.FindAsync(id);
        if (entity == null) return false;
        db.ProcessDocuments.Remove(entity);
        await db.SaveChangesAsync();
        return true;
    }

    private static ProcessResponse MapToResponse(ProcessDocument p) => new()
    {
        Id = p.Id,
        GroupKey = p.GroupKey,
        Code = p.Code,
        Title = p.Title,
    };
}
