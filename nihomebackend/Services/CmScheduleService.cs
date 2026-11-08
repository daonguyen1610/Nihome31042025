using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.Services;

public sealed class CmScheduleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class CmScheduleService(AppDbContext db, IConstructionTaskService constructionTasks, IPermissionService permissions, CmScheduleMailService mail)
{
    private static void Require(bool valid, string code) { if (!valid) throw new CmScheduleException(code); }
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static List<CmScheduleLine> Lines(CmScheduleBaseline baseline) => JsonSerializer.Deserialize<List<CmScheduleLine>>(baseline.ItemsJson)!;
    private IQueryable<ConstructionTask> Tasks(int projectId) => db.ConstructionTasks.Where(x => x.DesignProject.OperationalProjectId == projectId && x.Status != ConstructionTaskStatus.Cancelled);
    private IQueryable<OperationalProjectMember> Members(int projectId) => db.OperationalProjectMembers.Where(x =>
        x.OperationalProjectId == projectId && x.StartedAt <= DateTime.UtcNow && x.EndedAt == null && x.User.IsActive);

    public async Task<object> WorkspaceAsync(int projectId, int actor, CancellationToken ct)
    {
        var project = await db.OperationalProjects.AsNoTracking().SingleAsync(x => x.Id == projectId, ct);
        var crm = await db.ProjectScheduleRevisions.AsNoTracking().Include(x => x.Items)
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectScheduleRevisionStatus.Published)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        var tasks = await Tasks(projectId).AsNoTracking().Include(x => x.Predecessors).ToListAsync(ct);
        var canSeeDrafts = project.ProjectManagerUserId == actor || await permissions.HasAsync(actor, "construction.cm-preparation.manage", ct);
        var baselines = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.OperationalProjectId == projectId && (canSeeDrafts || x.Status == "Published"))
            .OrderByDescending(x => x.Version).ToListAsync(ct);
        var snapshots = await db.CmProgressSnapshots.AsNoTracking().Where(x => x.OperationalProjectId == projectId)
            .OrderByDescending(x => x.CapturedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var emailDeliveries = await db.ProjectScheduleEmails.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.CmScheduleBaselineId.HasValue)
            .Select(x => new { x.Id, baselineId = x.CmScheduleBaselineId, x.Status, x.AttemptCount }).ToListAsync(ct);
        var crmNodes = crm?.Items.ToDictionary(x => x.NodeId) ?? [];
        var eligible = tasks.Where(x => x.ProjectScheduleNodeId.HasValue && crmNodes.ContainsKey(x.ProjectScheduleNodeId.Value)).ToList();
        var proposed = eligible.Select(x =>
        {
            var node = crmNodes[x.ProjectScheduleNodeId!.Value];
            var leaf = !tasks.Any(child => child.DesignProjectId == x.DesignProjectId && !string.IsNullOrWhiteSpace(x.Wbs) &&
                child.Id != x.Id && child.Wbs != null && child.Wbs.StartsWith(x.Wbs + ".", StringComparison.Ordinal));
            return new CmScheduleLine(x.Id, x.DesignProjectId, node.NodeId, x.Wbs ?? x.TaskCode, x.Name,
                node.PlannedStart, node.PlannedEnd, x.PlannedStart, x.PlannedEnd,
                Math.Max(1, x.PlannedEnd.DayNumber - x.PlannedStart.DayNumber + 1), leaf,
                x.Predecessors.Select(p => p.PredecessorTaskId).ToList());
        }).ToList();
        var candidates = await Members(projectId).Select(x => x.UserId).Distinct().ToListAsync(ct);
        var eligibleMembers = await permissions.FilterByRolePermissionAsync(candidates, "construction.tasks.view", ct);
        var members = await Members(projectId).Where(x => eligibleMembers.Contains(x.UserId)).Select(x => new { x.UserId, Name = x.User.FullName ?? x.User.PhoneNumber }).Distinct().OrderBy(x => x.Name).ToListAsync(ct);
        return new
        {
            asOfDate = Today,
            projectManagerUserId = project.ProjectManagerUserId,
            crmRevisionId = crm?.Id,
            crmVersion = crm?.Version,
            thresholdPercent = await ThresholdAsync(ct),
            proposed,
            members,
            emailDeliveries,
            unlinked = tasks.Where(x => !eligible.Any(e => e.Id == x.Id)).Select(x => new { x.Id, x.TaskCode, x.Name, x.DesignProjectId }),
            actualInputs = tasks.Select(x => new { taskId = x.Id, x.ProgressPercent, updatedAt = Utc(x.UpdatedAt) }),
            baselines = baselines.Select(MapBaseline),
            snapshots = snapshots.Select(MapSnapshot)
        };
    }

    public async Task<object> CreateAsync(int projectId, CmScheduleDraftRequest request, int userId, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var project = await db.OperationalProjects.SingleAsync(x => x.Id == projectId, ct);
        Require(project.ProjectManagerUserId.HasValue && await permissions.HasAsync(project.ProjectManagerUserId.Value, "construction.cm-preparation.view", ct) && await permissions.HasAsync(project.ProjectManagerUserId.Value, "construction.tasks.view", ct), "pmRequired");
        Require(await Members(projectId).AnyAsync(x => x.UserId == request.SiteManagerUserId, ct) && await permissions.HasAsync(request.SiteManagerUserId, "construction.tasks.view", ct), "siteManagerRequired");
        Require(request.ChangeReason.Trim().Length >= 3, "reasonRequired");
        Require(await db.Contracts.AnyAsync(x => x.OperationalProjectId == projectId && x.Direction == ContractDirection.Upstream &&
            (x.Type == ContractType.Construction || x.Type == ContractType.DesignAndBuild || x.Type == ContractType.PermitAndConstruction || x.Type == ContractType.DesignPermitAndConstruction) &&
            (x.Status == ContractStatus.Signed || x.Status == ContractStatus.InProgress || x.Status == ContractStatus.OnHold), ct), "signedContractRequired");
        Require(!await db.CmScheduleBaselines.AnyAsync(x => x.OperationalProjectId == projectId && (x.Status == "Draft" || x.Status == "PendingApproval"), ct), "draftExists");
        var crm = await db.ProjectScheduleRevisions.AsNoTracking().Include(x => x.Items)
            .Where(x => x.OperationalProjectId == projectId && x.Status == ProjectScheduleRevisionStatus.Published)
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        Require(crm is not null, "crmRequired");
        var tasks = await Tasks(projectId).Include(x => x.Predecessors).ToListAsync(ct);
        Require(tasks.Count != 0 && tasks.Count <= 2000 && request.Items.Count == tasks.Count && request.Items.Select(x => x.TaskId).Distinct().Count() == tasks.Count, "taskSetInvalid");
        var nodes = crm!.Items.ToDictionary(x => x.NodeId);
        var lines = new List<CmScheduleLine>();
        foreach (var task in tasks)
        {
            Require(task.ProjectScheduleNodeId.HasValue && nodes.ContainsKey(task.ProjectScheduleNodeId.Value), "unlinkedTasks");
            var node = nodes[task.ProjectScheduleNodeId!.Value];
            var input = request.Items.SingleOrDefault(x => x.TaskId == task.Id);
            Require(input is not null, "taskSetInvalid");
            Require(input!.PlannedStart != default && input.PlannedEnd >= input.PlannedStart && input.PlannedStart >= node.PlannedStart && input.PlannedEnd <= node.PlannedEnd, "outsideCrm");
            Require(input.Weight > 0 && input.Weight <= 1000000000m, "weightInvalid");
            var leaf = !tasks.Any(x => x.DesignProjectId == task.DesignProjectId && !string.IsNullOrWhiteSpace(task.Wbs) &&
                x.Id != task.Id && x.Wbs != null && x.Wbs.StartsWith(task.Wbs + ".", StringComparison.Ordinal));
            lines.Add(new(task.Id, task.DesignProjectId, node.NodeId, task.Wbs ?? task.TaskCode, task.Name,
                node.PlannedStart, node.PlannedEnd, input.PlannedStart, input.PlannedEnd, input.Weight, leaf,
                task.Predecessors.Select(x => x.PredecessorTaskId).ToList()));
        }
        Require(lines.GroupBy(x => new { x.DesignProjectId, x.Wbs }).All(x => x.Count() == 1), "taskSetInvalid");
        foreach (var line in lines)
        {
            var parents = lines.Where(x => x.DesignProjectId == line.DesignProjectId && line.Wbs.StartsWith(x.Wbs + ".", StringComparison.Ordinal));
            Require(parents.All(x => x.PlannedStart <= line.PlannedStart && x.PlannedEnd >= line.PlannedEnd), "outsideCrm");
        }
        var resolved = new HashSet<int>();
        while (resolved.Count < lines.Count)
        {
            var next = lines.Where(x => !resolved.Contains(x.TaskId) && x.PredecessorIds.All(resolved.Contains)).ToList();
            Require(next.Count > 0, "predecessorInvalid");
            foreach (var line in next) resolved.Add(line.TaskId);
        }
        foreach (var line in lines)
            foreach (var predecessor in line.PredecessorIds)
            {
                var before = lines.SingleOrDefault(x => x.TaskId == predecessor);
                Require(before is not null && line.PlannedStart >= before.PlannedEnd, "predecessorInvalid");
            }
        var baseline = new CmScheduleBaseline
        {
            OperationalProjectId = projectId,
            Version = (await db.CmScheduleBaselines.Where(x => x.OperationalProjectId == projectId).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1,
            CrmRevisionId = crm.Id,
            SiteManagerUserId = request.SiteManagerUserId,
            ApproverUserId = project.ProjectManagerUserId!.Value,
            ChangeReason = request.ChangeReason.Trim(),
            CreatedByUserId = userId,
            ItemsJson = JsonSerializer.Serialize(lines)
        };
        db.CmScheduleBaselines.Add(baseline);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapBaseline(baseline);
    }

    public async Task<object> CancelDraftAsync(int projectId, int id, string token, int actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await BaselineAsync(projectId, id, ct);
        Require((row.Status == "Draft" || row.Status == "PendingApproval") && row.CreatedByUserId == actor, "authorRequired");
        CrmConcurrency.Apply(db, row, token);
        row.Status = "Cancelled";
        var pendingEmails = await db.ProjectScheduleEmails.Where(x => x.CmScheduleBaselineId == id && (x.Status == ProjectScheduleEmail.PendingStatus || x.Status == ProjectScheduleEmail.NeedsReviewStatus)).ToListAsync(ct);
        foreach (var email in pendingEmails) email.Status = ProjectScheduleEmail.CancelledStatus;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapBaseline(row);
    }

    public async Task<object> PreviewEmailAsync(int projectId, int id, int actor, CancellationToken ct)
    {
        var row = await BaselineAsync(projectId, id, ct);
        Require(row.Status == "Draft" && row.CreatedByUserId == actor, "authorRequired");
        return await mail.PreviewAsync(row, actor, ct);
    }
    public async Task<object> SubmitAsync(int projectId, int id, string token, string previewToken, int actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await BaselineAsync(projectId, id, ct);
        Require(row.Status == "Draft", "stateInvalid");
        Require(row.CreatedByUserId == actor, "authorRequired");
        Require(row.CreatedByUserId != row.ApproverUserId, "independentReview");
        Require(await Members(projectId).AnyAsync(x => x.UserId == row.SiteManagerUserId, ct) && await permissions.HasAsync(row.SiteManagerUserId, "construction.tasks.view", ct), "siteManagerRequired");
        var currentPm = await db.OperationalProjects.Where(x => x.Id == projectId).Select(x => x.ProjectManagerUserId).SingleAsync(ct);
        Require(currentPm == row.ApproverUserId && await db.Users.AnyAsync(x => x.Id == currentPm && x.IsActive, ct), "pmRequired");
        CrmConcurrency.Apply(db, row, token);
        await mail.QueueAsync(row, actor, previewToken, ct);
        row.Status = "PendingApproval";
        await QueueNotificationAsync(row.ApproverUserId, projectId, "cmSchedule.notice.approval", row.Version.ToString(CultureInfo.InvariantCulture), ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapBaseline(row);
    }

    public async Task<object> DecideAsync(int projectId, int id, CmScheduleDecisionRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var row = await BaselineAsync(projectId, id, ct);
        var pm = await db.OperationalProjects.Where(x => x.Id == projectId).Select(x => x.ProjectManagerUserId).SingleAsync(ct);
        Require(row.ApproverUserId == actor && pm == actor && actor != row.CreatedByUserId, "independentReview");
        Require(row.Status == "PendingApproval", "stateInvalid");
        Require(request.Approve || request.Note?.Trim().Length >= 3, "reasonRequired");
        if (request.Approve)
        {
            Require(await Members(projectId).AnyAsync(x => x.UserId == row.SiteManagerUserId, ct) && await permissions.HasAsync(row.SiteManagerUserId, "construction.tasks.view", ct), "siteManagerRequired");
            var latestCrm = await db.ProjectScheduleRevisions.Where(x => x.OperationalProjectId == projectId && x.Status == ProjectScheduleRevisionStatus.Published)
                .OrderByDescending(x => x.Version).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            Require(latestCrm == row.CrmRevisionId, "crmChanged");
            var tasks = await Tasks(projectId).AsNoTracking().Include(x => x.Predecessors).ToListAsync(ct);
            var frozen = Lines(row);
            Require(CmScheduleScope.Matches(frozen, tasks), "taskSetInvalid");
        }
        CrmConcurrency.Apply(db, row, request.RowVersion);
        row.Status = request.Approve ? "Published" : "Rejected";
        row.ReviewedByUserId = actor; row.ReviewNote = request.Note?.Trim();
        if (request.Approve) row.PublishedAt = DateTime.UtcNow;
        foreach (var recipient in (request.Approve ? new[] { row.CreatedByUserId, row.SiteManagerUserId } : new[] { row.CreatedByUserId }).Distinct())
            await QueueNotificationAsync(recipient, projectId, request.Approve ? "cmSchedule.notice.published" : "cmSchedule.notice.rejected", row.Version.ToString(CultureInfo.InvariantCulture), ct);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapBaseline(row);
    }

    public async Task<object> CaptureAsync(int projectId, int? actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var baseline = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Status == "Published")
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        Require(baseline is not null, "baselineRequired");
        var lines = Lines(baseline!);
        var currentTasks = await Tasks(projectId).AsNoTracking().Include(x => x.Predecessors).ToListAsync(ct);
        Require(CmScheduleScope.Matches(lines, currentTasks), "actualIncomplete");
        Require(currentTasks.All(x => x.ProgressPercent >= 0 && x.ProgressPercent <= 100), "actualIncomplete");
        var tasks = currentTasks.Select(x => new CmActualInput(x.Id, x.ProgressPercent, x.UpdatedAt, x.Status.ToString())).ToList();
        Require(tasks.Select(x => x.TaskId).Order().SequenceEqual(lines.Select(x => x.TaskId).Order()), "actualIncomplete");
        var totals = CmScheduleMath.Calculate(lines, tasks, Today);
        var threshold = await ThresholdAsync(ct);
        var inputsJson = JsonSerializer.Serialize(tasks.OrderBy(x => x.TaskId));
        var existing = await db.CmProgressSnapshots.AsNoTracking().Where(x => x.BaselineId == baseline!.Id && x.AsOfDate == Today && x.InputsJson == inputsJson && x.ThresholdPercent == threshold)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (existing is not null) return MapSnapshot(existing);
        var snapshot = new CmProgressSnapshot
        {
            OperationalProjectId = projectId,
            BaselineId = baseline!.Id,
            AsOfDate = Today,
            CapturedByUserId = actor,
            InputsJson = inputsJson,
            CrmPercent = totals.Crm,
            PlanPercent = totals.Plan,
            ActualPercent = totals.Actual,
            DelayPercent = totals.Delay,
            ThresholdPercent = threshold,
            IsDelayed = totals.Delay > threshold
        };
        db.CmProgressSnapshots.Add(snapshot);
        if (snapshot.IsDelayed && !await db.CmProgressSnapshots.AnyAsync(x => x.BaselineId == baseline.Id && x.AsOfDate == Today && x.IsDelayed, ct))
        {
            Require(await Members(projectId).AnyAsync(x => x.UserId == baseline.SiteManagerUserId, ct), "siteManagerRequired");
            await QueueNotificationAsync(baseline.SiteManagerUserId, projectId, "cmSchedule.notice.delay", baseline.Version.ToString(CultureInfo.InvariantCulture), ct);
        }
        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapSnapshot(snapshot);
    }

    public async Task<object> UpdateActualAsync(int projectId, int taskId, CmActualUpdateRequest request, int actor, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var baseline = await db.CmScheduleBaselines.AsNoTracking().Where(x => x.OperationalProjectId == projectId && x.Status == "Published")
            .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        Require(baseline is not null, "baselineRequired");
        Require(baseline!.SiteManagerUserId == actor && await Members(projectId).AnyAsync(x => x.UserId == actor, ct), "siteManagerRequired");
        Require(Lines(baseline).Any(x => x.TaskId == taskId && x.IsLeaf), "taskSetInvalid");
        var task = await Tasks(projectId).SingleOrDefaultAsync(x => x.Id == taskId, ct);
        Require(task is not null, "actualIncomplete");
        Require(request.ProgressPercent.HasValue && request.ProgressPercent >= 0 && request.ProgressPercent <= 100, "actualIncomplete");
        if (request.ExpectedUpdatedAt != task!.UpdatedAt) throw new CrmConcurrencyException("Dữ liệu đã đổi. Tải lại tiến độ BCH trước khi gửi lại.");
        if (request.ProgressPercent < 100 && task.Status == ConstructionTaskStatus.Completed) task.ActualEnd = null;
        await constructionTasks.UpdateProgressAsync(taskId, new UpdateConstructionTaskProgressRequest
        {
            ProgressPercent = request.ProgressPercent!.Value,
            ActualStart = request.ProgressPercent > 0 ? task.ActualStart ?? Today : null,
            Status = request.ProgressPercent < 100 && task.Status == ConstructionTaskStatus.Completed ? nameof(ConstructionTaskStatus.InProgress) : null
        }, actor, ct);
        await CaptureAsync(projectId, actor, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return await WorkspaceAsync(projectId, actor, ct);
    }

    public async Task<object> RecoverAsync(int projectId, int id, CmRecoveryRequest request, int actor, CancellationToken ct)
    {
        var row = await db.CmProgressSnapshots.Include(x => x.Baseline).SingleOrDefaultAsync(x => x.Id == id && x.OperationalProjectId == projectId, ct);
        Require(row is not null, "snapshotRequired");
        Require(row!.IsDelayed && row.RecoveryAt is null, "stateInvalid");
        Require(row.Baseline.SiteManagerUserId == actor && await Members(projectId).AnyAsync(x => x.UserId == actor, ct), "siteManagerRequired");
        Require(request.Explanation.Trim().Length >= 3 && request.RecoveryPlan.Trim().Length >= 3 && request.DueDate >= Today, "recoveryRequired");
        CrmConcurrency.Apply(db, row, request.RowVersion);
        row.Explanation = request.Explanation.Trim(); row.RecoveryPlan = request.RecoveryPlan.Trim(); row.RecoveryDueDate = request.DueDate;
        row.RecoveryByUserId = actor; row.RecoveryAt = DateTime.UtcNow;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapSnapshot(row);
    }

    public async Task<object> ReviewRecoveryAsync(int projectId, int id, string token, int actor, CancellationToken ct)
    {
        var row = await db.CmProgressSnapshots.SingleOrDefaultAsync(x => x.Id == id && x.OperationalProjectId == projectId, ct);
        Require(row is not null, "snapshotRequired");
        Require(row!.RecoveryAt.HasValue && !row.ReviewedAt.HasValue, "stateInvalid");
        Require(row.RecoveryByUserId != actor, "independentReview");
        CrmConcurrency.Apply(db, row, token);
        row.ReviewedAt = DateTime.UtcNow; row.ReviewedByUserId = actor;
        await CrmConcurrency.SaveChangesAsync(db, ct);
        return MapSnapshot(row);
    }

    private async Task<decimal> ThresholdAsync(CancellationToken ct)
    {
        var value = await db.SiteSettings.Select(x => (decimal?)x.CmScheduleDelayThresholdPercent).FirstOrDefaultAsync(ct);
        Require(value.HasValue && value >= 0 && value <= 100, "policyRequired");
        return value!.Value;
    }
    private async Task<CmScheduleBaseline> BaselineAsync(int projectId, int id, CancellationToken ct) =>
        await db.CmScheduleBaselines.SingleOrDefaultAsync(x => x.Id == id && x.OperationalProjectId == projectId, ct) ?? throw new CmScheduleException("baselineRequired");
    private async Task QueueNotificationAsync(int userId, int projectId, string key, string version, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var projectCode = await db.OperationalProjects.Where(x => x.Id == projectId).Select(x => x.Code).SingleAsync(ct);
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Module = "construction.cm-preparation",
            Title = key,
            Body = projectCode + " · v" + version,
            LinkUrl = $"/admin/construction/progress?projectId={projectId}",
            RefEntityType = nameof(OperationalProject),
            RefEntityId = projectId
        });
    }
    private static object Curve(CmScheduleBaseline baseline)
    {
        var lines = Lines(baseline);
        var actual = lines.Where(x => x.IsLeaf).Select(x => new CmActualInput(x.TaskId, 0, default, "")).ToList();
        var days = lines.Where(x => x.IsLeaf).SelectMany(x => new[] { x.CrmStart == DateOnly.MinValue ? x.CrmStart : x.CrmStart.AddDays(-1), x.CrmEnd,
            x.PlannedStart == DateOnly.MinValue ? x.PlannedStart : x.PlannedStart.AddDays(-1), x.PlannedEnd }).Distinct().Order();
        return days.Select(day => { var totals = CmScheduleMath.Calculate(lines, actual, day); return new { day, crmPercent = totals.Crm, planPercent = totals.Plan }; }).ToList();
    }
    private static DateTime? Utc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
    private static object MapBaseline(CmScheduleBaseline x) => new
    {
        x.Id,
        x.Version,
        x.CrmRevisionId,
        x.Status,
        x.ChangeReason,
        items = Lines(x),
        curve = Curve(x),
        x.SiteManagerUserId,
        x.ApproverUserId,
        x.CreatedByUserId,
        x.ReviewedByUserId,
        x.ReviewNote,
        createdAt = Utc(x.CreatedAt),
        publishedAt = Utc(x.PublishedAt),
        rowVersion = CrmConcurrency.Encode(x.RowVersion)
    };
    private static object MapSnapshot(CmProgressSnapshot x) => new
    {
        x.Id,
        x.BaselineId,
        x.AsOfDate,
        capturedAt = Utc(x.CapturedAt),
        x.CapturedByUserId,
        x.CrmPercent,
        x.PlanPercent,
        x.ActualPercent,
        x.DelayPercent,
        x.ThresholdPercent,
        x.IsDelayed,
        x.Explanation,
        x.RecoveryPlan,
        x.RecoveryDueDate,
        x.RecoveryByUserId,
        recoveryAt = Utc(x.RecoveryAt),
        x.ReviewedByUserId,
        reviewedAt = Utc(x.ReviewedAt),
        rowVersion = CrmConcurrency.Encode(x.RowVersion)
    };
}
