using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Config-only CRUD for approval workflows. NIH-225 explicitly limits the
/// scope to definition — runtime evaluation (routing real records through
/// the chain) belongs to the follow-up stories.
/// </summary>
public class WorkflowConfigService(AppDbContext db, ILogger<WorkflowConfigService> logger)
    : IWorkflowConfigService
{
    private static readonly JsonSerializerOptions StepsJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<WorkflowConfigResponse>> ListAsync(
        bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.WorkflowConfigs.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(w => w.IsActive);
        }

        var rows = await query
            .OrderBy(w => w.Module)
            .ThenBy(w => w.SortOrder)
            .ThenBy(w => w.Action)
            .ToListAsync(ct);
        return rows.Select(MapToResponse).ToList();
    }

    public async Task<WorkflowConfigResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.WorkflowConfigs.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        return entity == null ? null : MapToResponse(entity);
    }

    public async Task<WorkflowConfigResponse> CreateAsync(
        UpsertWorkflowConfigRequest req, CancellationToken ct = default)
    {
        var module = Normalize(req.Module);
        var action = Normalize(req.Action);
        await ValidateAsync(req, module, action, excludeId: null, ct);

        var entity = new WorkflowConfig
        {
            Module = module,
            Action = action,
            Name = req.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim(),
            IsActive = req.IsActive,
            SortOrder = req.SortOrder,
            StepsJson = SerializeSteps(req.Steps),
        };
        db.WorkflowConfigs.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created workflow config {Id} ({Module}/{Action})", entity.Id, entity.Module, entity.Action);
        return MapToResponse(entity);
    }

    public async Task<WorkflowConfigResponse?> UpdateAsync(
        int id, UpsertWorkflowConfigRequest req, CancellationToken ct = default)
    {
        var entity = await db.WorkflowConfigs.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return null;
        }

        var module = Normalize(req.Module);
        var action = Normalize(req.Action);
        await ValidateAsync(req, module, action, excludeId: id, ct);

        entity.Module = module;
        entity.Action = action;
        entity.Name = req.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        entity.IsActive = req.IsActive;
        entity.SortOrder = req.SortOrder;
        entity.StepsJson = SerializeSteps(req.Steps);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated workflow config {Id} ({Module}/{Action})", entity.Id, entity.Module, entity.Action);
        return MapToResponse(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.WorkflowConfigs.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return false;
        }

        db.WorkflowConfigs.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted workflow config {Id} ({Module}/{Action})", entity.Id, entity.Module, entity.Action);
        return true;
    }

    private async Task ValidateAsync(
        UpsertWorkflowConfigRequest req, string module, string action, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(action))
        {
            throw new WorkflowConfigValidationException("Module and action are required.");
        }

        if (req.Steps.Count == 0)
        {
            throw new WorkflowConfigValidationException("Workflow must have at least one step.");
        }

        var orderSet = new HashSet<int>();
        foreach (var s in req.Steps)
        {
            if (!orderSet.Add(s.Order))
            {
                throw new WorkflowConfigValidationException($"Duplicate step order '{s.Order}'.");
            }
        }

        var approverRoleCodes = req.Steps.Select(s => s.ApproverRoleCode.Trim()).Distinct().ToList();
        var knownRoleCodes = await db.Roles
            .AsNoTracking()
            .Where(r => approverRoleCodes.Contains(r.Code))
            .Select(r => r.Code)
            .ToListAsync(ct);
        var unknown = approverRoleCodes.Except(knownRoleCodes).ToList();
        if (unknown.Count > 0)
        {
            throw new WorkflowConfigValidationException(
                $"Unknown approver role code(s): {string.Join(", ", unknown)}.");
        }

        var duplicate = await db.WorkflowConfigs
            .AsNoTracking()
            .AnyAsync(w => w.Module == module && w.Action == action && (excludeId == null || w.Id != excludeId), ct);
        if (duplicate)
        {
            throw new WorkflowConfigDuplicateException(module, action);
        }
    }

    private static string Normalize(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string SerializeSteps(List<WorkflowStepRequest> steps)
    {
        var ordered = steps
            .OrderBy(s => s.Order)
            .Select(s => new WorkflowStepResponse
            {
                Order = s.Order,
                Name = s.Name.Trim(),
                ApproverRoleCode = s.ApproverRoleCode.Trim(),
                SlaHours = s.SlaHours,
                RequireAllApprovers = s.RequireAllApprovers,
                ConditionExpression = string.IsNullOrWhiteSpace(s.ConditionExpression) ? null : s.ConditionExpression.Trim(),
            })
            .ToList();
        return JsonSerializer.Serialize(ordered, StepsJsonOptions);
    }

    private static List<WorkflowStepResponse> DeserializeSteps(string stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return new List<WorkflowStepResponse>();
        try
        {
            return JsonSerializer.Deserialize<List<WorkflowStepResponse>>(stepsJson, StepsJsonOptions)
                   ?? new List<WorkflowStepResponse>();
        }
        catch (JsonException)
        {
            return new List<WorkflowStepResponse>();
        }
    }

    private static WorkflowConfigResponse MapToResponse(WorkflowConfig entity) => new()
    {
        Id = entity.Id,
        Module = entity.Module,
        Action = entity.Action,
        Name = entity.Name,
        Description = entity.Description,
        IsActive = entity.IsActive,
        SortOrder = entity.SortOrder,
        Steps = DeserializeSteps(entity.StepsJson),
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
