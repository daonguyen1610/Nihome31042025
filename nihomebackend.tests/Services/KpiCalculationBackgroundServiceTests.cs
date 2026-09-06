using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public class KpiCalculationBackgroundServiceTests
{
    [Theory]
    [InlineData("2026-08-31T16:59:59Z", 3601)]
    [InlineData("2026-08-31T17:00:00Z", 3600)]
    [InlineData("2028-02-28T18:00:00Z", 86400)]
    public void DelayUntilNextRun_TargetsOneAmVietnamTime(string utcValue, int expectedSeconds)
    {
        var utcNow = DateTimeOffset.Parse(utcValue, System.Globalization.CultureInfo.InvariantCulture);

        var delay = KpiCalculationBackgroundService.DelayUntilNextRun(utcNow);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }
}
