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
/// Invalid workflow definitions fail startup before any workflow changes are
/// persisted so approval chains cannot be silently degraded.
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
        Seed(db, stream);
    }

    internal static void Seed(AppDbContext db, Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("workflows", out var workflowsEl) ||
            workflowsEl.ValueKind != JsonValueKind.Array ||
            workflowsEl.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Workflow seed manifest requires a non-empty workflows array.");
        }

        var existingByPair = db.WorkflowConfigs
            .ToDictionary(w => WorkflowKey(w.Module, w.Action), StringComparer.OrdinalIgnoreCase);

        var knownRoleCodes = db.Roles
            .ToDictionary(r => r.Code, r => r.Code, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var toInsert = new List<WorkflowConfig>();
        var definedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasUpdates = false;

        foreach (var wfEl in workflowsEl.EnumerateArray())
        {
            var module = ReadString(wfEl, "module").ToLowerInvariant();
            var action = ReadString(wfEl, "action").ToLowerInvariant();
            var name = ReadString(wfEl, "name");
            if (string.IsNullOrEmpty(module) || string.IsNullOrEmpty(action) || string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException("Workflow seed definitions require module, action, and name.");
            }

            var pairKey = WorkflowKey(module, action);
            if (!definedPairs.Add(pairKey))
            {
                throw new InvalidDataException($"Duplicate workflow seed definition: {pairKey}.");
            }

            if (!wfEl.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Workflow steps must be an array: {pairKey}.");
            }

            var steps = new List<WorkflowStepResponse>();
            var stepOrders = new HashSet<int>();
            foreach (var stepEl in stepsEl.EnumerateArray())
            {
                if (stepEl.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException($"Invalid workflow step definition: {pairKey}.");
                }
                var stepName = ReadString(stepEl, "name");
                var approver = ReadString(stepEl, "approverRoleCode");
                var order = 0;
                var hasValidOrder = stepEl.TryGetProperty("order", out var orderElement)
                    && orderElement.ValueKind == JsonValueKind.Number
                    && orderElement.TryGetInt32(out order);
                if (string.IsNullOrEmpty(stepName) || string.IsNullOrEmpty(approver) ||
                    !hasValidOrder || order <= 0 || !stepOrders.Add(order))
                {
                    throw new InvalidDataException($"Invalid workflow step definition: {pairKey}.");
                }
                if (!knownRoleCodes.TryGetValue(approver, out var canonicalApprover))
                {
                    throw new InvalidDataException(
                        $"Unknown workflow approver role '{approver}': {pairKey}.");
                }

                steps.Add(new WorkflowStepResponse
                {
                    Order = order,
                    Name = stepName,
                    ApproverRoleCode = canonicalApprover,
                    SlaHours = ReadInt(stepEl, "slaHours", 0),
                    RequireAllApprovers = ReadBool(stepEl, "requireAllApprovers", false),
                    ConditionExpression = ReadOptionalString(stepEl, "conditionExpression"),
                });
            }

            if (steps.Count == 0)
            {
                throw new InvalidDataException($"Workflow requires at least one step: {pairKey}.");
            }

            var stepsJson = JsonSerializer.Serialize(steps.OrderBy(s => s.Order).ToList(), StepsJsonOptions);
            if (existingByPair.TryGetValue(pairKey, out var existing))
            {
                var repaired = false;
                if (string.IsNullOrWhiteSpace(existing.Name))
                {
                    existing.Name = name;
                    repaired = true;
                }
                if (HasNoSteps(existing.StepsJson, knownRoleCodes))
                {
                    existing.StepsJson = stepsJson;
                    repaired = true;
                }
                if (repaired)
                {
                    existing.UpdatedAt = now;
                    hasUpdates = true;
                }
                continue;
            }

            var workflow = new WorkflowConfig
            {
                Module = module,
                Action = action,
                Name = name,
                Description = ReadOptionalString(wfEl, "description"),
                IsActive = ReadBool(wfEl, "isActive", true),
                SortOrder = ReadInt(wfEl, "sortOrder", 0),
                StepsJson = stepsJson,
                CreatedAt = now,
                UpdatedAt = now,
            };
            toInsert.Add(workflow);
            existingByPair.Add(pairKey, workflow);
        }

        if (toInsert.Count > 0)
        {
            db.WorkflowConfigs.AddRange(toInsert);
            hasUpdates = true;
        }

        if (hasUpdates) db.SaveChanges();
    }

    private static string WorkflowKey(string module, string action) =>
        $"{module.Trim()}|{action.Trim()}";

    private static bool HasNoSteps(string? stepsJson,
        IReadOnlyDictionary<string, string> knownRoleCodes)
    {
        if (string.IsNullOrWhiteSpace(stepsJson)) return true;
        try
        {
            using var document = JsonDocument.Parse(stepsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                return true;
            }

            var orders = new HashSet<int>();
            foreach (var step in document.RootElement.EnumerateArray())
            {
                if (step.ValueKind != JsonValueKind.Object ||
                    !step.TryGetProperty("name", out var name) ||
                    name.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(name.GetString()) ||
                    !step.TryGetProperty("approverRoleCode", out var approverRoleCode) ||
                    approverRoleCode.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(approverRoleCode.GetString()) ||
                    !knownRoleCodes.ContainsKey(approverRoleCode.GetString()!) ||
                    !step.TryGetProperty("order", out var order) ||
                    !order.TryGetInt32(out var orderValue) ||
                    orderValue <= 0 ||
                    !orders.Add(orderValue))
                {
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return true;
        }
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
