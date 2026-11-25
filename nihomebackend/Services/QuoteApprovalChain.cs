using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;

namespace NihomeBackend.Services;

/// <summary>A step of the quote workflow as configured in /admin/workflows.</summary>
public sealed record QuoteWorkflowStep(int Order, string Name, string RoleCode, int SlaHours, bool EveryoneApproves);

/// <summary>
/// One decision in a quote's approval chain, fixed when the quote is sent for approval.
/// A step where everyone in the role approves becomes one entry per person.
/// </summary>
public sealed record QuoteApprovalStep(int Order, string Name, string RoleCode, string? RoleLabelKey,
    int SlaHours, int ApproverUserId, string ApproverName, DateTime? StartedAt, DateTime? DecidedAt);

/// <summary>
/// The quote approval chain comes from the "quotes / approve" workflow. Without an active
/// workflow with steps, the chosen Procurement Manager approves alone (NICON MoM 31/10).
/// </summary>
public static class QuoteApprovalChain
{
    public const string Module = "quotes";
    public const string Action = "approve";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly QuoteWorkflowStep Default =
        new(1, "Trưởng phòng Cung ứng được chọn duyệt", RbacRoleCodes.ProcurementManager, 24, false);

    public static async Task<IReadOnlyList<QuoteWorkflowStep>> ConfiguredAsync(AppDbContext db, CancellationToken ct)
    {
        var configured = await ConfiguredAsync(db, Module, Action, ct);
        return configured.Count > 0 ? configured : [Default];
    }

    /// <summary>
    /// The steps of an active workflow in Cấu hình → Luồng duyệt, in order; empty when the
    /// workflow is missing, inactive or has no usable step. Contract review reads "contracts / sign".
    /// </summary>
    public static async Task<IReadOnlyList<QuoteWorkflowStep>> ConfiguredAsync(AppDbContext db, string module,
        string action, CancellationToken ct)
    {
        var stepsJson = await db.WorkflowConfigs.AsNoTracking()
            .Where(w => w.Module == module && w.Action == action && w.IsActive)
            .Select(w => w.StepsJson).FirstOrDefaultAsync(ct);
        List<WorkflowStepResponse>? steps = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(stepsJson))
                steps = JsonSerializer.Deserialize<List<WorkflowStepResponse>>(stepsJson, Json);
        }
        catch (JsonException)
        {
            steps = null;
        }
        return (steps ?? [])
            .Where(step => !string.IsNullOrWhiteSpace(step.ApproverRoleCode))
            .OrderBy(step => step.Order)
            .Select(step => new QuoteWorkflowStep(step.Order, (step.Name ?? "").Trim(), step.ApproverRoleCode.Trim(),
                step.SlaHours, step.RequireAllApprovers))
            .ToList();
    }

    /// <summary>
    /// Each step with the people who may decide it: active, in the step's active role, holding
    /// every permission code, and not one of <paramref name="excludedUserIds"/> (the sender).
    /// </summary>
    public static async Task<IReadOnlyList<QuoteApprovalChainStepResponse>> WithCandidatesAsync(AppDbContext db,
        IPermissionService permissions, IReadOnlyList<QuoteWorkflowStep> steps, IReadOnlyCollection<int> excludedUserIds,
        IReadOnlyCollection<string> permissionCodes, CancellationToken ct)
    {
        var codes = steps.Select(step => step.RoleCode).Distinct().ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive && codes.Contains(r.Code))
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.Name,
                r.LabelKey,
                DepartmentId = r.RoleGroup != null && r.RoleGroup.IsActive ? r.RoleGroupId : null,
                DepartmentLabelKey = r.RoleGroup != null && r.RoleGroup.IsActive ? r.RoleGroup.LabelKey : null
            }).ToListAsync(ct);
        var roleIds = roles.Select(role => role.Id).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !excludedUserIds.Contains(u.Id) && u.RoleEntityId != null &&
                roleIds.Contains(u.RoleEntityId.Value))
            .Select(u => new { u.Id, u.FullName, u.Email, RoleId = u.RoleEntityId!.Value }).ToListAsync(ct);
        var candidatesByRole = roles.ToDictionary(role => role.Id, _ => new List<QuoteApprovalCandidateResponse>());
        var canDecide = await CanDecideAsync(db, permissions, users
            .Select(user => (user.Id, roles.Single(role => role.Id == user.RoleId).Code)).ToList(), permissionCodes, ct);
        foreach (var user in users)
        {
            var role = roles.Single(item => item.Id == user.RoleId);
            if (!canDecide.Contains((user.Id, role.Code))) continue;
            candidatesByRole[role.Id].Add(new QuoteApprovalCandidateResponse(user.Id, user.FullName ?? user.Email,
                user.Email, role.Code, role.Name, role.LabelKey, role.DepartmentId, role.DepartmentLabelKey));
        }
        return steps.Select(step =>
        {
            var role = roles.FirstOrDefault(item => string.Equals(item.Code, step.RoleCode, StringComparison.OrdinalIgnoreCase));
            return new QuoteApprovalChainStepResponse(step.Order, step.Name, step.RoleCode, role?.Name ?? step.RoleCode,
                role?.LabelKey, step.SlaHours, step.EveryoneApproves,
                role is null ? [] : candidatesByRole[role.Id].OrderBy(user => user.Name).ToList());
        }).ToList();
    }

    /// <summary>
    /// Of the given approvers (each with the role their step needs), those who can decide that
    /// step now: active, in that active role, holding every permission code. An empty role code
    /// is a person tagged by name, who needs only to be active with the permissions.
    /// </summary>
    public static async Task<IReadOnlySet<(int UserId, string RoleCode)>> CanDecideAsync(AppDbContext db,
        IPermissionService permissions, IReadOnlyCollection<(int UserId, string RoleCode)> approvers,
        IReadOnlyCollection<string> permissionCodes, CancellationToken ct)
    {
        var result = new HashSet<(int UserId, string RoleCode)>();
        if (approvers.Count == 0) return result;
        var ids = approvers.Select(approver => approver.UserId).Distinct().ToList();
        var roles = await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.IsActive && user.RoleEntity != null && user.RoleEntity.IsActive)
            .Select(user => new { user.Id, user.RoleEntity!.Code }).ToListAsync(ct);
        var permitted = await permissions.FilterByRolePermissionsAsync(ids, permissionCodes, ct);
        foreach (var approver in approvers)
        {
            if (permitted.Contains(approver.UserId) &&
                roles.Any(role => role.Id == approver.UserId && (approver.RoleCode.Length == 0 ||
                    string.Equals(role.Code, approver.RoleCode, StringComparison.OrdinalIgnoreCase))))
                result.Add(approver);
        }
        return result;
    }

    public static List<QuoteApprovalStep> Read(string? stepsJson)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<QuoteApprovalStep>>(stepsJson, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Each step with its state (Approved, Current or Waiting) and deadline.</summary>
    public static List<QuoteApprovalStepResponse> MapSteps(string? stepsJson, bool waiting, int? currentIndex) =>
        Read(stepsJson).Select((step, index) => new QuoteApprovalStepResponse(step.Order, step.Name, step.RoleCode,
            step.RoleLabelKey, step.ApproverUserId, step.ApproverName,
            step.DecidedAt is not null ? "Approved" : waiting && index == currentIndex ? "Current" : "Waiting",
            step.DecidedAt, step.StartedAt?.AddHours(step.SlaHours))).ToList();

    public static string Write(IEnumerable<QuoteApprovalStep> steps) => JsonSerializer.Serialize(steps, Json);

    /// <summary>
    /// Who may read a quote without permission to see every quote: its owner, its current
    /// approver, and everyone in its approval chain, before, at and after their turn
    /// (<see cref="QuoteChainApprover"/>). Changes stay with the owner.
    /// </summary>
    public static async Task<bool> CanReadAsync(AppDbContext db, int quoteId, int? ownerUserId,
        int? approverUserId, int userId, CancellationToken ct) =>
        ownerUserId == userId || approverUserId == userId ||
        await db.QuoteChainApprovers.AsNoTracking()
            .AnyAsync(item => item.QuoteId == quoteId && item.UserId == userId, ct);

    /// <summary>Records everyone in a chain just sent, replacing any earlier chain of the quote.</summary>
    public static async Task SetApproversAsync(AppDbContext db, Quote quote, IEnumerable<QuoteApprovalStep> steps,
        CancellationToken ct)
    {
        await ClearApproversAsync(db, quote.Id, ct);
        foreach (var userId in steps.Select(step => step.ApproverUserId).Distinct())
            db.QuoteChainApprovers.Add(new QuoteChainApprover { QuoteId = quote.Id, UserId = userId });
    }

    /// <summary>Stages removing a quote's chain members, saved with the change that ends the chain.</summary>
    public static async Task ClearApproversAsync(AppDbContext db, int quoteId, CancellationToken ct) =>
        db.QuoteChainApprovers.RemoveRange(
            await db.QuoteChainApprovers.Where(item => item.QuoteId == quoteId).ToListAsync(ct));

    /// <summary>The role that decides now: the current step's, or the Procurement Manager for a quote sent before chains.</summary>
    public static string CurrentRoleCode(string? stepsJson, int? stepIndex)
    {
        var steps = Read(stepsJson);
        return stepIndex is int index && index >= 0 && index < steps.Count
            ? steps[index].RoleCode
            : RbacRoleCodes.ProcurementManager;
    }
}
