using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DesignApprovalInboxOperationException(string message) : Exception(message);

/// <summary>Design work waiting on one person: tasks they approve and internal reviews they were sent.</summary>
public sealed class DesignApprovalInboxService(AppDbContext db)
{
    public async Task<DesignApprovalInboxSummaryResponse> GetSummaryAsync(int userId, CancellationToken ct)
    {
        var tasks = PendingTasks(userId);
        var reviews = PendingReviews(userId);
        var dossierIds = await tasks.Select(task => task.DesignProjectId)
            .Union(reviews.Select(review => review.DesignProjectId))
            .ToListAsync(ct);
        var dossiers = await db.DesignProjects.AsNoTracking()
            .Where(project => dossierIds.Contains(project.Id))
            .OrderBy(project => project.Name)
            .Select(project => new DesignApprovalInboxDossierResponse
            {
                Id = project.Id,
                ProjectCode = project.ProjectCode,
                Name = project.Name,
            })
            .ToListAsync(ct);
        return new DesignApprovalInboxSummaryResponse
        {
            TaskCount = await tasks.CountAsync(ct),
            ReviewCount = await reviews.CountAsync(ct),
            Dossiers = dossiers,
        };
    }

    public async Task<DesignApprovalInboxPageResponse<DesignApprovalInboxTaskResponse>> ListTasksAsync(
        int userId, DesignApprovalInboxQuery query, CancellationToken ct)
    {
        var tasks = PendingTasks(userId);
        if (query.DesignProjectId is { } projectId)
            tasks = tasks.Where(task => task.DesignProjectId == projectId);
        if (Term(query) is { } term)
            tasks = tasks.Where(task => task.Code.Contains(term)
                || task.Name.Contains(term)
                || task.DesignProject.ProjectCode.Contains(term)
                || task.DesignProject.Name.Contains(term)
                || (task.AssigneeMember.User.FullName != null && task.AssigneeMember.User.FullName.Contains(term)));
        tasks = query.NewestFirst
            ? tasks.OrderByDescending(task => task.SubmittedAt).ThenByDescending(task => task.Id)
            : tasks.OrderBy(task => task.SubmittedAt).ThenBy(task => task.Id);

        return await PageAsync(tasks.Select(task => new DesignApprovalInboxTaskResponse
        {
            TaskId = task.Id,
            Code = task.Code,
            Name = task.Name,
            PhaseCode = task.Phase.Code.ToString(),
            DesignProjectId = task.DesignProjectId,
            DesignProjectCode = task.DesignProject.ProjectCode,
            DesignProjectName = task.DesignProject.Name,
            AssigneeName = task.AssigneeMember.User.FullName,
            PlannedEnd = task.PlannedEnd,
            SubmittedAt = task.SubmittedAt,
        }), query, ct);
    }

    public async Task<DesignApprovalInboxPageResponse<DesignApprovalInboxReviewResponse>> ListReviewsAsync(
        int userId, DesignApprovalInboxQuery query, CancellationToken ct)
    {
        var reviews = PendingReviews(userId);
        if (query.DesignProjectId is { } projectId)
            reviews = reviews.Where(review => review.DesignProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            if (!Enum.TryParse<DesignReviewStage>(query.Stage.Trim(), true, out var stage) || !Enum.IsDefined(stage))
                throw new DesignApprovalInboxOperationException(
                    "Giai đoạn không hợp lệ. Hãy chọn Thiết kế ý tưởng, Thiết kế cơ sở hoặc Thiết kế chi tiết.");
            reviews = reviews.Where(review => review.Stage == stage);
        }
        if (query.Commented == true)
            reviews = reviews.Where(review => review.Comments.Any(comment => comment.AuthorUserId == userId));
        else if (query.Commented == false)
            reviews = reviews.Where(review => !review.Comments.Any(comment => comment.AuthorUserId == userId));
        if (Term(query) is { } term)
            reviews = reviews.Where(review => review.Title.Contains(term)
                || review.DesignProject.ProjectCode.Contains(term)
                || review.DesignProject.Name.Contains(term)
                || (review.DesignManager.FullName != null && review.DesignManager.FullName.Contains(term)));
        reviews = query.NewestFirst
            ? reviews.OrderByDescending(review => review.InternalSubmittedAt).ThenByDescending(review => review.Id)
            : reviews.OrderBy(review => review.InternalSubmittedAt).ThenBy(review => review.Id);

        return await PageAsync(reviews.Select(review => new DesignApprovalInboxReviewResponse
        {
            ReviewId = review.Id,
            Title = review.Title,
            Stage = review.Stage.ToString(),
            RevisionNumber = review.RevisionNumber,
            DesignProjectId = review.DesignProjectId,
            DesignProjectCode = review.DesignProject.ProjectCode,
            DesignProjectName = review.DesignProject.Name,
            DesignManagerName = review.DesignManager.FullName,
            SubmittedAt = review.InternalSubmittedAt,
            HasMyComment = review.Comments.Any(comment => comment.AuthorUserId == userId),
        }), query, ct);
    }

    /// <summary>Tasks the user can approve now: still the approver and still on the team.</summary>
    private IQueryable<DesignScheduleTask> PendingTasks(int userId)
    {
        var now = DateTime.UtcNow;
        return db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.DesignProject.Status != DesignProjectStatus.Cancelled
                && task.ApprovalStatus == DesignTaskApprovalStatus.Pending
                && task.ApproverMember.UserId == userId
                && task.ApproverMember.StartedAt <= now
                && (task.ApproverMember.EndedAt == null || task.ApproverMember.EndedAt > now));
    }

    private IQueryable<DesignReview> PendingReviews(int userId) =>
        db.DesignReviews.AsNoTracking()
            .Where(review => review.DesignProject.Status != DesignProjectStatus.Cancelled
                && review.Status == DesignReviewStatus.InternalReview
                && review.Recipients.Any(recipient => recipient.UserId == userId));

    private static string? Term(DesignApprovalInboxQuery query) =>
        string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

    private static async Task<DesignApprovalInboxPageResponse<T>> PageAsync<T>(
        IQueryable<T> items, DesignApprovalInboxQuery query, CancellationToken ct)
    {
        var total = await items.CountAsync(ct);
        var page = await items.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new DesignApprovalInboxPageResponse<T>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total,
            Items = page,
        };
    }
}
