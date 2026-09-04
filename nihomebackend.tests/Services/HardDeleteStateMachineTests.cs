using NihomeBackend.Models;
using NihomeBackend.Services.HardDelete;

namespace nihomebackend.tests.Services;

public sealed class HardDeleteStateMachineTests
{
    [Theory]
    [InlineData(HardDeleteOperationStatus.Preparing, HardDeleteOperationStatus.Ready)]
    [InlineData(HardDeleteOperationStatus.Ready, HardDeleteOperationStatus.Processing)]
    [InlineData(HardDeleteOperationStatus.Processing, HardDeleteOperationStatus.Failed)]
    [InlineData(HardDeleteOperationStatus.Failed, HardDeleteOperationStatus.Processing)]
    [InlineData(HardDeleteOperationStatus.Processing, HardDeleteOperationStatus.ManualActionRequired)]
    [InlineData(HardDeleteOperationStatus.ManualActionRequired, HardDeleteOperationStatus.Processing)]
    [InlineData(HardDeleteOperationStatus.Processing, HardDeleteOperationStatus.Completed)]
    public void Transition_AllowedEdge_UpdatesStatusAndTimestamps(
        HardDeleteOperationStatus current,
        HardDeleteOperationStatus next)
    {
        var now = DateTime.UtcNow;
        var operation = new HardDeleteOperation { Status = current };

        HardDeleteStateMachine.Transition(operation, next, now);

        Assert.Equal(next, operation.Status);
        Assert.Equal(now, operation.UpdatedAt);
        if (next == HardDeleteOperationStatus.Processing) Assert.Equal(now, operation.StartedAt);
        if (next == HardDeleteOperationStatus.Completed) Assert.Equal(now, operation.CompletedAt);
    }

    [Theory]
    [InlineData(HardDeleteOperationStatus.Completed, HardDeleteOperationStatus.Processing)]
    [InlineData(HardDeleteOperationStatus.Ready, HardDeleteOperationStatus.Completed)]
    [InlineData(HardDeleteOperationStatus.Preparing, HardDeleteOperationStatus.Processing)]
    [InlineData(HardDeleteOperationStatus.Failed, HardDeleteOperationStatus.Completed)]
    public void Transition_InvalidEdge_IsRejectedWithoutMutation(
        HardDeleteOperationStatus current,
        HardDeleteOperationStatus next)
    {
        var operation = new HardDeleteOperation { Status = current };

        var exception = Assert.Throws<HardDeleteOperationException>(() =>
            HardDeleteStateMachine.Transition(operation, next, DateTime.UtcNow));

        Assert.Equal("invalid_operation_transition", exception.Code);
        Assert.Equal(current, operation.Status);
    }
}