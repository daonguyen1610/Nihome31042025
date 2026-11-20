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
        var responses = rows.Select(MapToResponse).ToList();
        foreach (var response in responses) await AddQuoteStepIssuesAsync(response, ct);
        return responses;
    }

    public async Task<WorkflowConfigResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.WorkflowConfigs.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, ct);
        return entity == null ? null : await AddQuoteStepIssuesAsync(MapToResponse(entity), ct);
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
        return await AddQuoteStepIssuesAsync(MapToResponse(entity), ct);
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
        return await AddQuoteStepIssuesAsync(MapToResponse(entity), ct);
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

        // The quote workflow is applied when a quote is sent for approval, so every step must
        // be one that someone can decide.
        if (module == QuoteApprovalChain.Module && action == QuoteApprovalChain.Action)
        {
            var issues = await QuoteRoleIssuesAsync(approverRoleCodes, ct);
            var blocking = issues.Values.FirstOrDefault(issue => issue.Issue != NoEligibleUser);
            if (blocking is not null)
            {
                var parameters = new Dictionary<string, object>
                {
                    ["roleLabelKey"] = blocking.RoleLabelKey ?? "",
                    ["roleName"] = blocking.RoleName,
                };
                var name = await SourceLabels.VietnameseAsync(db, blocking.RoleLabelKey, blocking.RoleName, ct);
                throw blocking.Issue == RoleInactive
                    ? new WorkflowConfigValidationException(
                        $"Vai trò {name} đã ngừng dùng nên không ai duyệt được bước này. Chọn vai trò đang dùng, hoặc bật lại vai trò ở Phân quyền.",
                        "QUOTE_WORKFLOW_ROLE_INACTIVE", parameters)
                    : new WorkflowConfigValidationException(
                        $"Vai trò {name} chưa có quyền xem và duyệt báo giá nên không duyệt được bước này. Cấp quyền \"Xem báo giá\" và \"Duyệt báo giá\" cho vai trò này ở Phân quyền, rồi lưu lại luồng.",
                        "QUOTE_WORKFLOW_ROLE_CANNOT_APPROVE", parameters);
            }
            // Everyone in an "everyone approves" step is in the chain once already, so the same
            // role in another step would put each of them in it twice: no quote could be sent.
            var repeated = req.Steps.GroupBy(step => step.ApproverRoleCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1 && group.Any(step => step.RequireAllApprovers));
            if (repeated is not null)
            {
                var role = await db.Roles.AsNoTracking().Where(item => item.Code == repeated.Key)
                    .Select(item => new { item.Name, item.LabelKey }).FirstAsync(ct);
                var name = await SourceLabels.VietnameseAsync(db, role.LabelKey, role.Name, ct);
                throw new WorkflowConfigValidationException(
                    $"Vai trò {name} có bước \"mọi người đều phải duyệt\" nên không lặp lại ở bước khác: mỗi người chỉ duyệt một bước. Gộp các bước của vai trò này làm một.",
                    "QUOTE_WORKFLOW_EVERYONE_ROLE_REPEATED",
                    new Dictionary<string, object> { ["roleLabelKey"] = role.LabelKey ?? "", ["roleName"] = role.Name });
            }
        }

        var duplicate = await db.WorkflowConfigs
            .AsNoTracking()
            .AnyAsync(w => w.Module == module && w.Action == action && (excludeId == null || w.Id != excludeId), ct);
        if (duplicate)
        {
            throw new WorkflowConfigDuplicateException(module, action);
        }
    }

    private const string RoleInactive = "RoleInactive";
    private const string RoleCannotApprove = "RoleCannotApprove";
    private const string NoEligibleUser = "NoEligibleUser";
    private static readonly string[] QuoteApproverActions = ["view", "approve"];

    private sealed record RoleIssue(string RoleCode, string? RoleLabelKey, string RoleName, string Issue);

    /// <summary>
    /// Why nobody can approve a quote step of each role, if so: the role is inactive, lacks
    /// the active quote view/approve grants (the grants the permission check reads), or has
    /// no active member with a usable email.
    /// </summary>
    private async Task<Dictionary<string, RoleIssue>> QuoteRoleIssuesAsync(IReadOnlyCollection<string> roleCodes,
        CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking()
            .Where(role => roleCodes.Contains(role.Code))
            .Select(role => new
            {
                role.Id,
                role.Code,
                role.Name,
                role.LabelKey,
                role.IsActive,
                Grants = db.RolePermissions.Count(grant => grant.RoleId == role.Id && grant.Permission.IsActive &&
                    grant.Permission.Module == "crm.quotes" && QuoteApproverActions.Contains(grant.Permission.Action)),
            }).ToListAsync(ct);
        var roleIds = roles.Select(role => role.Id).ToList();
        var emails = await db.Users.AsNoTracking()
            .Where(user => user.IsActive && user.RoleEntityId != null && roleIds.Contains(user.RoleEntityId.Value))
            .Select(user => new { RoleId = user.RoleEntityId!.Value, user.Email }).ToListAsync(ct);
        var issues = new Dictionary<string, RoleIssue>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            var issue = !role.IsActive ? RoleInactive
                : role.Grants < QuoteApproverActions.Length ? RoleCannotApprove
                : !emails.Any(user => user.RoleId == role.Id && ContactValidation.IsValidEmail(user.Email)) ? NoEligibleUser
                : null;
            if (issue is not null) issues[role.Code] = new RoleIssue(role.Code, role.LabelKey, role.Name, issue);
        }
        return issues;
    }

    private async Task<WorkflowConfigResponse> AddQuoteStepIssuesAsync(WorkflowConfigResponse response, CancellationToken ct)
    {
        if (response.Module != QuoteApprovalChain.Module || response.Action != QuoteApprovalChain.Action)
            return response;
        var issues = await QuoteRoleIssuesAsync(
            response.Steps.Select(step => step.ApproverRoleCode).Distinct().ToList(), ct);
        response.StepIssues = response.Steps
            .Where(step => issues.ContainsKey(step.ApproverRoleCode))
            .Select(step =>
            {
                var issue = issues[step.ApproverRoleCode];
                return new WorkflowStepIssueResponse(step.Order, issue.RoleCode, issue.RoleLabelKey, issue.RoleName, issue.Issue);
            }).ToList();
        return response;
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
