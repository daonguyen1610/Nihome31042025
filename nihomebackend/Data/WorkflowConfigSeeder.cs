using System.Reflection;
using System.Text.Json;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Data;

/// <summary>
/// Idempotent bootstrap of the approval-workflow catalogue defined by
/// NIH-225. Loads <c>Data/Seeds/workflows/defaults.json</c> at every startup
/// and inserts <c>(module, action)</c> pairs that are missing. Existing rows
/// are never overwritten so admin edits made through the UI survive reboots.
///
/// Any step whose <c>approverRoleCode</c> is not present in the RBAC table
/// is skipped for that workflow — this keeps the seed safe when a role gets
/// removed downstream.
/// </summary>
public static class WorkflowConfigSeeder
{
    private static readonly JsonSerializerOptions StepsJsonOptions = new(JsonSerializerDefaults.Web);

    public static void Seed(AppDbContext db) => Seed(db, typeof(WorkflowConfigSeeder).Assembly);

    public static void Seed(AppDbContext db, Assembly assembly)
    {
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(".workflows.defaults.json", StringComparison.OrdinalIgnoreCase));
        if (resource == null)
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("workflows", out var workflowsEl) ||
            workflowsEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var existingPairs = db.WorkflowConfigs
            .Select(w => new { w.Module, w.Action })
            .ToHashSet();

        var knownRoleCodes = db.Roles.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var toInsert = new List<WorkflowConfig>();

        foreach (var wfEl in workflowsEl.EnumerateArray())
        {
            var module = ReadString(wfEl, "module").ToLowerInvariant();
            var action = ReadString(wfEl, "action").ToLowerInvariant();
            var name = ReadString(wfEl, "name");
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(action) || string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (existingPairs.Contains(new { Module = module, Action = action }))
            {
                continue;
            }

            if (!wfEl.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var steps = new List<WorkflowStepResponse>();
            var stepIndex = 0;
            foreach (var stepEl in stepsEl.EnumerateArray())
            {
                stepIndex++;
                var stepName = ReadString(stepEl, "name");
                var approver = ReadString(stepEl, "approverRoleCode");
                if (string.IsNullOrEmpty(stepName) || string.IsNullOrEmpty(approver))
                {
                    continue;
                }
                if (!knownRoleCodes.Contains(approver))
                {
                    // Silently skip: the role was probably removed from the RBAC
                    // seed; don't block the whole workflow over one dangling ref.
                    continue;
                }

                steps.Add(new WorkflowStepResponse
                {
                    Order = ReadInt(stepEl, "order", stepIndex),
                    Name = stepName,
                    ApproverRoleCode = approver,
                    SlaHours = ReadInt(stepEl, "slaHours", 0),
                    RequireAllApprovers = ReadBool(stepEl, "requireAllApprovers", false),
                    ConditionExpression = ReadOptionalString(stepEl, "conditionExpression"),
                });
            }

            if (steps.Count == 0)
            {
                continue;
            }

            toInsert.Add(new WorkflowConfig
            {
                Module = module,
                Action = action,
                Name = name,
                Description = ReadOptionalString(wfEl, "description"),
                IsActive = ReadBool(wfEl, "isActive", true),
                SortOrder = ReadInt(wfEl, "sortOrder", 0),
                StepsJson = JsonSerializer.Serialize(steps.OrderBy(s => s.Order).ToList(), StepsJsonOptions),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        if (toInsert.Count == 0)
        {
            return;
        }

        db.WorkflowConfigs.AddRange(toInsert);
        db.SaveChanges();
    }

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) ? (prop.GetString() ?? string.Empty).Trim() : string.Empty;

    private static string? ReadOptionalString(JsonElement el, string name)
    {
        var value = ReadString(el, name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static int ReadInt(JsonElement el, string name, int fallback) =>
        el.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value) ? value : fallback;

    private static bool ReadBool(JsonElement el, string name, bool fallback) =>
        el.TryGetProperty(name, out var prop) && (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            ? prop.GetBoolean()
            : fallback;
}
