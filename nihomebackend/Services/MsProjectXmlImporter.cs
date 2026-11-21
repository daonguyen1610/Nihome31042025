using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Services;

public sealed class MsProjectImportException(string message) : Exception(message);

/// <summary>An MS Project schedule read into master-schedule rows, plus what was adjusted.</summary>
public sealed record MsProjectImport(List<SaveProjectScheduleItemRequest> Items, List<string> Warnings);

/// <summary>
/// Reads an MS Project XML file (File → Save As → XML, the MSPDI format) into master-schedule
/// rows, as NICON asked (PRD v2 answer 11): outline levels and WBS, names, dates, milestones,
/// FS/SS/FF/SF links with lag, and baseline dates. Resources and costs are not read. Nothing is
/// saved here: the rows become a draft the user reviews, saves and sends for approval.
/// </summary>
public static class MsProjectXmlImporter
{
    public const int MaxItems = 500;
    private const int TitleLimit = 300;
    private const int WbsLimit = 80;
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

        var tasks = document.Root?.Element(Ns + "Tasks")?.Elements(Ns + "Task")
            .Where(task => Text(task, "IsNull") != "1" && Level(task) > 0 && !string.IsNullOrWhiteSpace(Text(task, "Name")))
            .ToList();
        if (document.Root?.Name != Ns + "Project" || tasks is null)
            throw new MsProjectImportException(
                "Tệp không phải tiến độ MS Project dạng XML. Trong MS Project, chọn File → Save As → XML.");
        if (tasks.Count == 0) throw new MsProjectImportException("Tệp MS Project không có công việc nào.");
        if (tasks.Count > MaxItems)
            throw new MsProjectImportException(
                $"Tệp có {tasks.Count} công việc; mỗi phiên bản tiến độ tối đa {MaxItems}. Hãy gộp bớt các công việc chi tiết.");

        var warnings = new List<string>();
        var nodeByUid = tasks.ToDictionary(task => Text(task, "UID"), _ => Guid.NewGuid());
        var parents = new Stack<(int Level, Guid NodeId)>();
        var items = new List<SaveProjectScheduleItemRequest>();
        var usedWbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (task, index) in tasks.Select((task, index) => (task, index)))
        {
            var level = Level(task);
            while (parents.Count > 0 && parents.Peek().Level >= level) parents.Pop();
            var nodeId = nodeByUid[Text(task, "UID")];
            var name = Text(task, "Name").Trim();
            var wbs = FirstNonEmpty(Text(task, "WBS"), Text(task, "OutlineNumber"), (index + 1).ToString(CultureInfo.InvariantCulture));
            if (name.Length > TitleLimit)
            {
                warnings.Add($"{wbs}: tên dài hơn {TitleLimit} ký tự nên được cắt bớt.");
                name = name[..TitleLimit];
            }
            if (wbs.Length > WbsLimit || !usedWbs.Add(wbs))
            {
                var replacement = $"{index + 1}";
                warnings.Add($"{wbs}: mã WBS quá dài hoặc trùng nên dùng mã {replacement}.");
                wbs = replacement;
                usedWbs.Add(wbs);
            }

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
                if (!nodeByUid.TryGetValue(Text(link, "PredecessorUID"), out var predecessor))
                {
                    warnings.Add($"{wbs}: bỏ một liên kết tới công việc không có trong tệp.");
                    continue;
                }
                var type = int.TryParse(Text(link, "Type"), out var code) && code is >= 0 and <= 3 ? LinkTypes[code] : "FS";
                links.Add(new SaveProjectScheduleLinkRequest
                {
                    PredecessorNodeId = predecessor,
                    Type = type,
                    LagDays = LagDays(link),
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
        return new MsProjectImport(items, warnings);
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
    /// MSPDI keeps lag in tenths of a minute. Elapsed formats (even LagFormat codes) count
    /// 24-hour days; the others count 8-hour working days.
    /// </summary>
    private static int LagDays(XElement link)
    {
        if (!long.TryParse(Text(link, "LinkLag"), out var tenths) || tenths == 0) return 0;
        var elapsed = int.TryParse(Text(link, "LagFormat"), out var format) && format % 2 == 0;
        return (int)Math.Round(tenths / (elapsed ? 14_400m : 4_800m), MidpointRounding.AwayFromZero);
    }
}
