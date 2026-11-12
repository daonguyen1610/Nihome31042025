using NihomeBackend.Models;

namespace NihomeBackend.Services;

internal sealed record ProjectScheduleLinkedTask(
    string Source,
    string Reference,
    Guid NodeId,
    DateOnly? PlannedStart,
    DateOnly? PlannedEnd);

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
                    $"Không thể phát hành: {task.Source} {task.Reference} đang liên kết một hạng mục đã bị gỡ khỏi phiên bản tiến độ. Hãy giữ hạng mục hoặc sửa liên kết công việc.",
                    409);
            }

            if (task.PlannedStart.HasValue && task.PlannedEnd.HasValue &&
                (task.PlannedStart.Value < item.PlannedStart || task.PlannedEnd.Value > item.PlannedEnd))
            {
                throw new ProjectScheduleOperationException(
                    $"Không thể phát hành: lịch của {task.Source} {task.Reference} nằm ngoài khung hạng mục {item.WbsCode} ({item.PlannedStart:yyyy-MM-dd} đến {item.PlannedEnd:yyyy-MM-dd}). Hãy điều chỉnh lịch hoặc giữ khung hạng mục phù hợp.",
                    409);
            }
        }
    }
}
