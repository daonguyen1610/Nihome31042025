using NihomeBackend.Models;
using NihomeBackend.Services;

namespace nihomebackend.tests.Services;

public sealed class ProjectSchedulePublicationValidatorTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);
    private static readonly DateOnly End = new(2026, 6, 30);

    [Fact]
    public void Validate_accepts_linked_work_within_inclusive_milestone_dates()
    {
        var nodeId = Guid.NewGuid();
        var revision = Revision(nodeId, Start, End);
        var linkedTask = new ProjectScheduleLinkedTask(
            "công việc Thiết kế", "DS-01 · Móng", nodeId, Start, End);

        ProjectSchedulePublicationValidator.Validate(revision, [linkedTask]);
    }

    [Fact]
    public void Validate_rejects_task_outside_revised_milestone_window()
    {
        var nodeId = Guid.NewGuid();
        var revision = Revision(nodeId, new DateOnly(2026, 6, 10), End);
        var linkedTask = new ProjectScheduleLinkedTask(
            "công việc phòng ban", "1.1 · Ép cọc", nodeId, Start, new DateOnly(2026, 6, 20));

        var error = Assert.Throws<ProjectScheduleOperationException>(
            () => ProjectSchedulePublicationValidator.Validate(revision, [linkedTask]));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("1.1 · Ép cọc", error.Message);
        Assert.Contains("nằm ngoài khung WBS", error.Message);
    }

    [Fact]
    public void Validate_rejects_removing_a_milestone_referenced_by_existing_work()
    {
        var removedNodeId = Guid.NewGuid();
        var revision = Revision(Guid.NewGuid(), Start, End);
        var linkedTask = new ProjectScheduleLinkedTask(
            "công việc Thi công", "T-01 · Đổ bê tông", removedNodeId, Start, End);

        var error = Assert.Throws<ProjectScheduleOperationException>(
            () => ProjectSchedulePublicationValidator.Validate(revision, [linkedTask]));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("đã bị gỡ khỏi revision", error.Message);
    }

    private static ProjectScheduleRevision Revision(Guid nodeId, DateOnly start, DateOnly end) => new()
    {
        Items =
        [
            new ProjectScheduleItem
            {
                NodeId = nodeId,
                WbsCode = "1.1",
                Title = "Thi công móng",
                PlannedStart = start,
                PlannedEnd = end,
            },
        ],
    };
}
