namespace NihomeBackend.Services;

public sealed record CmScheduleLine(int TaskId, int DesignProjectId, Guid NodeId, string Wbs, string Name,
    DateOnly CrmStart, DateOnly CrmEnd, DateOnly PlannedStart, DateOnly PlannedEnd,
    decimal Weight, bool IsLeaf, List<int> PredecessorIds, bool IsMilestone = false);
public sealed class CmScheduleCalendar
{
    private readonly HashSet<int> workingDays;
    private readonly int[] nonWorkingDayNumbers;

    public CmScheduleCalendar(int[] workingDaysOfWeek, DateOnly[] nonWorkingDates)
    {
        workingDays = workingDaysOfWeek.ToHashSet();
        nonWorkingDayNumbers = nonWorkingDates.Select(x => x.DayNumber).Distinct().Order().ToArray();
    }

    public int WorkingDaysBetween(DateOnly start, DateOnly end)
    {
        if (end < start) return 0;
        var inclusiveDays = end.DayNumber - start.DayNumber + 1;
        var fullWeeks = inclusiveDays / 7;
        var remainingDays = inclusiveDays % 7;
        var count = fullWeeks * workingDays.Count;
        for (var offset = 0; offset < remainingDays; offset++)
            if (workingDays.Contains((int)start.AddDays(offset).DayOfWeek)) count++;

        var firstHoliday = Array.BinarySearch(nonWorkingDayNumbers, start.DayNumber);
        if (firstHoliday < 0) firstHoliday = ~firstHoliday;
        for (var i = firstHoliday; i < nonWorkingDayNumbers.Length && nonWorkingDayNumbers[i] <= end.DayNumber; i++)
            if (workingDays.Contains((int)new DateOnly(1, 1, 1).AddDays(nonWorkingDayNumbers[i]).DayOfWeek)) count--;
        return count;
    }
}
public sealed record CmActualInput(int TaskId, int ProgressPercent, DateTime UpdatedAt, string Status,
    DateTime? ProgressReportedAt = null, int? ProgressReportedByUserId = null);
public sealed record CmProgressTotals(decimal Crm, decimal Plan, decimal Actual, decimal Delay);

public static class CmScheduleMath
{
    public static decimal Planned(DateOnly start, DateOnly end, DateOnly day, CmScheduleCalendar calendar)
    {
        if (day < start) return 0m;
        if (day >= end) return 100m;
        var total = calendar.WorkingDaysBetween(start, end);
        if (total == 0) throw new ArgumentException("The planned range contains no working days.");
        var elapsed = calendar.WorkingDaysBetween(start, day);
        return 100m * elapsed / total;
    }

    public static CmProgressTotals Calculate(IReadOnlyList<CmScheduleLine> lines, IReadOnlyList<CmActualInput> actual,
        DateOnly day, CmScheduleCalendar calendar)
    {
        var leaves = lines.Where(x => x.IsLeaf && !x.IsMilestone).ToList();
        if (leaves.Count == 0 || leaves.Any(x => x.Weight <= 0 || x.PlannedEnd < x.PlannedStart || x.CrmEnd < x.CrmStart))
            throw new ArgumentException("A weighted scope schedule is required.");
        var inputs = actual.ToDictionary(x => x.TaskId);
        if (leaves.Any(x => !inputs.TryGetValue(x.TaskId, out var input) || input.ProgressPercent is < 0 or > 100))
            throw new ArgumentException("Actual progress is incomplete.");
        var weight = leaves.Sum(x => x.Weight);
        var crm = leaves.Sum(x => x.Weight * Planned(x.CrmStart, x.CrmEnd, day, calendar)) / weight;
        var plan = leaves.Sum(x => x.Weight * Planned(x.PlannedStart, x.PlannedEnd, day, calendar)) / weight;
        var done = leaves.Sum(x => x.Weight * inputs[x.TaskId].ProgressPercent) / weight;
        return new(crm, plan, done, plan - done);
    }
}
