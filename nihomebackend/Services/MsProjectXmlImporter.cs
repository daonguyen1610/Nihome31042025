using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class MsProjectImportException(string message) : Exception(message);

/// <summary>An MS Project schedule read into master-schedule rows, plus what was adjusted.</summary>
public sealed record MsProjectImport(List<SaveProjectScheduleItemRequest> Items, List<MsProjectImportWarning> Warnings);

/// <summary>
/// Reads an MS Project XML file (File → Save As → XML, the MSPDI format) into master-schedule
/// rows, as NICON asked (PRD v2 answer 11): outline levels and WBS, names, dates, milestones,
/// FS/SS/FF/SF links with lag, and baseline dates. Resources and costs are not read. Nothing is
/// saved here: the rows become a draft the user reviews, saves and sends for approval. Rows the
/// master schedule would refuse on save are adjusted here, each with a warning.
/// </summary>
public static class MsProjectXmlImporter
{
    public const int MaxItems = 500;
    public const int MaxLinksPerItem = 50;
    public const int MaxLagDays = 3650;
    private const int TitleLimit = 300;
    private const int WbsLimit = 80;
    private const decimal DefaultMinutesPerDay = 480;
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/project";

    // MSPDI link types: 0 FF, 1 FS, 2 SF, 3 SS.
    private static readonly string[] LinkTypes = ["FF", "FS", "SF", "SS"];

    public static MsProjectImport Parse(Stream xml)
    {
        XDocument document;
        try
        {
            // No DTDs and no external resolution: an uploaded file cannot reach other files or hosts.
            using var reader = XmlReader.Create(xml, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            throw new MsProjectImportException(
                "Không đọc được tệp. Trong MS Project, chọn File → Save As → XML rồi tải tệp .xml đó lên.");
        }

        var root = document.Root;
        var tasks = root?.Element(Ns + "Tasks")?.Elements(Ns + "Task")
            .Where(task => Text(task, "IsNull") != "1" && Level(task) > 0 && !string.IsNullOrWhiteSpace(Text(task, "Name")))
            .ToList();
        if (root?.Name != Ns + "Project" || tasks is null)
            throw new MsProjectImportException(
                "Tệp không phải tiến độ MS Project dạng XML. Trong MS Project, chọn File → Save As → XML.");
        if (tasks.Count == 0) throw new MsProjectImportException("Tệp MS Project không có công việc nào.");
        if (tasks.Count > MaxItems)
            throw new MsProjectImportException(
                $"Tệp có {tasks.Count} công việc; mỗi phiên bản tiến độ tối đa {MaxItems}. Hãy gộp bớt các công việc chi tiết.");
        var uids = tasks.Select(task => Text(task, "UID").Trim()).ToList();
        if (uids.Any(uid => uid.Length == 0) || uids.Distinct().Count() != uids.Count)
            throw new MsProjectImportException(
                "Tệp có công việc thiếu hoặc trùng mã UID. Hãy mở lại tệp trong MS Project rồi Save As → XML lần nữa.");

        var minutesPerDay = decimal.TryParse(Text(root, "MinutesPerDay"), NumberStyles.Number, CultureInfo.InvariantCulture,
            out var minutes) && minutes > 0 ? minutes : DefaultMinutesPerDay;
        var warnings = new List<MsProjectImportWarning>();
        var nodeByUid = uids.ToDictionary(uid => uid, _ => Guid.NewGuid());
        var parents = new Stack<(int Level, Guid NodeId)>();
        var items = new List<SaveProjectScheduleItemRequest>();
        var usedWbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (task, index) in tasks.Select((task, index) => (task, index)))
        {
            var level = Level(task);
            while (parents.Count > 0 && parents.Peek().Level >= level) parents.Pop();
            var nodeId = nodeByUid[uids[index]];
            var name = Text(task, "Name").Trim();
            var wbs = FirstNonEmpty(Text(task, "WBS"), Text(task, "OutlineNumber"), (index + 1).ToString(CultureInfo.InvariantCulture));
            if (name.Length > TitleLimit)
            {
                warnings.Add(new MsProjectImportWarning("titleTruncated", wbs));
                name = name[..TitleLimit];
            }
            if (wbs.Length > WbsLimit || usedWbs.Contains(wbs))
            {
                var replacement = UniqueCode(usedWbs, index + 1);
                warnings.Add(new MsProjectImportWarning("wbsReplaced", wbs, replacement));
                wbs = replacement;
            }
            usedWbs.Add(wbs);

            var start = Date(task, "Start") ?? throw new MsProjectImportException($"{wbs} {name}: thiếu ngày bắt đầu.");
            var end = Date(task, "Finish") ?? start;
            if (end < start) end = start;

            var baseline = task.Elements(Ns + "Baseline").FirstOrDefault(item => Text(item, "Number") == "0");
            var baselineStart = baseline is null ? null : Date(baseline, "Start");
            var baselineEnd = baseline is null ? null : Date(baseline, "Finish");
            if (baselineStart is null || baselineEnd is null || baselineEnd < baselineStart)
                (baselineStart, baselineEnd) = (null, null);

            var links = new List<SaveProjectScheduleLinkRequest>();
            foreach (var link in task.Elements(Ns + "PredecessorLink"))
            {
                if (!nodeByUid.TryGetValue(Text(link, "PredecessorUID").Trim(), out var predecessor) || predecessor == nodeId)
                {
                    warnings.Add(new MsProjectImportWarning("linkMissing", wbs));
                    continue;
                }
                var type = int.TryParse(Text(link, "Type"), out var code) && code is >= 0 and <= 3 ? LinkTypes[code] : "FS";
                if (links.Any(existing => existing.PredecessorNodeId == predecessor)) continue;
                if (links.Count == MaxLinksPerItem)
                {
                    warnings.Add(new MsProjectImportWarning("linksCapped", wbs, MaxLinksPerItem.ToString(CultureInfo.InvariantCulture)));
                    break;
                }
                links.Add(new SaveProjectScheduleLinkRequest
                {
                    PredecessorNodeId = predecessor,
                    Type = type,
                    LagDays = LagDays(link, minutesPerDay, wbs, warnings),
                });
            }

            items.Add(new SaveProjectScheduleItemRequest
            {
                NodeId = nodeId,
                ParentNodeId = parents.Count > 0 ? parents.Peek().NodeId : null,
                WbsCode = wbs,
                Title = name,
                PlannedStart = start,
                PlannedEnd = end,
                SortOrder = index,
                IsMilestone = Text(task, "Milestone") == "1",
                BaselineStart = baselineStart,
                BaselineEnd = baselineEnd,
                Predecessors = links,
            });
            parents.Push((level, nodeId));
        }
        WidenParents(items, warnings);
        return new MsProjectImport(items, warnings);
    }

    /// <summary>
    /// MS Project lets a manually scheduled summary end before its tasks; the master schedule
    /// needs every child inside its parent, so such a parent is widened (deepest first).
    /// </summary>
    private static void WidenParents(List<SaveProjectScheduleItemRequest> items, List<MsProjectImportWarning> warnings)
    {
        var byId = items.ToDictionary(item => item.NodeId!.Value);
        int Depth(SaveProjectScheduleItemRequest item)
        {
            var depth = 0;
            for (var current = item; current.ParentNodeId.HasValue; current = byId[current.ParentNodeId.Value]) depth++;
            return depth;
        }
        foreach (var child in items.OrderByDescending(Depth).Where(item => item.ParentNodeId.HasValue))
        {
            var parent = byId[child.ParentNodeId!.Value];
            if (child.PlannedStart >= parent.PlannedStart && child.PlannedEnd <= parent.PlannedEnd) continue;
            if (child.PlannedStart < parent.PlannedStart) parent.PlannedStart = child.PlannedStart;
            if (child.PlannedEnd > parent.PlannedEnd) parent.PlannedEnd = child.PlannedEnd;
            if (!warnings.Any(warning => warning.Code == "parentWidened" && warning.Wbs == parent.WbsCode))
                warnings.Add(new MsProjectImportWarning("parentWidened", parent.WbsCode));
        }
    }

    private static string UniqueCode(HashSet<string> used, int seed)
    {
        for (var number = seed; ; number++)
        {
            var code = number.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(code)) return code;
        }
    }

    private static string Text(XElement element, string name) => element.Element(Ns + name)?.Value ?? string.Empty;

    private static int Level(XElement task) =>
        int.TryParse(Text(task, "OutlineLevel"), out var level) ? level : 1;

    private static string FirstNonEmpty(params string[] values) =>
        values.Select(value => value.Trim()).First(value => value.Length > 0);

    private static DateOnly? Date(XElement element, string name) =>
        DateTime.TryParse(Text(element, name), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? DateOnly.FromDateTime(value)
            : null;

    /// <summary>
    /// MSPDI keeps lag in tenths of a minute. Elapsed formats (even codes up to 12) count 24-hour
    /// days; the others count the project's working day (<c>MinutesPerDay</c>, 8 hours by default).
    /// A percentage lag (formats 19, 20) has no length in days, so it becomes 0 with a warning.
    /// </summary>
    private static int LagDays(XElement link, decimal minutesPerDay, string wbs, List<MsProjectImportWarning> warnings)
    {
        if (!long.TryParse(Text(link, "LinkLag"), out var tenths) || tenths == 0) return 0;
        var format = int.TryParse(Text(link, "LagFormat"), out var value) ? value : 7;
        if (format is 19 or 20)
        {
            warnings.Add(new MsProjectImportWarning("lagPercent", wbs));
            return 0;
        }
        var elapsed = format is >= 4 and <= 12 && format % 2 == 0;
        var days = (int)Math.Round(tenths / (elapsed ? 14_400m : minutesPerDay * 10m), MidpointRounding.AwayFromZero);
        if (Math.Abs(days) <= MaxLagDays) return days;
        warnings.Add(new MsProjectImportWarning("lagClamped", wbs, MaxLagDays.ToString(CultureInfo.InvariantCulture)));
        return Math.Sign(days) * MaxLagDays;
    }
}
