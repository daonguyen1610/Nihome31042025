using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>
/// Who approves finishing each tender stage comes from the "tenders / advance-stage" workflow in
/// Luồng duyệt: its steps, in order, are the stages Nhận thông tin, Lập kế hoạch đấu thầu,
/// Triển khai lập hồ sơ thầu and Đánh giá hồ sơ thầu. Nộp thầu ends with the recorded submission
/// and Kết quả with Mark Won/Lost, so neither needs a stage approval.
/// </summary>
public static class TenderStageFlow
{
    public const string Module = "tenders";
    public const string Action = "advance-stage";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool NeedsApproval(TenderStage stage) => stage < TenderStage.Submission;

    /// <summary>The role that approves finishing <paramref name="stage"/>, or null when Luồng duyệt has no active step for it.</summary>
    public static async Task<string?> ApproverRoleAsync(AppDbContext db, TenderStage stage, CancellationToken ct)
    {
        if (!NeedsApproval(stage)) return null;
        var stepsJson = await db.WorkflowConfigs.AsNoTracking()
            .Where(w => w.Module == Module && w.Action == Action && w.IsActive)
            .Select(w => w.StepsJson).FirstOrDefaultAsync(ct);
        List<WorkflowStepResponse>? steps;
        try
        {
            steps = string.IsNullOrWhiteSpace(stepsJson)
                ? null
                : JsonSerializer.Deserialize<List<WorkflowStepResponse>>(stepsJson, Json);
        }
        catch (JsonException)
        {
            steps = null;
        }
        // shortcut: one approver per stage, the step's "everyone approves" flag is ignored; upgrade when N-06's shared approval engine lands.
        var role = steps?.OrderBy(step => step.Order).ElementAtOrDefault((int)stage)?.ApproverRoleCode?.Trim();
        return string.IsNullOrEmpty(role) ? null : role;
    }

    /// <summary>Vietnamese stage names for notifications, which render in Vietnamese.</summary>
    public static string Label(TenderStage stage) => stage switch
    {
        TenderStage.Received => "Nhận thông tin",
        TenderStage.Planning => "Lập kế hoạch đấu thầu",
        TenderStage.Preparing => "Triển khai lập hồ sơ thầu",
        TenderStage.Evaluating => "Đánh giá hồ sơ thầu",
        TenderStage.Submission => "Nộp thầu",
        _ => "Kết quả đấu thầu",
    };
}
