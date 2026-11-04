using NihomeBackend.Models;

namespace NihomeBackend.Services;

internal sealed record ProjectScheduleLinkedTask(
    string Source,
    string Reference,
    Guid NodeId,
    DateOnly PlannedStart,
    DateOnly PlannedEnd);

internal static class ProjectSchedulePublicationValidator
{
    public static void Validate(ProjectScheduleRevision revision, IEnumerable<ProjectScheduleLinkedTask> tasks)
    {
        var items = revision.Items.ToDictionary(item => item.NodeId);
        foreach (var task in tasks)
        {
            if (!items.TryGetValue(task.NodeId, out var item))
            {
                throw new ProjectScheduleOperationException(
                    $"Không thể phát hành: {task.Source} {task.Reference} đang liên kết WBS {task.NodeId}, nhưng mốc này đã bị gỡ khỏi revision.",
                    409);
            }

            if (task.PlannedStart < item.PlannedStart || task.PlannedEnd > item.PlannedEnd)
            {
                throw new ProjectScheduleOperationException(
                    $"Không thể phát hành: lịch của {task.Source} {task.Reference} nằm ngoài khung WBS {item.WbsCode} ({item.PlannedStart:yyyy-MM-dd} đến {item.PlannedEnd:yyyy-MM-dd}). Hãy điều chỉnh lịch hoặc giữ khung mốc phù hợp.",
                    409);
            }
        }
    }
}
