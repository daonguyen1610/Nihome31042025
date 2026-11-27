using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public static class MyWorkSources
{
    public const string WorkTask = "WorkTask";
    public const string DesignTask = "DesignTask";
}

/// <summary>
/// "Việc của tôi": the work-package and design-schedule tasks a person is assigned
/// or created, read from the existing task records. Only the caller's own work is returned.
/// </summary>
public sealed class MyWorkService(AppDbContext db)
{
    // shortcut: due-soon window reuses the default reminder lead time; move to ADMIN settings if NICON wants to tune it.
    public const int DueSoonDays = ReminderDefaults.DaysBefore;

    /// <summary>
    /// Without a range: open work only (the attention lists). With a range: every
    /// non-cancelled item whose planned dates overlap it (the day/week/month/year plan).
    /// </summary>
    public async Task<MyWorkResponse> GetAsync(int userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        if (from.HasValue != to.HasValue || from > to)
            throw new ArgumentException("Khoảng ngày không hợp lệ: cần cả ngày bắt đầu và ngày kết thúc, theo đúng thứ tự.");
        var today = await DisplaySettings.TodayAsync(db, ct);

        var workTasks = db.DepartmentWorkTasks.AsNoTracking()
            .Where(task => task.AssigneeUserId == userId || task.CreatedByUserId == userId)
            .Where(task => task.Status != WorkTaskStatus.Cancelled && task.Status != WorkTaskStatus.Disabled);
        var designTasks = db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.AssigneeMember.UserId == userId || task.CreatedByUserId == userId);
        if (from.HasValue && to.HasValue)
        {
            var fromAt = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var toAt = to.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            workTasks = workTasks.Where(task => task.PlannedStart <= toAt && task.PlannedEnd >= fromAt);
            designTasks = designTasks.Where(task => task.PlannedStart <= to.Value && task.PlannedEnd >= from.Value);
        }
        else
        {
            workTasks = workTasks.Where(task =>
                task.Status == WorkTaskStatus.Planned || task.Status == WorkTaskStatus.InProgress);
            // Nobody can act on a closed dossier, so its unfinished tasks leave the to-do list.
            designTasks = designTasks.Where(task => task.Status != DesignScheduleStatus.Completed &&
                task.DesignProject.Status != DesignProjectStatus.Cancelled);
        }

        var items = new List<MyWorkItemResponse>();
        foreach (var task in await workTasks.Select(task => new
        {
            task.Id,
            task.WorkPackageId,
            task.WbsCode,
            task.Title,
            task.Status,
            task.ProgressPercent,
            task.PlannedStart,
            task.PlannedEnd,
            task.IsPriority,
            task.AssigneeUserId,
            AssigneeName = task.AssigneeUser.FullName ?? task.AssigneeUser.Email,
            PackageCode = task.WorkPackage.Code,
            PackageName = task.WorkPackage.Name,
            IsInternal = task.WorkPackage.Kind == WorkPackageKind.Internal,
        }).ToListAsync(ct))
        {
            items.Add(Item(today, MyWorkSources.WorkTask, task.Id, task.WbsCode, task.Title,
                task.PackageCode, task.PackageName, task.IsInternal, task.AssigneeUserId == userId, task.AssigneeName,
                task.Status.ToString(), task.Status is WorkTaskStatus.Planned or WorkTaskStatus.InProgress,
                task.ProgressPercent, DateOnly.FromDateTime(task.PlannedStart), DateOnly.FromDateTime(task.PlannedEnd),
                task.IsPriority, $"/admin/work-packages/{task.WorkPackageId}?taskId={task.Id}"));
        }
        foreach (var task in await designTasks.Select(task => new
        {
            task.Id,
            task.DesignProjectId,
            task.Code,
            task.Name,
            task.Status,
            task.ProgressPercent,
            task.PlannedStart,
            task.PlannedEnd,
            task.IsCritical,
            AssigneeUserId = task.AssigneeMember.UserId,
            AssigneeName = task.AssigneeMember.User.FullName ?? task.AssigneeMember.User.Email,
            ProjectCode = task.DesignProject.ProjectCode,
            ProjectName = task.DesignProject.Name,
        }).ToListAsync(ct))
        {
            items.Add(Item(today, MyWorkSources.DesignTask, task.Id, task.Code, task.Name,
                task.ProjectCode, task.ProjectName, false, task.AssigneeUserId == userId, task.AssigneeName,
                task.Status.ToString(), task.Status != DesignScheduleStatus.Completed,
                task.ProgressPercent, task.PlannedStart, task.PlannedEnd,
                task.IsCritical, $"/admin/design-projects/{task.DesignProjectId}?tab=schedule"));
        }

        return new MyWorkResponse
        {
            Today = today,
            DueSoonDays = DueSoonDays,
            Items = items.OrderBy(item => item.PlannedEnd).ThenBy(item => item.Code).ToList(),
        };
    }

    private static MyWorkItemResponse Item(
        DateOnly today, string source, int id, string code, string title, string? containerCode, string? containerName,
        bool isInternal, bool assignedToMe, string? assigneeName, string status, bool isOpen, int progress,
        DateOnly start, DateOnly end, bool isPriority, string link)
    {
        var daysLate = today.DayNumber - end.DayNumber;
        return new MyWorkItemResponse
        {
            Source = source,
            Id = id,
            Code = code,
            Title = title,
            ContainerCode = containerCode,
            ContainerName = containerName,
            IsInternal = isInternal,
            Relation = assignedToMe ? "Assigned" : "Created",
            AssigneeName = assigneeName,
            Status = status,
            IsOpen = isOpen,
            ProgressPercent = progress,
            PlannedStart = start,
            PlannedEnd = end,
            IsToday = isOpen && start <= today && end >= today,
            IsDueSoon = isOpen && daysLate < 0 && -daysLate <= DueSoonDays,
            IsOverdue = isOpen && daysLate > 0,
            OverdueDays = isOpen && daysLate > 0 ? daysLate : null,
            IsPriority = isOpen && isPriority,
            LinkUrl = link,
        };
    }
}
