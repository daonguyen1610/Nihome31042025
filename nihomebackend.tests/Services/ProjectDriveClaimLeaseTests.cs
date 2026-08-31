using Moq;
using NihomeBackend.Services.GoogleDrive;

namespace nihomebackend.tests.Services;

public sealed class ProjectDriveClaimLeaseTests
{
    [Fact]
    public async Task RunAsync_LongOperation_RenewsClaimUntilCompletion()
    {
        var renewer = new Mock<IProjectDriveClaimRenewer>();
        var operationCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        renewer.Setup(item => item.RenewAsync(42, It.IsAny<Guid>(), 3, It.IsAny<CancellationToken>()))
            .Callback(() => operationCompletion.TrySetResult("uploaded"))
            .ReturnsAsync(true);
        var lease = new ProjectDriveClaimLease(renewer.Object, TimeSpan.FromMilliseconds(10));

        var result = await lease.RunAsync(42, Guid.NewGuid(), 3, _ => operationCompletion.Task, CancellationToken.None);

        Assert.Equal("uploaded", result);
        renewer.Verify(item => item.RenewAsync(42, It.IsAny<Guid>(), 3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ClaimOwnershipLost_CancelsUploadAndRejectsCompletion()
    {
        var renewer = new Mock<IProjectDriveClaimRenewer>();
        renewer.Setup(item => item.RenewAsync(42, It.IsAny<Guid>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var lease = new ProjectDriveClaimLease(renewer.Object, TimeSpan.FromMilliseconds(10));
        var operationCancelled = false;

        await Assert.ThrowsAsync<ProjectDriveClaimLostException>(() => lease.RunAsync(
            42, Guid.NewGuid(), 3, async operationCt =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, operationCt);
                    return "unexpected";
                }
                catch (OperationCanceledException)
                {
                    operationCancelled = true;
                    throw;
                }
            }, CancellationToken.None));

        Assert.True(operationCancelled);
    }
}