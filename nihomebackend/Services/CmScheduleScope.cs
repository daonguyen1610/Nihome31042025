using NihomeBackend.Models;

namespace NihomeBackend.Services;

public static class CmScheduleScope
{
    public static bool Matches(IReadOnlyCollection<CmScheduleLine> lines, IReadOnlyCollection<ConstructionTask> tasks) =>
        lines.Count == tasks.Count && tasks.All(task => lines.Any(line => line.TaskId == task.Id &&
            line.DesignProjectId == task.DesignProjectId && line.NodeId == task.ProjectScheduleNodeId &&
            line.Wbs == (task.Wbs ?? task.TaskCode) &&
            line.PredecessorIds.Order().SequenceEqual(task.Predecessors.Select(x => x.PredecessorTaskId).Order())));
}
