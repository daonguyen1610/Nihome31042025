using Moq;
using NihomeBackend.Models;
using NihomeBackend.Services;
using nihomebackend.tests.Helpers;

namespace nihomebackend.tests.Services;

public sealed class CmPortfolioServiceTests
{
    [Fact]
    public async Task SourcePermissions_MaskCountsAndLinksRatherThanShowingZero()
    {
        await using var db = DbContextFactory.Create();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.GetForUserAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "reports.projects.view" });
        var result = (await new CmPortfolioService(db, permissions.Object).GetAsync([41], 10, default))[41];
        Assert.Equal("CurrentState", result.PeriodPolicy);
        foreach (var section in new[] { result.Upstream, result.Downstream, result.QaQc, result.Safety })
        {
            Assert.Equal("Unavailable", section.Availability);
            Assert.Equal("SOURCE_PERMISSION_REQUIRED", section.ReasonCode);
            Assert.Null(section.Count);
            Assert.Empty(section.Sources);
        }
        Assert.Null(result.Budget.Count);
    }

    [Fact]
    public async Task QsSources_SeparateDirectionsAndExcludeReversedSupersededPendingAndOtherProjects()
    {
        await using var db = DbContextFactory.Create();
        db.ConstructionQsBatches.AddRange(
            Batch(1, 41, ConstructionQsDirection.Upstream),
            Batch(2, 41, ConstructionQsDirection.Downstream),
            Batch(3, 42, ConstructionQsDirection.Upstream),
            Batch(4, 41, ConstructionQsDirection.Upstream, ConstructionQsRecordState.Superseded),
            Batch(5, 41, ConstructionQsDirection.Downstream, ConstructionQsRecordState.Reversed),
            Batch(6, 41, ConstructionQsDirection.Upstream, status: ConstructionReviewStatus.PendingCmReview));
        await db.SaveChangesAsync();
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.GetForUserAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "construction.qs.view", "construction.qa-qc.view" });
        var result = (await new CmPortfolioService(db, permissions.Object).GetAsync([41], 10, default))[41];
        Assert.Equal(1, result.Upstream.Count);
        Assert.Equal(1, result.Downstream.Count);
        Assert.Equal(1, Assert.Single(result.Upstream.Sources).Id);
        Assert.Equal(2, Assert.Single(result.Downstream.Sources).Id);
        Assert.Contains("projectId=41", result.Upstream.Sources[0].Route);
        Assert.Equal("Available", result.QaQc.Availability);
        Assert.Equal(0, result.QaQc.Count);
    }

    private static ConstructionQsBatch Batch(int id, int project, ConstructionQsDirection direction,
        ConstructionQsRecordState state = ConstructionQsRecordState.Active,
        ConstructionReviewStatus status = ConstructionReviewStatus.CmApproved) => new()
        {
            Id = id,
            OperationalProjectId = project,
            Code = $"QS-STRUCTURE-{id}",
            Direction = direction,
            Status = status,
            RecordState = state,
            IsActiveLineage = true,
        };
}
