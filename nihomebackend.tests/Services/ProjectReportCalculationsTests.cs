using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectReportCalculationsTests
{
    [Fact]
    public void CalculateDesignRollup_UsesApprovedTwoLevelWeightedPolicy()
    {
        var phases = new[]
        {
            Phase(1, DesignSchedulePhaseCode.Concept, 20),
            Phase(2, DesignSchedulePhaseCode.BasicDesign, 30),
            Phase(3, DesignSchedulePhaseCode.ShopDrawing, 50),
        };
        var tasks = new[]
        {
            Task(11, 1, 40, 50), Task(12, 1, 60, 100),
            Task(21, 2, 100, 20),
            Task(31, 3, 50, 40), Task(32, 3, 50, 60),
        };

        var result = ProjectReportCalculations.CalculateDesignRollup(phases, tasks);

        Assert.True(result.BaselineReady);
        Assert.Equal(47m, result.Progress);
        Assert.Equal([16m, 6m, 25m], result.Sources.Select(source => source.WeightedValue));
    }

    [Fact]
    public void CalculateDesignRollup_ReturnsUnavailableWhenAnyBaselineIsIncomplete()
    {
        var phases = new[]
        {
            Phase(1, DesignSchedulePhaseCode.Concept, 20),
            Phase(2, DesignSchedulePhaseCode.BasicDesign, 30),
            Phase(3, DesignSchedulePhaseCode.ShopDrawing, 50),
        };
        var tasks = new[]
        {
            Task(11, 1, 100, 50),
            Task(21, 2, 100, 50),
        };

        var result = ProjectReportCalculations.CalculateDesignRollup(phases, tasks);

        Assert.False(result.BaselineReady);
        Assert.Null(result.Progress);
        Assert.Empty(result.Sources);
    }

    [Theory]
    [InlineData("=SUM(A1:A2)", "'=SUM(A1:A2)")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("Normal project", "Normal project")]
    public void SafeSpreadsheetText_NeutralizesFormulaPrefixes(string value, string expected)
    {
        Assert.Equal(expected, ProjectReportCalculations.SafeSpreadsheetText(value));
    }

    private static DesignSchedulePhase Phase(int id, DesignSchedulePhaseCode code, int weight) => new()
    {
        Id = id,
        Code = code,
        Weight = weight,
    };

    private static DesignScheduleTask Task(int id, int phaseId, int weight, int progress) => new()
    {
        Id = id,
        PhaseId = phaseId,
        Weight = weight,
        ProgressPercent = progress,
    };
}
