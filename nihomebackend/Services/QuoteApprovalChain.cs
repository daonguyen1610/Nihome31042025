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
        var stepsJson = await db.WorkflowConfigs.AsNoTracking()
            .Where(w => w.Module == Module && w.Action == Action && w.IsActive)
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
        var configured = (steps ?? [])
            .Where(step => !string.IsNullOrWhiteSpace(step.ApproverRoleCode))
            .OrderBy(step => step.Order)
            .Select(step => new QuoteWorkflowStep(step.Order, (step.Name ?? "").Trim(), step.ApproverRoleCode.Trim(),
                step.SlaHours, step.RequireAllApprovers))
            .ToList();
        return configured.Count > 0 ? configured : [Default];
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
