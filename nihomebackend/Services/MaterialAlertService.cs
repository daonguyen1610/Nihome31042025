using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Services.Audit;

namespace NihomeBackend.Services;

public sealed class MaterialAlertService(
    AppDbContext db,
    INotificationService notifications,
    IAuditLogger audit,
    ILogger<MaterialAlertService> logger) : IMaterialAlertService
{
    private const string DetectedTemplate = "procurement.material-alert.detected";
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> EvaluationGates = new();

    public async Task<MaterialAlertListResponse> ListAsync(
        int projectId,
        MaterialAlertListParams parameters,
        CancellationToken ct = default)
    {
        var query = BaseQuery().Where(item => item.OperationalProjectId == projectId);
        if (parameters.Type.HasValue) query = query.Where(item => item.Type == parameters.Type.Value);
        if (parameters.Status.HasValue) query = query.Where(item => item.Status == parameters.Status.Value);
        if (parameters.Severity.HasValue) query = query.Where(item => item.Severity == parameters.Severity.Value);
        if (parameters.AssignedToUserId.HasValue) query = query.Where(item => item.AssignedToUserId == parameters.AssignedToUserId.Value);
        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var pattern = $"%{parameters.Search.Trim()}%";
            query = query.Where(item => EF.Functions.Like(item.Code, pattern) ||
                EF.Functions.Like(item.ItemCode, pattern) ||
                EF.Functions.Like(item.Description, pattern) ||
                EF.Functions.Like(item.AssignedToUser.FullName, pattern));
        }

        var descending = string.Equals(parameters.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<MaterialAlert> ordered = (parameters.SortBy, descending) switch
        {
            ("itemCode", false) => query.OrderBy(item => item.ItemCode),
            ("itemCode", true) => query.OrderByDescending(item => item.ItemCode),
            ("severity", false) => query.OrderBy(item => item.Severity),
            ("severity", true) => query.OrderByDescending(item => item.Severity),
            ("variance", false) => query.OrderBy(item => item.VarianceQuantity),
            ("variance", true) => query.OrderByDescending(item => item.VarianceQuantity),
            ("updatedAt", false) => query.OrderBy(item => item.UpdatedAt),
            ("updatedAt", true) => query.OrderByDescending(item => item.UpdatedAt),
            (_, false) => query.OrderBy(item => item.DetectedAt),
            _ => query.OrderByDescending(item => item.DetectedAt),
        };
        var rows = await ordered.ThenByDescending(item => item.Id)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync(ct);
        var statusCounts = await db.MaterialAlerts.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key.ToString(), Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, ct);
        return new MaterialAlertListResponse
        {
            Total = await query.CountAsync(ct),
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            StatusCounts = statusCounts,
            Items = rows.Select(Map).ToList(),
        };
    }

    public async Task<MaterialAlertResponse?> GetAsync(
        int projectId,
        int id,
        CancellationToken ct = default)
    {
        var alert = await BaseQuery().SingleOrDefaultAsync(item =>
            item.Id == id && item.OperationalProjectId == projectId, ct);
        return alert is null ? null : Map(alert);
    }

    public async Task<IReadOnlyList<MaterialAlertResponse>> EvaluateProjectAsync(
        int projectId,
        int actorUserId,
        CancellationToken ct = default)
    {
        var gate = EvaluationGates.GetOrAdd(projectId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await EvaluateProjectCoreAsync(projectId, actorUserId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<MaterialAlertResponse>> EvaluateProjectCoreAsync(
        int projectId,
        int actorUserId,
        CancellationToken ct)
    {
        var project = await db.OperationalProjects.AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new { item.Code, item.ProjectManagerUserId })
            .SingleOrDefaultAsync(ct);
        if (project is null) throw new MaterialAlertOperationException("Dự án vận hành không tồn tại.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
        if (transaction is not null)
        {
            var lockResource = $"material-alerts:project:{projectId}";
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DECLARE @result int; EXEC @result = sp_getapplock @Resource={lockResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Unable to acquire material-alert evaluation lock.', 1;",
                ct);
        }

        var now = DateTime.UtcNow;
        var candidates = await DetectCandidatesAsync(projectId, project.ProjectManagerUserId, ct);
        var candidateKeys = candidates.Select(item => (item.Type, item.ItemCode)).ToHashSet();
        var existing = await db.MaterialAlerts.Include(item => item.Events)
            .Where(item => item.OperationalProjectId == projectId).ToListAsync(ct);
        var notify = new List<MaterialAlert>();
        var nextCode = NextCode(existing.Select(item => item.Code));

        foreach (var candidate in candidates)
        {
            var alert = existing.SingleOrDefault(item => item.Type == candidate.Type &&
                string.Equals(item.ItemCode, candidate.ItemCode, StringComparison.OrdinalIgnoreCase));
            if (alert is null)
            {
                alert = new MaterialAlert
                {
                    OperationalProjectId = projectId,
                    ProjectBoqLineId = candidate.ProjectBoqLineId,
                    Code = $"MA-{nextCode++:D4}",
                    Type = candidate.Type,
                    Status = MaterialAlertStatus.Open,
                    DetectedAt = now,
                    CreatedAt = now,
                    Events = [],
                };
                ApplyMetrics(alert, candidate, now);
                alert.Events.Add(NewEvent(alert, MaterialAlertEventType.Detected, null,
                    MaterialAlertStatus.Open, null, actorUserId, now));
                db.MaterialAlerts.Add(alert);
                existing.Add(alert);
                notify.Add(alert);
            }
            else
            {
                var metricsChanged = MetricsChanged(alert, candidate);
                var previousStatus = alert.Status;
                ApplyMetrics(alert, candidate, now);
                if (previousStatus == MaterialAlertStatus.Resolved)
                {
                    alert.Status = MaterialAlertStatus.Open;
                    alert.DetectedAt = now;
                    alert.ResolvedAt = null;
                    alert.AcknowledgedAt = null;
                    alert.AcknowledgedByUserId = null;
                    alert.AcknowledgementNote = null;
                    alert.Events.Add(NewEvent(alert, MaterialAlertEventType.Reopened,
                        previousStatus, MaterialAlertStatus.Open, null, actorUserId, now));
                    notify.Add(alert);
                }
                else if (metricsChanged)
                {
                    alert.Events.Add(NewEvent(alert, MaterialAlertEventType.MetricsUpdated,
                        previousStatus, previousStatus, null, actorUserId, now));
                }
            }
        }

        foreach (var alert in existing.Where(item => item.Status != MaterialAlertStatus.Resolved &&
            !candidateKeys.Contains((item.Type, item.ItemCode))))
        {
            var previous = alert.Status;
            alert.Status = MaterialAlertStatus.Resolved;
            alert.ResolvedAt = now;
            alert.LastEvaluatedAt = now;
            alert.UpdatedAt = now;
            alert.Events.Add(NewEvent(alert, MaterialAlertEventType.AutoResolved,
                previous, MaterialAlertStatus.Resolved, "Nguồn dữ liệu không còn vi phạm.", actorUserId, now));
        }

        await CrmConcurrency.SaveChangesAsync(db, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        foreach (var alert in existing.Where(item => item.Events.Any(entry => entry.ChangedAt == now)))
            WriteAudit(alert, alert.Events.Last(entry => entry.ChangedAt == now));
        foreach (var alert in notify)
            await NotifyDetectedAsync(alert, project.Code, project.ProjectManagerUserId, ct);

        var ids = existing.Select(item => item.Id).ToList();
        var refreshed = await BaseQuery().Where(item => ids.Contains(item.Id))
            .OrderByDescending(item => item.DetectedAt).ToListAsync(ct);
        return refreshed.Select(Map).ToList();
    }

    public async Task<MaterialAlertResponse?> AcknowledgeAsync(
        int projectId,
        int id,
        AcknowledgeMaterialAlertRequest request,
        int actorUserId,
        CancellationToken ct = default)
    {
        var alert = await db.MaterialAlerts.Include(item => item.Events)
            .Include(item => item.OperationalProject)
            .SingleOrDefaultAsync(item => item.Id == id && item.OperationalProjectId == projectId, ct);
        if (alert is null) return null;
        if (alert.Status == MaterialAlertStatus.Resolved)
            throw new MaterialAlertOperationException("Cảnh báo đã được xử lý tự động và không thể xác nhận lại.");
        if (alert.AssignedToUserId != actorUserId && alert.OperationalProject.ProjectManagerUserId != actorUserId)
            throw new MaterialAlertOperationException("Chỉ người được giao hoặc quản lý dự án được xác nhận cảnh báo.");
        if (alert.Status == MaterialAlertStatus.Acknowledged)
            return await GetAsync(projectId, id, ct);

        CrmConcurrency.Apply(db, alert, request.RowVersion);
        var now = DateTime.UtcNow;
        alert.Status = MaterialAlertStatus.Acknowledged;
        alert.AcknowledgedAt = now;
        alert.AcknowledgedByUserId = actorUserId;
        alert.AcknowledgementNote = request.Note.Trim();
        alert.UpdatedAt = now;
        var entry = NewEvent(alert, MaterialAlertEventType.Acknowledged,
            MaterialAlertStatus.Open, MaterialAlertStatus.Acknowledged,
            alert.AcknowledgementNote, actorUserId, now);
        alert.Events.Add(entry);
        await CrmConcurrency.SaveChangesAsync(db, ct);
        WriteAudit(alert, entry);
        return await GetAsync(projectId, id, ct);
    }

    private async Task<List<AlertCandidate>> DetectCandidatesAsync(
        int projectId,
        int? projectManagerUserId,
        CancellationToken ct)
    {
        var revision = await db.ProjectBoqRevisions.AsNoTracking()
            .Where(item => item.OperationalProjectId == projectId && item.Status == ProjectBoqRevisionStatus.Approved)
            .OrderByDescending(item => item.ApprovedAt).ThenByDescending(item => item.RevisionNumber)
            .Select(item => new
            {
                Lines = item.Lines.Select(line => new
                {
                    line.Id,
                    line.ItemCode,
                    line.Description,
                    line.Unit,
                    line.ApprovedQuantity,
                }).ToList(),
            }).FirstOrDefaultAsync(ct);
        if (revision is null) return [];

        var receipts = await db.WarehouseReceiptLines.AsNoTracking().Where(line =>
                line.WarehouseReceipt.OperationalProjectId == projectId &&
                (line.WarehouseReceipt.Status == WarehouseLedgerStatus.Posted || line.WarehouseReceipt.Status == WarehouseLedgerStatus.Reversed))
            .Select(line => new
            {
                line.MaterialRequestLine.ProjectBoqLine.ItemCode,
                Quantity = line.WarehouseReceipt.ReversalOfReceiptId.HasValue ? -line.ReceivedQuantity : line.ReceivedQuantity,
            }).ToListAsync(ct);
        var issues = await db.WarehouseIssueLines.AsNoTracking().Where(line =>
                line.WarehouseIssue.OperationalProjectId == projectId &&
                (line.WarehouseIssue.Status == WarehouseLedgerStatus.Posted || line.WarehouseIssue.Status == WarehouseLedgerStatus.Reversed))
            .Select(line => new
            {
                line.WarehouseIssueId,
                line.WarehouseIssue.ResponsibleSiteUserId,
                line.WarehouseIssue.PostedAt,
                line.ProjectBoqLine.ItemCode,
                Quantity = line.WarehouseIssue.ReversalOfIssueId.HasValue ? -line.IssuedQuantity : line.IssuedQuantity,
            }).ToListAsync(ct);
        var demands = await db.MaterialRequestLines.AsNoTracking().Where(line =>
                line.MaterialRequest.OperationalProjectId == projectId &&
                (line.MaterialRequest.Status == MaterialRequestStatus.Approved ||
                 line.MaterialRequest.Status == MaterialRequestStatus.PartiallyFulfilled ||
                 line.MaterialRequest.Status == MaterialRequestStatus.Fulfilled))
            .Select(line => new
            {
                line.MaterialRequestId,
                line.MaterialRequest.AssignedProcurementUserId,
                line.MaterialRequest.UpdatedAt,
                line.ProjectBoqLine.ItemCode,
                line.RequestedQuantity,
            }).ToListAsync(ct);
        var receivedByCode = receipts.GroupBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity), StringComparer.OrdinalIgnoreCase);
        var issuedByCode = issues.GroupBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity), StringComparer.OrdinalIgnoreCase);
        var requestedByCode = demands.GroupBy(item => item.ItemCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.RequestedQuantity), StringComparer.OrdinalIgnoreCase);
        var candidates = new List<AlertCandidate>();

        foreach (var line in revision.Lines)
        {
            var received = receivedByCode.GetValueOrDefault(line.ItemCode);
            var issued = issuedByCode.GetValueOrDefault(line.ItemCode);
            var onHand = received - issued;
            var required = requestedByCode.GetValueOrDefault(line.ItemCode);
            if (issued > line.ApprovedQuantity)
            {
                var source = issues.Where(item => string.Equals(item.ItemCode, line.ItemCode, StringComparison.OrdinalIgnoreCase) && item.Quantity > 0)
                    .OrderByDescending(item => item.PostedAt).ThenByDescending(item => item.WarehouseIssueId).First();
                candidates.Add(new AlertCandidate(line.Id, line.ItemCode, line.Description, line.Unit,
                    MaterialAlertType.OverBoq, MaterialAlertSeverity.Critical,
                    line.ApprovedQuantity, required, received, issued, onHand,
                    issued - line.ApprovedQuantity, EntityTypes.WarehouseIssue, source.WarehouseIssueId,
                    source.ResponsibleSiteUserId));
            }

            var outstanding = Math.Max(required - issued, 0m);
            if (outstanding > onHand)
            {
                var source = demands.Where(item => string.Equals(item.ItemCode, line.ItemCode, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.MaterialRequestId).FirstOrDefault();
                if (source is not null)
                {
                    candidates.Add(new AlertCandidate(line.Id, line.ItemCode, line.Description, line.Unit,
                        MaterialAlertType.Shortage,
                        onHand <= 0 ? MaterialAlertSeverity.Critical : MaterialAlertSeverity.Warning,
                        line.ApprovedQuantity, required, received, issued, onHand,
                        outstanding - onHand, EntityTypes.MaterialRequest, source.MaterialRequestId,
                        source.AssignedProcurementUserId));
                }
            }
        }
        return candidates;
    }

    private IQueryable<MaterialAlert> BaseQuery() => db.MaterialAlerts.AsNoTracking()
        .Include(item => item.OperationalProject).ThenInclude(project => project.Customer)
        .Include(item => item.AssignedToUser)
        .Include(item => item.AcknowledgedBy)
        .Include(item => item.Events.OrderBy(entry => entry.ChangedAt)).ThenInclude(entry => entry.ChangedBy)
        .Include(item => item.OperationalProject).ThenInclude(project => project.Contracts).ThenInclude(contract => contract.Vendor);

    private static int NextCode(IEnumerable<string> codes) => codes
        .Select(code => code.StartsWith("MA-", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(code[3..], out var value) ? value : 0)
        .DefaultIfEmpty().Max() + 1;

    private static void ApplyMetrics(MaterialAlert alert, AlertCandidate candidate, DateTime now)
    {
        alert.ProjectBoqLineId = candidate.ProjectBoqLineId;
        alert.ItemCode = candidate.ItemCode;
        alert.Description = candidate.Description;
        alert.Unit = candidate.Unit;
        alert.Severity = candidate.Severity;
        alert.BoqAllowance = candidate.BoqAllowance;
        alert.RequiredQuantity = candidate.RequiredQuantity;
        alert.ReceivedQuantity = candidate.ReceivedQuantity;
        alert.IssuedQuantity = candidate.IssuedQuantity;
        alert.OnHandQuantity = candidate.OnHandQuantity;
        alert.VarianceQuantity = candidate.VarianceQuantity;
        alert.SourceEntityType = candidate.SourceEntityType;
        alert.SourceEntityId = candidate.SourceEntityId;
        alert.AssignedToUserId = candidate.AssignedToUserId;
        alert.LastEvaluatedAt = now;
        alert.UpdatedAt = now;
    }

    private static bool MetricsChanged(MaterialAlert alert, AlertCandidate candidate) =>
        alert.Severity != candidate.Severity ||
        alert.BoqAllowance != candidate.BoqAllowance ||
        alert.RequiredQuantity != candidate.RequiredQuantity ||
        alert.ReceivedQuantity != candidate.ReceivedQuantity ||
        alert.IssuedQuantity != candidate.IssuedQuantity ||
        alert.OnHandQuantity != candidate.OnHandQuantity ||
        alert.VarianceQuantity != candidate.VarianceQuantity ||
        alert.SourceEntityType != candidate.SourceEntityType ||
        alert.SourceEntityId != candidate.SourceEntityId ||
        alert.AssignedToUserId != candidate.AssignedToUserId;

    private static MaterialAlertEvent NewEvent(
        MaterialAlert alert,
        MaterialAlertEventType type,
        MaterialAlertStatus? from,
        MaterialAlertStatus to,
        string? reason,
        int actorUserId,
        DateTime now) => new()
        {
            MaterialAlert = alert,
            Type = type,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                alert.Type,
                alert.ItemCode,
                alert.Severity,
                alert.BoqAllowance,
                alert.RequiredQuantity,
                alert.ReceivedQuantity,
                alert.IssuedQuantity,
                alert.OnHandQuantity,
                alert.VarianceQuantity,
                alert.SourceEntityType,
                alert.SourceEntityId,
                alert.AssignedToUserId,
            }),
            ChangedByUserId = actorUserId,
            ChangedAt = now,
        };

    private static MaterialAlertResponse Map(MaterialAlert alert) => new()
    {
        Id = alert.Id,
        OperationalProjectId = alert.OperationalProjectId,
        OperationalProjectCode = alert.OperationalProject.Code,
        OperationalProjectName = alert.OperationalProject.Name,
        CustomerId = alert.OperationalProject.CustomerId,
        CustomerName = alert.OperationalProject.Customer.Name,
        ProjectBoqLineId = alert.ProjectBoqLineId,
        Code = alert.Code,
        Type = alert.Type.ToString(),
        Status = alert.Status.ToString(),
        Severity = alert.Severity.ToString(),
        ItemCode = alert.ItemCode,
        Description = alert.Description,
        Unit = alert.Unit,
        BoqAllowance = alert.BoqAllowance,
        RequiredQuantity = alert.RequiredQuantity,
        ReceivedQuantity = alert.ReceivedQuantity,
        IssuedQuantity = alert.IssuedQuantity,
        OnHandQuantity = alert.OnHandQuantity,
        VarianceQuantity = alert.VarianceQuantity,
        SourceEntityType = alert.SourceEntityType,
        SourceEntityId = alert.SourceEntityId,
        AssignedToUserId = alert.AssignedToUserId,
        AssignedToUserName = alert.AssignedToUser.FullName,
        DetectedAt = alert.DetectedAt,
        AcknowledgedAt = alert.AcknowledgedAt,
        AcknowledgedByUserId = alert.AcknowledgedByUserId,
        AcknowledgedByName = alert.AcknowledgedBy?.FullName,
        AcknowledgementNote = alert.AcknowledgementNote,
        ResolvedAt = alert.ResolvedAt,
        LastEvaluatedAt = alert.LastEvaluatedAt,
        CreatedAt = alert.CreatedAt,
        UpdatedAt = alert.UpdatedAt,
        RowVersion = CrmConcurrency.Encode(alert.RowVersion),
        Contracts = alert.OperationalProject.Contracts.OrderBy(contract => contract.ContractNumber)
            .Select(contract => new MaterialRequestContractContextResponse
            {
                Id = contract.Id,
                ContractNumber = contract.ContractNumber,
                Direction = contract.Direction.ToString(),
                Type = contract.Type.ToString(),
                VendorId = contract.VendorId,
                VendorName = contract.Vendor?.CompanyName,
                Status = contract.Status.ToString(),
            }).ToList(),
        Events = alert.Events.Select(entry => new MaterialAlertEventResponse
        {
            Id = entry.Id,
            Type = entry.Type.ToString(),
            FromStatus = entry.FromStatus?.ToString(),
            ToStatus = entry.ToStatus.ToString(),
            Reason = entry.Reason,
            ChangedByUserId = entry.ChangedByUserId,
            ChangedByName = entry.ChangedBy.FullName,
            ChangedAt = entry.ChangedAt,
        }).ToList(),
    };

    private async Task NotifyDetectedAsync(
        MaterialAlert alert,
        string projectCode,
        int? projectManagerUserId,
        CancellationToken ct)
    {
        try
        {
            var recipients = new[] { alert.AssignedToUserId, projectManagerUserId ?? 0 }
                .Where(id => id > 0).Distinct().ToList();
            await notifications.NotifyManyFromTemplateAsync(
                recipients,
                DetectedTemplate,
                new Dictionary<string, string>
                {
                    ["alertCode"] = alert.Code,
                    ["alertType"] = alert.Type.ToString(),
                    ["itemCode"] = alert.ItemCode,
                    ["projectCode"] = projectCode,
                    ["variance"] = alert.VarianceQuantity.ToString("0.######"),
                },
                EntityTypes.MaterialAlert,
                alert.Id,
                $"/admin/procurement-control/projects/{alert.OperationalProjectId}/material-alerts/{alert.Id}");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Material alert {MaterialAlertId} persisted but notification failed.", alert.Id);
        }
    }

    private void WriteAudit(MaterialAlert alert, MaterialAlertEvent entry) => audit.Log(new AuditEvent
    {
        Action = $"proc.material-alert.{entry.Type.ToString().ToLowerInvariant()}",
        ResourceType = EntityTypes.MaterialAlert,
        ResourceId = alert.Id.ToString(),
        Message = $"Material alert {alert.Code} {entry.Type} for project #{alert.OperationalProjectId}.",
        NewValue = new { alert.Code, alert.Type, alert.Status, alert.ItemCode, alert.VarianceQuantity },
        Metadata = new { alert.OperationalProjectId, alert.SourceEntityType, alert.SourceEntityId },
    });

    private sealed record AlertCandidate(
        int ProjectBoqLineId,
        string ItemCode,
        string Description,
        string Unit,
        MaterialAlertType Type,
        MaterialAlertSeverity Severity,
        decimal BoqAllowance,
        decimal RequiredQuantity,
        decimal ReceivedQuantity,
        decimal IssuedQuantity,
        decimal OnHandQuantity,
        decimal VarianceQuantity,
        string SourceEntityType,
        int SourceEntityId,
        int AssignedToUserId);
}
