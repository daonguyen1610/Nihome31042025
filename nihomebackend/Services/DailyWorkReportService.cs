using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed class DailyWorkReportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Raised when the caller may not see the department's reports; never names the record.</summary>
public sealed class DailyWorkReportScopeException() : Exception("Not allowed.");

/// <summary>
/// Daily work report (N-09, QLNS-M05). A person submits today's report, prefilled
/// from their in-progress work; the head of their department (a member of the
/// same department holding work.daily-reports.review) reads it and comments.
/// </summary>
public sealed class DailyWorkReportService(AppDbContext db, MyWorkService myWork, IPermissionService permissions)
{
    public const string NoDepartment = "DAILY_REPORT_NO_DEPARTMENT";
    public const string Reviewed = "DAILY_REPORT_REVIEWED";
    public const string OwnReport = "DAILY_REPORT_OWN_REVIEW";
    public const string ReviewPermission = "work.daily-reports.review";
    private const int HistorySize = 30;

    /// <summary>Today's report, or a draft prefilled from the work the person is doing today.</summary>
    public async Task<DailyWorkReportResponse> GetTodayAsync(int userId, CancellationToken ct = default)
    {
        var today = await DisplaySettings.TodayAsync(db, ct);
        var existing = await Reports().SingleOrDefaultAsync(item => item.UserId == userId && item.ReportDate == today, ct);
        if (existing is not null) return Map(existing, userId, false, today);

        var work = await myWork.GetAsync(userId, null, null, ct);
        var name = await db.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => item.FullName ?? item.Email).SingleAsync(ct);
        return new DailyWorkReportResponse
        {
            UserId = userId,
            UserName = name ?? string.Empty,
            ReportDate = today,
            CanEdit = true,
            Items = work.Items
                .Where(item => item.Relation == "Assigned" &&
                    (item.Status == "InProgress" || item.IsToday || item.IsOverdue))
                .Select(item => new DailyWorkReportItemResponse
                {
                    SourceType = item.Source,
                    SourceId = item.Id,
                    Title = $"{item.Code} · {item.Title}",
                    ProgressPercent = item.ProgressPercent,
                }).ToList(),
        };
    }

    public async Task<IReadOnlyList<DailyWorkReportResponse>> HistoryAsync(int userId, CancellationToken ct = default)
    {
        var today = await DisplaySettings.TodayAsync(db, ct);
        var rows = await Reports().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.ReportDate).Take(HistorySize).ToListAsync(ct);
        return rows.Select(item => Map(item, userId, false, today)).ToList();
    }

    /// <summary>Creates or replaces the caller's report for today, until their head has commented.</summary>
    public async Task<DailyWorkReportResponse> SubmitTodayAsync(
        int userId, SubmitDailyWorkReportRequest request, CancellationToken ct = default)
    {
        var today = await DisplaySettings.TodayAsync(db, ct);
        var departmentId = await DepartmentOfAsync(userId, ct) ??
            throw new DailyWorkReportException(NoDepartment,
                "Tài khoản chưa thuộc phòng ban nào nên chưa có trưởng bộ phận nhận báo cáo. Liên hệ quản trị viên.");
        var report = await db.DailyWorkReports.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.UserId == userId && item.ReportDate == today, ct);
        var now = DateTime.UtcNow;
        if (report is null)
        {
            report = new DailyWorkReport { UserId = userId, ReportDate = today, SubmittedAt = now };
            db.DailyWorkReports.Add(report);
        }
        else
        {
            if (report.ReviewedAt.HasValue)
                throw new DailyWorkReportException(Reviewed,
                    "Trưởng bộ phận đã nhận xét báo cáo hôm nay nên không thể sửa nữa.");
            CrmConcurrency.Apply(db, report, request.RowVersion);
            db.DailyWorkReportItems.RemoveRange(report.Items);
            report.Items = new();
        }
        report.DepartmentRoleGroupId = departmentId;
        report.Proposals = string.IsNullOrWhiteSpace(request.Proposals) ? null : request.Proposals.Trim();
        report.TomorrowPlan = request.TomorrowPlan.Trim();
        report.UpdatedAt = now;
        report.Items = request.Items.Select((item, index) => new DailyWorkReportItem
        {
            SourceType = item.SourceType is MyWorkSources.WorkTask or MyWorkSources.DesignTask ? item.SourceType : null,
            SourceId = item.SourceType is MyWorkSources.WorkTask or MyWorkSources.DesignTask ? item.SourceId : null,
            Title = item.Title.Trim(),
            ProgressPercent = item.ProgressPercent,
            Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim(),
            SortOrder = index,
        }).ToList();
        try
        {
            await CrmConcurrency.SaveChangesAsync(db, ct);
        }
        catch (DbUpdateException) when (db.Entry(report).State == EntityState.Added)
        {
            // A second first-submit of the same day (double click, two tabs) hits the unique (UserId, ReportDate) index.
            throw new CrmConcurrencyException("Báo cáo hôm nay vừa được gửi từ nơi khác. Vui lòng tải lại trang rồi gửi lại.");
        }
        return await GetTodayAsync(userId, ct);
    }

    /// <summary>
    /// One day of a department's reports plus the members who have not reported.
    /// A head sees their own department; <paramref name="canViewAll"/> may pick any.
    /// </summary>
    public async Task<DepartmentDailyReportsResponse> DepartmentAsync(
        int callerUserId, DateOnly? date, int? departmentId, bool canViewAll, CancellationToken ct = default)
    {
        var today = await DisplaySettings.TodayAsync(db, ct);
        var day = date ?? today;
        var groupId = canViewAll && departmentId.HasValue
            ? departmentId.Value
            : await HeadedDepartmentAsync(callerUserId, ct) ?? throw new DailyWorkReportScopeException();
        if (departmentId.HasValue && departmentId != groupId) throw new DailyWorkReportScopeException();
        var labelKey = await db.RoleGroups.AsNoTracking().Where(item => item.Id == groupId)
            .Select(item => item.LabelKey).SingleOrDefaultAsync(ct) ?? throw new DailyWorkReportScopeException();

        var reports = await Reports().Where(item => item.DepartmentRoleGroupId == groupId && item.ReportDate == day)
            .OrderBy(item => item.User.FullName).ToListAsync(ct);
        var reported = reports.Select(item => item.UserId).ToHashSet();
        var members = await db.Users.AsNoTracking()
            .Where(item => item.IsActive && item.RoleEntity != null && item.RoleEntity.RoleGroupId == groupId)
            .OrderBy(item => item.FullName ?? item.Email)
            .Select(item => new DailyReportMemberResponse { UserId = item.Id, Name = item.FullName ?? item.Email ?? string.Empty })
            .ToListAsync(ct);
        return new DepartmentDailyReportsResponse
        {
            ReportDate = day,
            DepartmentRoleGroupId = groupId,
            DepartmentLabelKey = labelKey,
            Reports = reports.Select(item => Map(item, callerUserId, true, today)).ToList(),
            MissingMembers = members.Where(item => !reported.Contains(item.UserId)).ToList(),
        };
    }

    public async Task<DailyWorkReportResponse> ReviewAsync(
        int callerUserId, int reportId, ReviewDailyWorkReportRequest request, bool canViewAll, CancellationToken ct = default)
    {
        var report = await db.DailyWorkReports.SingleOrDefaultAsync(item => item.Id == reportId, ct) ??
            throw new DailyWorkReportScopeException();
        if (!canViewAll && await HeadedDepartmentAsync(callerUserId, ct) != report.DepartmentRoleGroupId)
            throw new DailyWorkReportScopeException();
        if (report.UserId == callerUserId)
            throw new DailyWorkReportException(OwnReport, "Bạn không thể tự nhận xét báo cáo của chính mình.");
        CrmConcurrency.Apply(db, report, request.RowVersion);
        report.ReviewComment = request.Comment.Trim();
        report.ReviewedByUserId = callerUserId;
        report.ReviewedAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        var saved = await Reports().SingleAsync(item => item.Id == reportId, ct);
        return Map(saved, callerUserId, true, await DisplaySettings.TodayAsync(db, ct));
    }

    private IQueryable<DailyWorkReport> Reports() => db.DailyWorkReports.AsNoTracking()
        .Include(item => item.User)
        .Include(item => item.ReviewedByUser)
        .Include(item => item.Items.OrderBy(line => line.SortOrder));

    private Task<int?> DepartmentOfAsync(int userId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.IsActive && item.RoleEntity != null && item.RoleEntity.IsActive)
            .Select(item => item.RoleEntity!.RoleGroupId).SingleOrDefaultAsync(ct);

    /// <summary>The department the caller heads: their own, when they hold the review permission.</summary>
    private async Task<int?> HeadedDepartmentAsync(int userId, CancellationToken ct) =>
        await permissions.HasAsync(userId, ReviewPermission, ct) ? await DepartmentOfAsync(userId, ct) : null;

    private static DailyWorkReportResponse Map(DailyWorkReport item, int callerUserId, bool asReviewer, DateOnly today) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        UserName = item.User.FullName ?? item.User.Email ?? string.Empty,
        ReportDate = item.ReportDate,
        Proposals = item.Proposals,
        TomorrowPlan = item.TomorrowPlan,
        SubmittedAt = item.SubmittedAt,
        UpdatedAt = item.UpdatedAt,
        ReviewComment = item.ReviewComment,
        ReviewedByName = item.ReviewedByUser == null ? null : item.ReviewedByUser.FullName ?? item.ReviewedByUser.Email,
        ReviewedAt = item.ReviewedAt,
        CanEdit = item.UserId == callerUserId && item.ReportDate == today && item.ReviewedAt is null,
        CanReview = asReviewer && item.UserId != callerUserId,
        RowVersion = CrmConcurrency.Encode(item.RowVersion),
        Items = item.Items.OrderBy(line => line.SortOrder).Select(line => new DailyWorkReportItemResponse
        {
            SourceType = line.SourceType,
            SourceId = line.SourceId,
            Title = line.Title,
            ProgressPercent = line.ProgressPercent,
            Note = line.Note,
        }).ToList(),
    };
}
