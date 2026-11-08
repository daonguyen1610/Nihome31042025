using NihomeBackend.Services;
namespace nihomebackend.tests.Services;

public sealed class CmScheduleMathTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly CmScheduleCalendar Calendar = new([1, 2, 3, 4, 5, 6], []);
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    [InlineData(11, 100)]
    public void Planned_UsesWorkingDays(int day, decimal expected) => Assert.Equal(expected,
        CmScheduleMath.Planned(Start, new(2026, 11, 10), new DateOnly(2026, 10, 31).AddDays(day), Calendar));
    [Fact]
    public void Calculate_WeightsOnlyLeaves_AndDoesNotDoubleCountSummary()
    {
        var lines = new[] { Line(1, 0, false), Line(2, 25, true), Line(3, 75, true) };
        var values = new[] { Actual(1, 100), Actual(2, 100), Actual(3, 0) };
        var result = CmScheduleMath.Calculate(lines, values, new(2026, 11, 5), Calendar);
        Assert.Equal(50m, result.Plan); Assert.Equal(25m, result.Actual); Assert.Equal(25m, result.Delay);
    }
    [Fact]
    public void Calculate_MissingLeafIsNotZeroProgress() => Assert.Throws<ArgumentException>(() =>
        CmScheduleMath.Calculate([Line(1, 100, true)], [], Start, Calendar));
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Calculate_InvalidActualRejected(int percent) => Assert.Throws<ArgumentException>(() =>
        CmScheduleMath.Calculate([Line(1, 100, true)], [Actual(1, percent)], Start, Calendar));
    [Fact]
    public void Calculate_ExactlyFiveIsNotAboveFive()
    {
        var result = CmScheduleMath.Calculate([Line(1, 100, true)], [Actual(1, 45)], new(2026, 11, 5), Calendar);
        Assert.Equal(5m, result.Delay); Assert.False(result.Delay > 5m);
    }
    [Fact]
    public void JustAboveThreshold_IsNotRoundedDownBeforeComparison()
    {
        var result = CmScheduleMath.Calculate([Line(1, 1, true), Line(2, 99999, true)], [Actual(1, 94), Actual(2, 95)], new(2026, 11, 10), Calendar);
        Assert.Equal(5.00001m, result.Delay);
        Assert.True(result.Delay > 5m);
    }
    [Fact]
    public void Planned_ExcludesHolidayAndSunday()
    {
        var calendar = new CmScheduleCalendar([1, 2, 3, 4, 5, 6], [new(2026, 11, 3)]);
        Assert.Equal(2, calendar.WorkingDaysBetween(new(2026, 11, 2), new(2026, 11, 4)));
        Assert.Equal(0m, CmScheduleMath.Planned(new(2026, 11, 2), new(2026, 11, 4), new(2026, 11, 1), calendar));
        Assert.Equal(50m, CmScheduleMath.Planned(new(2026, 11, 2), new(2026, 11, 4), new(2026, 11, 3), calendar));
    }
    [Fact]
    public void Calculate_ExcludesMilestonesFromScopeWeight()
    {
        var lines = new[] { Line(1, 100, true), Line(2, 0, true) with { IsMilestone = true } };
        var result = CmScheduleMath.Calculate(lines, [Actual(1, 40)], new(2026, 11, 5), Calendar);
        Assert.Equal(40m, result.Actual);
    }
    private static CmScheduleLine Line(int id, decimal weight, bool leaf) => new(id, 1, Guid.NewGuid(), id.ToString(), "NICON – Thi công móng", Start, new(2026, 11, 10), Start, new(2026, 11, 10), weight, leaf, []);
    private static CmActualInput Actual(int id, int percent) => new(id, percent, DateTime.UtcNow, "InProgress");
}
