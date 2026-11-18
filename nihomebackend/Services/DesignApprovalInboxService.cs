using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

/// <summary>Design work waiting on one person: tasks they approve and internal reviews they were sent.</summary>
public sealed class DesignApprovalInboxService(AppDbContext db)
{
    public async Task<DesignApprovalInboxResponse> GetAsync(int userId, CancellationToken ct)
    {
        var tasks = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => task.ApprovalStatus == DesignTaskApprovalStatus.Pending
                && task.ApproverMember.UserId == userId)
            .OrderBy(task => task.SubmittedAt)
            .Select(task => new DesignApprovalInboxTaskResponse
            {
                TaskId = task.Id,
                Code = task.Code,
                Name = task.Name,
                DesignProjectId = task.DesignProjectId,
                DesignProjectName = task.DesignProject.Name,
                AssigneeName = task.AssigneeMember.User.FullName,
                SubmittedAt = task.SubmittedAt,
            })
            .ToListAsync(ct);

        var reviews = await db.DesignReviews.AsNoTracking()
            .Where(review => review.Status == DesignReviewStatus.InternalReview
                && review.Recipients.Any(recipient => recipient.UserId == userId))
            .OrderBy(review => review.InternalSubmittedAt)
            .Select(review => new DesignApprovalInboxReviewResponse
            {
                ReviewId = review.Id,
                Title = review.Title,
                Stage = review.Stage.ToString(),
                DesignProjectId = review.DesignProjectId,
                DesignProjectName = review.DesignProject.Name,
                SubmittedAt = review.InternalSubmittedAt,
                HasMyComment = review.Comments.Any(comment => comment.AuthorUserId == userId),
            })
            .ToListAsync(ct);

        return new DesignApprovalInboxResponse { Tasks = tasks, Reviews = reviews };
    }
}
