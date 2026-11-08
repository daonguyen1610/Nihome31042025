using NihomeBackend.Services;
namespace nihomebackend.tests.Services;

public sealed class CmScheduleMathTests
{
    private static readonly DateOnly Start = new(2026, 11, 1);
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    [InlineData(11, 100)]
    public void Planned_InclusiveCalendarDays(int day, decimal expected) => Assert.Equal(expected,
        CmScheduleMath.Planned(Start, new(2026, 11, 10), new DateOnly(2026, 10, 31).AddDays(day)));
    [Fact]
    public void Calculate_WeightsOnlyLeaves_AndDoesNotDoubleCountSummary()
    {
        var lines = new[] { Line(1, 1000, false), Line(2, 1, true), Line(3, 3, true) };
        var values = new[] { Actual(1, 100), Actual(2, 100), Actual(3, 0) };
        var result = CmScheduleMath.Calculate(lines, values, new(2026, 11, 5));
        Assert.Equal(50m, result.Plan); Assert.Equal(25m, result.Actual); Assert.Equal(25m, result.Delay);
    }
    [Fact]
    public void Calculate_MissingLeafIsNotZeroProgress() => Assert.Throws<ArgumentException>(() =>
        CmScheduleMath.Calculate([Line(1, 1, true)], [], Start));
    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Calculate_InvalidActualRejected(int percent) => Assert.Throws<ArgumentException>(() =>
        CmScheduleMath.Calculate([Line(1, 1, true)], [Actual(1, percent)], Start));
    [Fact]
    public void Calculate_ExactlyFiveIsNotAboveFive()
    {
        var result = CmScheduleMath.Calculate([Line(1, 1, true)], [Actual(1, 45)], new(2026, 11, 5));
        Assert.Equal(5m, result.Delay); Assert.False(result.Delay > 5m);
    }
    [Fact]
    public void JustAboveThreshold_IsNotRoundedDownBeforeComparison()
    {
        var result = CmScheduleMath.Calculate([Line(1, 1, true), Line(2, 24999, true)], [Actual(1, 94), Actual(2, 95)], new(2026, 11, 10));
        Assert.Equal(5.00004m, result.Delay);
        Assert.True(result.Delay > 5m);
    }
    private static CmScheduleLine Line(int id, decimal weight, bool leaf) => new(id, 1, Guid.NewGuid(), id.ToString(), "NICON – Thi công móng", Start, new(2026, 11, 10), Start, new(2026, 11, 10), weight, leaf, []);
    private static CmActualInput Actual(int id, int percent) => new(id, percent, DateTime.UtcNow, "InProgress");
}
