using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.Services;

public sealed record ProjectReportExportFile(
    byte[] Content,
    string ContentType,
    string FileName,
    int ProjectCount);

public interface IProjectReportService
{
    Task<ProjectReportResponse?> GetAsync(
        ProjectReportQuery query,
        int callerUserId,
        CancellationToken ct = default);

    Task<ProjectReportExportFile?> ExportAsync(
        ProjectReportExportQuery query,
        int callerUserId,
        CancellationToken ct = default);
}

public static class ProjectReportCalculations
{
    public static (bool BaselineReady, decimal? Progress, List<ProjectReportDesignSourceResponse> Sources)
        CalculateDesignRollup(
            IReadOnlyCollection<DesignSchedulePhase> phases,
            IReadOnlyCollection<DesignScheduleTask> tasks)
    {
        var sources = new List<ProjectReportDesignSourceResponse>();
        foreach (var phase in phases.OrderBy(item => item.Code))
        {
            var phaseTasks = tasks.Where(item => item.PhaseId == phase.Id).ToList();
            var phaseRollup = DesignScheduleRules.CalculateRollup(
                phaseTasks.Select(item => (item.Weight, item.ProgressPercent)));
            if (!phaseRollup.BaselineReady || !phaseRollup.Progress.HasValue)
            {
                return (false, null, []);
            }

            sources.Add(new ProjectReportDesignSourceResponse
            {
                PhaseId = phase.Id,
                Weight = phase.Weight,
                ProgressPercent = phaseRollup.Progress.Value,
                WeightedValue = phase.Weight * phaseRollup.Progress.Value / 100m,
            });
        }

        var ready = phases.Count == 3 && phases.Sum(item => item.Weight) == 100 && sources.Count == 3;
        return ready
            ? (true, Math.Round(sources.Sum(item => item.WeightedValue), 2), sources)
            : (false, null, []);
    }

    public static string SafeSpreadsheetText(string? value)
    {
        var text = value ?? string.Empty;
        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@'
            ? $"'{text}"
            : text;
    }
}

public sealed class ProjectReportService(
    AppDbContext db,
    IProjectAccessService projectAccess) : IProjectReportService
{
    private const int PermitWarningWindowDays = 30;

    public async Task<ProjectReportResponse?> GetAsync(
        ProjectReportQuery query,
        int callerUserId,
        CancellationToken ct = default)
    {
        var accessibleIds = await projectAccess.GetAccessibleOperationalProjectIdsAsync(callerUserId, ct);
        if (query.ProjectId.HasValue && !accessibleIds.Contains(query.ProjectId.Value)) return null;

        var scopedIds = query.ProjectId.HasValue
            ? new HashSet<int> { query.ProjectId.Value }
            : accessibleIds.ToHashSet();
        var generatedAtUtc = DateTime.UtcNow;
        var asOfDate = DateOnly.FromDateTime(generatedAtUtc);

        var projects = await db.OperationalProjects.AsNoTracking()
            .Where(project => scopedIds.Contains(project.Id))
            .Include(project => project.Customer)
            .Include(project => project.ProjectManager)
            .OrderBy(project => project.Code)
            .ToListAsync(ct);
        if (query.ProjectId.HasValue && projects.Count == 0) return null;

        var projectIds = projects.Select(project => project.Id).ToList();
        var designProjects = await db.DesignProjects.AsNoTracking()
            .Where(project => project.OperationalProjectId.HasValue &&
                projectIds.Contains(project.OperationalProjectId.Value))
            .ToListAsync(ct);
        var designProjectIds = designProjects.Select(project => project.Id).ToList();
        var designPhases = await db.DesignSchedulePhases.AsNoTracking()
            .Where(phase => projectIds.Contains(phase.OperationalProjectId))
            .ToListAsync(ct);
        var designTasks = await db.DesignScheduleTasks.AsNoTracking()
            .Where(task => projectIds.Contains(task.OperationalProjectId))
            .ToListAsync(ct);
        var constructionTasks = await db.ConstructionTasks.AsNoTracking()
            .Where(task => designProjectIds.Contains(task.DesignProjectId))
            .ToListAsync(ct);
        var acceptanceRecords = await db.AcceptanceRecords.AsNoTracking()
            .Where(record => designProjectIds.Contains(record.DesignProjectId))
            .ToListAsync(ct);
        var permits = await db.PermitChecklistItems.AsNoTracking()
            .Where(permit => designProjectIds.Contains(permit.DesignProjectId))
            .ToListAsync(ct);
        var quotes = await db.Quotes.AsNoTracking()
            .Where(quote => quote.OperationalProjectId.HasValue &&
                projectIds.Contains(quote.OperationalProjectId.Value))
            .ToListAsync(ct);
        var contracts = await db.Contracts.AsNoTracking()
            .Where(contract => contract.OperationalProjectId.HasValue &&
                projectIds.Contains(contract.OperationalProjectId.Value))
            .ToListAsync(ct);
        var contractIds = contracts.Select(contract => contract.Id).ToList();
        var appendices = await db.ContractAppendices.AsNoTracking()
            .Where(appendix => contractIds.Contains(appendix.ContractId))
            .ToListAsync(ct);
        var milestones = await db.ContractPaymentMilestones.AsNoTracking()
            .Where(milestone => contractIds.Contains(milestone.ContractId))
            .ToListAsync(ct);

        var response = new ProjectReportResponse
        {
            GeneratedAtUtc = generatedAtUtc,
            AsOfUtc = generatedAtUtc,
            ProjectId = query.ProjectId,
            From = query.From,
            To = query.To,
        };

        foreach (var project in projects)
        {
            var designProject = designProjects.SingleOrDefault(item => item.OperationalProjectId == project.Id);
            var designProjectId = designProject?.Id;
            response.Projects.Add(new ProjectOperationalReportResponse
            {
                Project = MapProject(project),
                Design = BuildDesign(
                    designPhases.Where(item => item.OperationalProjectId == project.Id).ToList(),
                    designTasks.Where(item => item.OperationalProjectId == project.Id).ToList()),
                Construction = BuildConstruction(
                    designProjectId,
                    constructionTasks.Where(item => item.DesignProjectId == designProjectId)
                        .Where(item => InRange(item.PlannedEnd, query)).ToList(),
                    asOfDate),
                Acceptance = BuildAcceptance(
                    designProjectId,
                    acceptanceRecords.Where(item => item.DesignProjectId == designProjectId)
                        .Where(item => InRange(item.AcceptanceDate, query)).ToList(),
                    asOfDate),
                Permits = BuildPermits(
                    designProjectId,
                    permits.Where(item => item.DesignProjectId == designProjectId)
                        .Where(item => PermitInRange(item, query)).ToList(),
                    asOfDate,
                    query),
                ContractualFinance = BuildContractualFinance(
                    project.Id,
                    query,
                    quotes,
                    contracts,
                    appendices,
                    milestones),
                UnavailableMetrics = BuildUnavailableMetrics(),
            });
        }

        return response;
    }

    public async Task<ProjectReportExportFile?> ExportAsync(
        ProjectReportExportQuery query,
        int callerUserId,
        CancellationToken ct = default)
    {
        var report = await GetAsync(query.ToReportQuery(), callerUserId, ct);
        if (report is null) return null;
        return string.Equals(query.Format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? BuildPdf(report, query.Language)
            : BuildWorkbook(report, query.Language);
    }

    private static ProjectReportIdentityResponse MapProject(OperationalProject project) => new()
    {
        OperationalProjectId = project.Id,
        Code = project.Code,
        Name = project.Name,
        CustomerId = project.CustomerId,
        CustomerName = project.Customer.Name,
        ProjectManagerUserId = project.ProjectManagerUserId,
        ProjectManagerName = project.ProjectManager?.FullName,
        Status = project.Status.ToString(),
        StartDate = project.StartDate.HasValue ? DateOnly.FromDateTime(project.StartDate.Value) : null,
        EndDate = project.EndDate.HasValue ? DateOnly.FromDateTime(project.EndDate.Value) : null,
        DrillDown = new ProjectReportDrillDownResponse
        {
            Route = $"/admin/operational-projects/{project.Id}",
        },
    };

    private static ProjectReportDesignResponse BuildDesign(
        List<DesignSchedulePhase> phases,
        List<DesignScheduleTask> tasks)
    {
        if (phases.Count == 0)
        {
            return new ProjectReportDesignResponse
            {
                ReasonCode = ProjectReportUnavailableReasons.SourceNotConfigured,
            };
        }

        var rollup = ProjectReportCalculations.CalculateDesignRollup(phases, tasks);
        return new ProjectReportDesignResponse
        {
            Availability = rollup.BaselineReady
                ? ProjectReportAvailability.Available
                : ProjectReportAvailability.Unavailable,
            ReasonCode = rollup.BaselineReady
                ? null
                : ProjectReportUnavailableReasons.BaselineIncomplete,
            WeightedProgressPercent = rollup.Progress,
            Sources = rollup.Sources,
        };
    }

    private static ProjectReportConstructionResponse BuildConstruction(
        int? designProjectId,
        List<ConstructionTask> tasks,
        DateOnly asOfDate)
    {
        if (!designProjectId.HasValue)
        {
            return new ProjectReportConstructionResponse
            {
                Availability = ProjectReportAvailability.Unavailable,
                ReasonCode = ProjectReportUnavailableReasons.SourceNotConfigured,
            };
        }

        var overdue = tasks.Where(task =>
            task.PlannedEnd < asOfDate &&
            task.Status is not ConstructionTaskStatus.Completed and not ConstructionTaskStatus.Cancelled)
            .OrderBy(task => task.PlannedEnd)
            .ThenBy(task => task.Id)
            .ToList();
        return new ProjectReportConstructionResponse
        {
            TotalCount = tasks.Count,
            CountsByStatus = Enum.GetValues<ConstructionTaskStatus>()
                .ToDictionary(status => status.ToString(), status => tasks.Count(task => task.Status == status)),
            OverdueCount = overdue.Count,
            OverdueItems = overdue.Select(task => new ProjectReportConstructionTaskResponse
            {
                Id = task.Id,
                DesignProjectId = task.DesignProjectId,
                TaskCode = task.TaskCode,
                Name = task.Name,
                Status = task.Status.ToString(),
                PlannedEnd = task.PlannedEnd,
                DrillDown = new ProjectReportDrillDownResponse
                {
                    Route = "/admin/construction/tasks",
                    Query = $"designProjectId={task.DesignProjectId}&taskId={task.Id}",
                },
            }).ToList(),
        };
    }

    private static ProjectReportAcceptanceResponse BuildAcceptance(
        int? designProjectId,
        List<AcceptanceRecord> records,
        DateOnly asOfDate)
    {
        if (!designProjectId.HasValue)
        {
            return new ProjectReportAcceptanceResponse
            {
                Availability = ProjectReportAvailability.Unavailable,
                ReasonCode = ProjectReportUnavailableReasons.SourceNotConfigured,
            };
        }

        return new ProjectReportAcceptanceResponse
        {
            TotalCount = records.Count,
            CountsByStatus = Enum.GetValues<AcceptanceStatus>()
                .ToDictionary(status => status.ToString(), status => records.Count(record => record.Status == status)),
            CountsByRevision = records.GroupBy(record => record.RevisionCount)
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.Count()),
            OverdueCount = records.Count(record => record.AcceptanceDate < asOfDate &&
                record.Status is AcceptanceStatus.Draft or AcceptanceStatus.Submitted),
        };
    }

    private static ProjectReportPermitResponse BuildPermits(
        int? designProjectId,
        List<PermitChecklistItem> permits,
        DateOnly asOfDate,
        ProjectReportQuery query)
    {
        if (!designProjectId.HasValue)
        {
            return new ProjectReportPermitResponse
            {
                Availability = ProjectReportAvailability.Unavailable,
                ReasonCode = ProjectReportUnavailableReasons.SourceNotConfigured,
            };
        }

        var warningEnd = asOfDate.AddDays(PermitWarningWindowDays);
        var items = permits.Select(permit =>
        {
            var target = ToDateOnly(permit.TargetDeadline);
            var expires = ToDateOnly(permit.ExpiresAt);
            var activeTarget = permit.Status is not PermitStatus.Issued and
                not PermitStatus.Rejected and not PermitStatus.Expired;
            return new ProjectReportPermitItemResponse
            {
                Id = permit.Id,
                DesignProjectId = permit.DesignProjectId,
                PermitTypeCode = permit.PermitTypeCode,
                Status = permit.Status.ToString(),
                TargetDeadline = target,
                ExpiresAt = expires,
                IsOverdue = activeTarget && target.HasValue && InRange(target.Value, query) &&
                    target.Value < asOfDate,
                IsDueSoon = activeTarget && target.HasValue &&
                    InRange(target.Value, query) &&
                    target.Value >= asOfDate && target.Value <= warningEnd,
                IsExpiring = permit.Status == PermitStatus.Issued && expires.HasValue &&
                    InRange(expires.Value, query) &&
                    expires.Value >= asOfDate && expires.Value <= warningEnd,
                DrillDown = new ProjectReportDrillDownResponse
                {
                    Route = "/admin/permits",
                    Query = $"designProjectId={permit.DesignProjectId}&permitId={permit.Id}",
                },
            };
        }).Where(item => item.IsOverdue || item.IsDueSoon || item.IsExpiring)
            .OrderBy(item => item.TargetDeadline ?? item.ExpiresAt)
            .ThenBy(item => item.Id)
            .ToList();

        return new ProjectReportPermitResponse
        {
            OverdueCount = items.Count(item => item.IsOverdue),
            DueSoonCount = items.Count(item => item.IsDueSoon),
            ExpiringCount = items.Count(item => item.IsExpiring),
            Items = items,
        };
    }

    private static ProjectReportContractualFinanceResponse BuildContractualFinance(
        int projectId,
        ProjectReportQuery query,
        IReadOnlyCollection<Quote> allQuotes,
        IReadOnlyCollection<Contract> allContracts,
        IReadOnlyCollection<ContractAppendix> allAppendices,
        IReadOnlyCollection<ContractPaymentMilestone> allMilestones)
    {
        var quotes = allQuotes.Where(item => item.OperationalProjectId == projectId)
            .Where(item => InRange(DateOnly.FromDateTime(item.CreatedAt), query)).ToList();
        var projectContracts = allContracts.Where(item => item.OperationalProjectId == projectId).ToList();
        var contracts = projectContracts
            .Where(item => InRange(DateOnly.FromDateTime(item.SignedDate ?? item.CreatedAt), query)).ToList();
        var projectContractIds = projectContracts.Select(item => item.Id).ToHashSet();
        var appendices = allAppendices.Where(item => projectContractIds.Contains(item.ContractId) &&
                item.Status == ContractAppendixStatus.Approved)
            .Where(item => InRange(DateOnly.FromDateTime(item.DecidedAt ?? item.UpdatedAt), query)).ToList();
        var milestones = allMilestones.Where(item =>
                projectContractIds.Contains(item.ContractId) && item.DueDate.HasValue)
            .Where(item => InRange(DateOnly.FromDateTime(item.DueDate!.Value), query)).ToList();
        var contractValues = projectContracts.ToDictionary(item => item.Id, item => item.Value);
        var approvedDelta = appendices.Sum(item => item.ValueDelta);

        return new ProjectReportContractualFinanceResponse
        {
            QuoteGrandTotal = quotes.Sum(item => item.GrandTotal),
            QuoteTotalsByStatus = Enum.GetValues<QuoteStatus>()
                .ToDictionary(status => status.ToString(),
                    status => quotes.Where(item => item.Status == status).Sum(item => item.GrandTotal)),
            ContractBaseValue = contracts.Sum(item => item.Value),
            ApprovedVariationOrderDelta = approvedDelta,
            ContractCurrentValue = contracts.Sum(item => item.Value) + approvedDelta,
            MilestoneScheduledValuesByStatus = Enum.GetValues<PaymentMilestoneStatus>()
                .ToDictionary(status => status.ToString(), status => milestones
                    .Where(item => item.Status == status)
                    .Sum(item => Math.Round(contractValues[item.ContractId] * item.PercentValue / 100m, 2))),
        };
    }

    private static List<ProjectReportUnavailableMetricResponse> BuildUnavailableMetrics() =>
    [
        Unavailable("historicalSCurve", ProjectReportUnavailableReasons.HistoricalSnapshotsUnavailable),
        Unavailable("weightedConstructionProgress", ProjectReportUnavailableReasons.ConstructionWeightsUnavailable),
        Unavailable("acceptanceRatios", ProjectReportUnavailableReasons.AcceptanceOutcomeDataUnavailable),
        Unavailable("acceptanceFirstPass", ProjectReportUnavailableReasons.AcceptanceOutcomeDataUnavailable),
        Unavailable("actualCashflow", ProjectReportUnavailableReasons.ActualFinanceLedgerUnavailable),
        Unavailable("actualRevenue", ProjectReportUnavailableReasons.ActualFinanceLedgerUnavailable),
        Unavailable("actualExpenditure", ProjectReportUnavailableReasons.ActualFinanceLedgerUnavailable),
        Unavailable("profitAndLoss", ProjectReportUnavailableReasons.ActualFinanceLedgerUnavailable),
        Unavailable("receivables", ProjectReportUnavailableReasons.ActualFinanceLedgerUnavailable),
        Unavailable("inventory", ProjectReportUnavailableReasons.InventoryLedgerUnavailable),
        Unavailable("boqUsage", ProjectReportUnavailableReasons.BoqUsageDataUnavailable),
        Unavailable("vendorPerformance", ProjectReportUnavailableReasons.VendorPerformanceDataUnavailable),
    ];

    private static ProjectReportUnavailableMetricResponse Unavailable(string code, string reasonCode) => new()
    {
        MetricCode = code,
        ReasonCode = reasonCode,
    };

    private static bool InRange(DateOnly date, ProjectReportQuery query) =>
        (!query.From.HasValue || date >= query.From.Value) &&
        (!query.To.HasValue || date <= query.To.Value);

    private static bool PermitInRange(PermitChecklistItem permit, ProjectReportQuery query)
    {
        if (!query.From.HasValue && !query.To.HasValue) return true;
        var target = ToDateOnly(permit.TargetDeadline);
        var expiry = ToDateOnly(permit.ExpiresAt);
        return target.HasValue && InRange(target.Value, query) ||
            expiry.HasValue && InRange(expiry.Value, query);
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static ProjectReportExportFile BuildWorkbook(ProjectReportResponse report, string language)
    {
        var labels = ExportLabels.For(language);
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add(labels.ProjectsSheet);
        var headers = new[]
        {
            labels.ProjectId, labels.Code, labels.Name, labels.Status, labels.DesignProgress,
            labels.ConstructionTasks, labels.ConstructionOverdue, labels.AcceptanceRecords,
            labels.AcceptanceOverdue, labels.PermitOverdue, labels.PermitDueSoon,
            labels.PermitExpiring, labels.QuoteTotal, labels.ContractBaseValue,
            labels.ApprovedVoDelta, labels.ContractCurrentValue,
        };
        for (var column = 0; column < headers.Length; column++) summary.Cell(1, column + 1).Value = headers[column];

        var row = 2;
        foreach (var project in report.Projects)
        {
            var values = new object?[]
            {
                project.Project.OperationalProjectId,
                ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                ProjectReportCalculations.SafeSpreadsheetText(project.Project.Name),
                project.Project.Status,
                project.Design.WeightedProgressPercent,
                project.Construction.Availability == ProjectReportAvailability.Available ? project.Construction.TotalCount : null,
                project.Construction.Availability == ProjectReportAvailability.Available ? project.Construction.OverdueCount : null,
                project.Acceptance.Availability == ProjectReportAvailability.Available ? project.Acceptance.TotalCount : null,
                project.Acceptance.Availability == ProjectReportAvailability.Available ? project.Acceptance.OverdueCount : null,
                project.Permits.Availability == ProjectReportAvailability.Available ? project.Permits.OverdueCount : null,
                project.Permits.Availability == ProjectReportAvailability.Available ? project.Permits.DueSoonCount : null,
                project.Permits.Availability == ProjectReportAvailability.Available ? project.Permits.ExpiringCount : null,
                project.ContractualFinance.QuoteGrandTotal,
                project.ContractualFinance.ContractBaseValue,
                project.ContractualFinance.ApprovedVariationOrderDelta,
                project.ContractualFinance.ContractCurrentValue,
            };
            for (var column = 0; column < values.Length; column++)
            {
                var cell = summary.Cell(row, column + 1);
                if (values[column] is null) cell.Value = labels.Unavailable;
                else if (values[column] is int intValue) cell.Value = intValue;
                else if (values[column] is decimal decimalValue) cell.Value = decimalValue;
                else cell.Value = values[column]?.ToString() ?? string.Empty;
            }
            row++;
        }
        summary.Cell(row + 1, 1).Value = $"{labels.GeneratedAtUtc}: {report.GeneratedAtUtc:O}";
        summary.Columns().AdjustToContents();
        summary.SheetView.FreezeRows(1);

        var notices = workbook.Worksheets.Add(labels.UnavailableSheet);
        notices.Cell(1, 1).Value = labels.ProjectId;
        notices.Cell(1, 2).Value = labels.Metric;
        notices.Cell(1, 3).Value = labels.ReasonCode;
        var noticeRow = 2;
        foreach (var project in report.Projects)
        {
            foreach (var metric in project.UnavailableMetrics)
            {
                notices.Cell(noticeRow, 1).Value = project.Project.OperationalProjectId;
                notices.Cell(noticeRow, 2).Value = metric.MetricCode;
                notices.Cell(noticeRow, 3).Value = metric.ReasonCode;
                noticeRow++;
            }
        }
        notices.Columns().AdjustToContents();

        AddConstructionSheet(workbook, report, labels);
        AddAcceptanceSheet(workbook, report, labels);
        AddPermitSheet(workbook, report, labels);
        AddFinanceSheet(workbook, report, labels);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ProjectReportExportFile(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"project-reports-{report.GeneratedAtUtc:yyyy-MM-dd-HHmmss}.xlsx",
            report.Projects.Count);
    }

    private static ProjectReportExportFile BuildPdf(ProjectReportResponse report, string language)
    {
        var labels = ExportLabels.For(language);
        var lines = new List<string>
        {
            labels.Title,
            $"{labels.GeneratedAtUtc}: {report.GeneratedAtUtc:O}",
            $"{labels.Filter}: {report.From?.ToString("yyyy-MM-dd") ?? labels.Open} - {report.To?.ToString("yyyy-MM-dd") ?? labels.Open}",
            string.Empty,
        };
        foreach (var project in report.Projects)
        {
            lines.Add($"{project.Project.Code} - {project.Project.Name}");
            lines.Add($"{labels.DesignProgress}: {project.Design.WeightedProgressPercent?.ToString("0.##") ?? $"{labels.Unavailable} ({project.Design.ReasonCode})"}");
            lines.Add($"{labels.ConstructionTasks}: {AvailableValue(project.Construction.Availability, project.Construction.TotalCount, project.Construction.ReasonCode, labels.Unavailable)}; {labels.Overdue.ToLowerInvariant()} {project.Construction.OverdueCount}");
            foreach (var status in project.Construction.CountsByStatus)
                lines.Add($"  {labels.Status} {status.Key}: {status.Value}");
            foreach (var item in project.Construction.OverdueItems)
                lines.Add($"  {labels.Overdue}: {item.TaskCode} - {item.Name}; {labels.PlannedEnd} {item.PlannedEnd:yyyy-MM-dd}");
            lines.Add($"{labels.AcceptanceRecords}: {AvailableValue(project.Acceptance.Availability, project.Acceptance.TotalCount, project.Acceptance.ReasonCode, labels.Unavailable)}; {labels.Overdue.ToLowerInvariant()} {project.Acceptance.OverdueCount}");
            foreach (var status in project.Acceptance.CountsByStatus)
                lines.Add($"  {labels.Status} {status.Key}: {status.Value}");
            foreach (var revision in project.Acceptance.CountsByRevision)
                lines.Add($"  {labels.Revision} {revision.Key}: {revision.Value}");
            lines.Add($"{labels.Permits}: {labels.Overdue.ToLowerInvariant()} {project.Permits.OverdueCount}; {labels.DueSoon.ToLowerInvariant()} {project.Permits.DueSoonCount}; {labels.Expiring.ToLowerInvariant()} {project.Permits.ExpiringCount}");
            foreach (var item in project.Permits.Items)
                lines.Add($"  {item.PermitTypeCode}; {labels.Status} {item.Status}; {labels.TargetDeadline} {item.TargetDeadline?.ToString("yyyy-MM-dd") ?? "-"}; {labels.ExpiresAt} {item.ExpiresAt?.ToString("yyyy-MM-dd") ?? "-"}; {AlertFlags(item, labels)}");
            lines.Add($"{labels.ContractualFinance}: {labels.Quotes.ToLowerInvariant()} {project.ContractualFinance.QuoteGrandTotal:0.##}; {labels.ContractBase.ToLowerInvariant()} {project.ContractualFinance.ContractBaseValue:0.##}; {labels.ApprovedVo.ToLowerInvariant()} {project.ContractualFinance.ApprovedVariationOrderDelta:0.##}; {labels.Current.ToLowerInvariant()} {project.ContractualFinance.ContractCurrentValue:0.##}");
            foreach (var status in project.ContractualFinance.QuoteTotalsByStatus)
                lines.Add($"  {labels.Quotes} - {status.Key}: {status.Value:0.##}");
            foreach (var status in project.ContractualFinance.MilestoneScheduledValuesByStatus)
                lines.Add($"  {labels.MilestoneSchedule} - {status.Key}: {status.Value:0.##}");
            lines.Add(labels.ContractualScheduleNotice);
            foreach (var unavailable in project.UnavailableMetrics)
                lines.Add($"{labels.Unavailable}: {unavailable.MetricCode} ({unavailable.ReasonCode})");
            lines.Add(string.Empty);
        }

        return new ProjectReportExportFile(
            SimplePdfWriter.Create(lines, language),
            "application/pdf",
            $"project-reports-{report.GeneratedAtUtc:yyyy-MM-dd-HHmmss}.pdf",
            report.Projects.Count);
    }

    private static string AvailableValue(
        string availability,
        int value,
        string? reasonCode,
        string unavailableLabel) => availability == ProjectReportAvailability.Available
            ? value.ToString()
            : $"{unavailableLabel} ({reasonCode})";

    private static void AddConstructionSheet(
        XLWorkbook workbook,
        ProjectReportResponse report,
        ExportLabels labels)
    {
        var sheet = workbook.Worksheets.Add(labels.ConstructionSheet);
        WriteHeaders(sheet, labels.ProjectId, labels.Code, labels.RecordType, labels.Status,
            labels.Count, labels.TaskCode, labels.TaskName, labels.PlannedEnd);
        var row = 2;
        foreach (var project in report.Projects)
        {
            foreach (var status in project.Construction.CountsByStatus)
            {
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.StatusBreakdown, status.Key, status.Value, null, null, null);
            }
            foreach (var item in project.Construction.OverdueItems)
            {
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.Overdue, item.Status, null,
                    ProjectReportCalculations.SafeSpreadsheetText(item.TaskCode),
                    ProjectReportCalculations.SafeSpreadsheetText(item.Name), item.PlannedEnd);
            }
        }
        sheet.Columns().AdjustToContents();
    }

    private static void AddAcceptanceSheet(
        XLWorkbook workbook,
        ProjectReportResponse report,
        ExportLabels labels)
    {
        var sheet = workbook.Worksheets.Add(labels.AcceptanceSheet);
        WriteHeaders(sheet, labels.ProjectId, labels.Code, labels.RecordType, labels.Status,
            labels.Revision, labels.Count);
        var row = 2;
        foreach (var project in report.Projects)
        {
            foreach (var status in project.Acceptance.CountsByStatus)
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.StatusBreakdown, status.Key, null, status.Value);
            foreach (var revision in project.Acceptance.CountsByRevision)
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.RevisionBreakdown, null, revision.Key, revision.Value);
        }
        sheet.Columns().AdjustToContents();
    }

    private static void AddPermitSheet(
        XLWorkbook workbook,
        ProjectReportResponse report,
        ExportLabels labels)
    {
        var sheet = workbook.Worksheets.Add(labels.PermitSheet);
        WriteHeaders(sheet, labels.ProjectId, labels.Code, labels.PermitType, labels.Status,
            labels.TargetDeadline, labels.ExpiresAt, labels.AlertFlags);
        var row = 2;
        foreach (var project in report.Projects)
        {
            foreach (var item in project.Permits.Items)
            {
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    ProjectReportCalculations.SafeSpreadsheetText(item.PermitTypeCode), item.Status,
                    item.TargetDeadline, item.ExpiresAt, AlertFlags(item, labels));
            }
        }
        sheet.Columns().AdjustToContents();
    }

    private static void AddFinanceSheet(
        XLWorkbook workbook,
        ProjectReportResponse report,
        ExportLabels labels)
    {
        var sheet = workbook.Worksheets.Add(labels.FinanceSheet);
        WriteHeaders(sheet, labels.ProjectId, labels.Code, labels.RecordType, labels.Status,
            labels.ScheduledValue);
        var row = 2;
        foreach (var project in report.Projects)
        {
            foreach (var status in project.ContractualFinance.QuoteTotalsByStatus)
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.Quotes, status.Key, status.Value);
            foreach (var status in project.ContractualFinance.MilestoneScheduledValuesByStatus)
                WriteRow(sheet, row++, project.Project.OperationalProjectId,
                    ProjectReportCalculations.SafeSpreadsheetText(project.Project.Code),
                    labels.MilestoneSchedule, status.Key, status.Value);
        }
        sheet.Columns().AdjustToContents();
    }

    private static void WriteHeaders(IXLWorksheet sheet, params string[] values)
    {
        for (var column = 0; column < values.Length; column++)
            sheet.Cell(1, column + 1).Value = values[column];
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            var cell = sheet.Cell(row, column + 1);
            switch (values[column])
            {
                case null: cell.Value = string.Empty; break;
                case int intValue: cell.Value = intValue; break;
                case decimal decimalValue: cell.Value = decimalValue; break;
                case DateOnly dateValue: cell.Value = dateValue.ToString("yyyy-MM-dd"); break;
                default: cell.Value = values[column]?.ToString() ?? string.Empty; break;
            }
        }
    }

    private static string AlertFlags(ProjectReportPermitItemResponse item, ExportLabels labels) =>
        string.Join(", ", new[]
        {
            item.IsOverdue ? labels.Overdue : null,
            item.IsDueSoon ? labels.DueSoon : null,
            item.IsExpiring ? labels.Expiring : null,
        }.Where(value => value is not null));

    private sealed record ExportLabels(
        string Title,
        string ProjectsSheet,
        string UnavailableSheet,
        string ProjectId,
        string Code,
        string Name,
        string Status,
        string DesignProgress,
        string ConstructionTasks,
        string ConstructionOverdue,
        string AcceptanceRecords,
        string AcceptanceOverdue,
        string PermitOverdue,
        string PermitDueSoon,
        string PermitExpiring,
        string QuoteTotal,
        string ContractBaseValue,
        string ApprovedVoDelta,
        string ContractCurrentValue,
        string GeneratedAtUtc,
        string Filter,
        string Open,
        string Unavailable,
        string Metric,
        string ReasonCode,
        string Overdue,
        string Permits,
        string DueSoon,
        string Expiring,
        string ContractualFinance,
        string Quotes,
        string ContractBase,
        string ApprovedVo,
        string Current,
        string ContractualScheduleNotice,
        string ConstructionSheet,
        string AcceptanceSheet,
        string PermitSheet,
        string FinanceSheet,
        string RecordType,
        string Count,
        string TaskCode,
        string TaskName,
        string PlannedEnd,
        string Revision,
        string PermitType,
        string TargetDeadline,
        string ExpiresAt,
        string AlertFlags,
        string ScheduledValue,
        string StatusBreakdown,
        string RevisionBreakdown,
        string MilestoneSchedule)
    {
        public static ExportLabels For(string language) => language switch
        {
            "en" => English,
            "zh" => Chinese,
            "ja" => Japanese,
            _ => Vietnamese,
        };

        private static ExportLabels English => new(
            "NICON PROJECT OPERATIONAL REPORT", "Projects", "Unavailable", "Project ID", "Code",
            "Name", "Status", "Design progress", "Construction tasks", "Construction overdue",
            "Acceptance records", "Acceptance overdue", "Permit overdue", "Permit due soon",
            "Permit expiring", "Quote total", "Contract base value", "Approved VO delta",
            "Contract current value", "Generated at UTC", "Filter", "open", "Unavailable",
            "Metric", "Reason code", "Overdue", "Permits", "Due soon", "Expiring",
            "Contractual finance", "Quotes", "Contract base", "Approved VO", "Current",
            "Milestones are contractual schedule values, not cash or revenue.",
            "Construction", "Acceptance", "Permits", "Finance", "Record type", "Count",
            "Task code", "Task name", "Planned end", "Revision", "Permit type",
            "Target deadline", "Expires at", "Alert flags", "Scheduled value",
            "Status breakdown", "Revision breakdown", "Milestone schedule");

        private static ExportLabels Vietnamese => new(
            "BÁO CÁO VẬN HÀNH DỰ ÁN NICON", "Dự án", "Chưa khả dụng", "ID dự án", "Mã",
            "Tên", "Trạng thái", "Tiến độ thiết kế", "Công việc thi công", "Thi công quá hạn",
            "Hồ sơ nghiệm thu", "Nghiệm thu quá hạn", "Giấy phép quá hạn", "Giấy phép sắp đến hạn",
            "Giấy phép sắp hết hạn", "Tổng báo giá", "Giá trị hợp đồng gốc", "Chênh lệch VO đã duyệt",
            "Giá trị hợp đồng hiện tại", "Thời điểm tạo UTC", "Bộ lọc", "mở", "Chưa khả dụng",
            "Chỉ số", "Mã lý do", "Quá hạn", "Giấy phép", "Sắp đến hạn", "Sắp hết hạn",
            "Tài chính hợp đồng", "Báo giá", "Hợp đồng gốc", "VO đã duyệt", "Hiện tại",
            "Mốc thanh toán là giá trị lịch hợp đồng, không phải tiền thực thu hoặc doanh thu.",
            "Thi công", "Nghiệm thu", "Giấy phép", "Tài chính", "Loại dữ liệu", "Số lượng",
            "Mã công việc", "Tên công việc", "Ngày kết thúc dự kiến", "Lần sửa", "Loại giấy phép",
            "Hạn mục tiêu", "Ngày hết hạn", "Cảnh báo", "Giá trị theo lịch",
            "Theo trạng thái", "Theo lần sửa", "Lịch mốc hợp đồng");

        private static ExportLabels Chinese => new(
            "NICON 项目运营报告", "项目", "不可用", "项目 ID", "代码", "名称", "状态", "设计进度",
            "施工任务", "施工逾期", "验收记录", "验收逾期", "许可证逾期", "许可证即将到期",
            "许可证即将失效", "报价总额", "合同基础金额", "已批准 VO 变更", "当前合同金额",
            "生成时间 UTC", "筛选", "不限", "不可用", "指标", "原因代码", "逾期", "许可证",
            "即将到期", "即将失效", "合同财务", "报价", "合同基础", "已批准 VO", "当前",
            "付款节点仅表示合同计划金额，不代表现金或收入。",
            "施工", "验收", "许可证", "财务", "记录类型", "数量", "任务代码", "任务名称",
            "计划结束日期", "修订", "许可证类型", "目标期限", "失效日期", "预警标记",
            "计划金额", "状态明细", "修订明细", "合同里程碑计划");

        private static ExportLabels Japanese => new(
            "NICON プロジェクト運用レポート", "プロジェクト", "利用不可", "プロジェクト ID", "コード",
            "名称", "ステータス", "設計進捗", "施工タスク", "施工期限超過", "検収記録", "検収期限超過",
            "許可期限超過", "許可期限間近", "許可失効間近", "見積合計", "基本契約額", "承認済み VO 差額",
            "現在契約額", "生成日時 UTC", "フィルター", "指定なし", "利用不可", "指標", "理由コード",
            "期限超過", "許可", "期限間近", "失効間近", "契約財務", "見積", "基本契約", "承認済み VO",
            "現在", "支払マイルストーンは契約上の予定額であり、現金または売上ではありません。",
            "施工", "検収", "許可", "財務", "レコード種別", "件数", "タスクコード", "タスク名",
            "予定終了日", "改訂", "許可種別", "目標期限", "有効期限", "警告フラグ",
            "予定額", "ステータス内訳", "改訂内訳", "契約マイルストーン予定");
    }
}
