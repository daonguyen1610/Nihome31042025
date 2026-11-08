namespace NihomeBackend.Services;

public sealed record CmScheduleLine(int TaskId, int DesignProjectId, Guid NodeId, string Wbs, string Name,
    DateOnly CrmStart, DateOnly CrmEnd, DateOnly PlannedStart, DateOnly PlannedEnd,
    decimal Weight, bool IsLeaf, List<int> PredecessorIds);
public sealed record CmActualInput(int TaskId, int ProgressPercent, DateTime UpdatedAt, string Status);
public sealed record CmProgressTotals(decimal Crm, decimal Plan, decimal Actual, decimal Delay);

public static class CmScheduleMath
{
    public static decimal Planned(DateOnly start, DateOnly end, DateOnly day) => day < start ? 0m : day >= end ? 100m :
        100m * (day.DayNumber - start.DayNumber + 1) / (end.DayNumber - start.DayNumber + 1);

    public static CmProgressTotals Calculate(IReadOnlyList<CmScheduleLine> lines, IReadOnlyList<CmActualInput> actual, DateOnly day)
    {
        var leaves = lines.Where(x => x.IsLeaf).ToList();
        if (leaves.Count == 0 || leaves.Any(x => x.Weight <= 0 || x.PlannedEnd < x.PlannedStart || x.CrmEnd < x.CrmStart))
            throw new ArgumentException("A weighted leaf schedule is required.");
        var inputs = actual.ToDictionary(x => x.TaskId);
        if (leaves.Any(x => !inputs.TryGetValue(x.TaskId, out var input) || input.ProgressPercent is < 0 or > 100))
            throw new ArgumentException("Actual progress is incomplete.");
        var weight = leaves.Sum(x => x.Weight);
        var crm = leaves.Sum(x => x.Weight * Planned(x.CrmStart, x.CrmEnd, day)) / weight;
        var plan = leaves.Sum(x => x.Weight * Planned(x.PlannedStart, x.PlannedEnd, day)) / weight;
        var done = leaves.Sum(x => x.Weight * inputs[x.TaskId].ProgressPercent) / weight;
        return new(crm, plan, done, plan - done);
    }
}
