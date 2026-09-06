using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ProjectReportsControllerTests : IntegrationTestBase
{
    public ProjectReportsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_RequiresAuthenticationAndDedicatedPermission()
    {
        (await Client.GetAsync("/api/reports/projects")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthenticateAsync("WAREHOUSE");
        (await Client.GetAsync("/api/reports/projects")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Portfolio_UsesProjectScopeAndExplicitInaccessibleProjectIsNotFound()
    {
        var fixture = await SeedFixtureAsync();
        await AuthenticateAsync("PM");

        var portfolio = await Client.GetAsync("/api/reports/projects");
        portfolio.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(portfolio);
        body.GetProperty("projects").EnumerateArray()
            .Select(item => item.GetProperty("project").GetProperty("operationalProjectId").GetInt32())
            .Should().Contain(fixture.AccessibleProjectId).And.NotContain(fixture.InaccessibleProjectId);

        var inaccessible = await Client.GetAsync($"/api/reports/projects?projectId={fixture.InaccessibleProjectId}");
        inaccessible.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("BGD")]
    [InlineData("ACCOUNTANT")]
    public async Task ManagementRoles_CanViewPortfolio(string roleCode)
    {
        var fixture = await SeedFixtureAsync();
        await AuthenticateAsync(roleCode);

        var response = await Client.GetAsync("/api/reports/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await ReadJsonAsync(response)).GetProperty("projects").EnumerateArray()
            .Select(item => item.GetProperty("project").GetProperty("operationalProjectId").GetInt32());
        ids.Should().Contain([fixture.AccessibleProjectId, fixture.InaccessibleProjectId]);
    }

    [Fact]
    public async Task Get_ReturnsExactAggregatesAndExplicitUnavailableStates()
    {
        var fixture = await SeedFixtureAsync();
        await AuthenticateAsync("BGD");

        var response = await Client.GetAsync($"/api/reports/projects?projectId={fixture.AccessibleProjectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = (await ReadJsonAsync(response)).GetProperty("projects")[0];
        project.GetProperty("design").GetProperty("availability").GetString().Should().Be("Unavailable");
        project.GetProperty("design").GetProperty("reasonCode").GetString()
            .Should().Be("DESIGN_BASELINE_INCOMPLETE");
        project.GetProperty("construction").GetProperty("totalCount").GetInt32().Should().Be(2);
        project.GetProperty("construction").GetProperty("overdueCount").GetInt32().Should().Be(1);
        project.GetProperty("construction").GetProperty("countsByStatus").GetProperty("Planned")
            .GetInt32().Should().Be(1);
        project.GetProperty("acceptance").GetProperty("totalCount").GetInt32().Should().Be(2);
        project.GetProperty("acceptance").GetProperty("overdueCount").GetInt32().Should().Be(1);
        project.GetProperty("acceptance").GetProperty("countsByRevision").GetProperty("2")
            .GetInt32().Should().Be(1);
        project.GetProperty("permits").GetProperty("overdueCount").GetInt32().Should().Be(1);
        project.GetProperty("permits").GetProperty("dueSoonCount").GetInt32().Should().Be(1);
        project.GetProperty("permits").GetProperty("expiringCount").GetInt32().Should().Be(1);
        var finance = project.GetProperty("contractualFinance");
        finance.GetProperty("quoteGrandTotal").GetDecimal().Should().Be(2500m);
        finance.GetProperty("contractBaseValue").GetDecimal().Should().Be(1000m);
        finance.GetProperty("approvedVariationOrderDelta").GetDecimal().Should().Be(200m);
        finance.GetProperty("contractCurrentValue").GetDecimal().Should().Be(1200m);
        finance.GetProperty("milestoneScheduledValuesByStatus").GetProperty("Pending")
            .GetDecimal().Should().Be(500m);
        finance.GetProperty("milestoneLabel").GetString().Should().Be("Contractual schedule");
        project.GetProperty("unavailableMetrics").EnumerateArray()
            .Select(item => item.GetProperty("metricCode").GetString())
            .Should().Contain(["historicalSCurve", "weightedConstructionProgress", "acceptanceRatios",
                "actualCashflow", "profitAndLoss", "inventory", "boqUsage", "vendorPerformance"]);
    }

    [Fact]
    public async Task DateRange_IsInclusiveAndReversedRangeIsRejected()
    {
        var fixture = await SeedFixtureAsync();
        await AuthenticateAsync("BGD");
        var date = fixture.Today.ToString("yyyy-MM-dd");

        var inclusive = await Client.GetAsync(
            $"/api/reports/projects?projectId={fixture.AccessibleProjectId}&from={date}&to={date}");
        var reversed = await Client.GetAsync(
            $"/api/reports/projects?from={fixture.Today.AddDays(1):yyyy-MM-dd}&to={date}");

        inclusive.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = (await ReadJsonAsync(inclusive)).GetProperty("projects")[0];
        project.GetProperty("construction").GetProperty("totalCount").GetInt32().Should().Be(1);
        project.GetProperty("acceptance").GetProperty("totalCount").GetInt32().Should().Be(1);
        project.GetProperty("permits").GetProperty("dueSoonCount").GetInt32().Should().Be(1);
        project.GetProperty("permits").GetProperty("expiringCount").GetInt32().Should().Be(0);
        var finance = project.GetProperty("contractualFinance");
        finance.GetProperty("contractBaseValue").GetDecimal().Should().Be(0m);
        finance.GetProperty("approvedVariationOrderDelta").GetDecimal().Should().Be(200m);
        finance.GetProperty("contractCurrentValue").GetDecimal().Should().Be(200m);
        finance.GetProperty("milestoneScheduledValuesByStatus").GetProperty("Pending")
            .GetDecimal().Should().Be(500m);
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_RejectsUnsupportedLanguage()
    {
        await AuthenticateAsync("BGD");

        var response = await Client.GetAsync("/api/reports/projects/export?format=xlsx&language=fr");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_RequiresExportPermissionAndProducesAuditedXlsxAndPdf()
    {
        var fixture = await SeedFixtureAsync();
        await AuthenticateAsync("PM");
        (await Client.GetAsync($"/api/reports/projects/export?format=xlsx&projectId={fixture.AccessibleProjectId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthenticateAsync("BGD");
        var xlsx = await Client.GetAsync(
            $"/api/reports/projects/export?format=xlsx&language=en&projectId={fixture.AccessibleProjectId}");
        var pdf = await Client.GetAsync(
            $"/api/reports/projects/export?format=pdf&language=en&projectId={fixture.AccessibleProjectId}");

        xlsx.StatusCode.Should().Be(HttpStatusCode.OK);
        xlsx.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using (var workbook = new XLWorkbook(await xlsx.Content.ReadAsStreamAsync()))
        {
            workbook.Worksheet("Projects").Cell(2, 1).GetValue<int>()
                .Should().Be(fixture.AccessibleProjectId);
            var projectNameCell = workbook.Worksheet("Projects").Cell(2, 3);
            projectNameCell.HasFormula.Should().BeFalse();
            projectNameCell.DataType.Should().Be(XLDataType.Text);
            workbook.Worksheet("Unavailable").LastRowUsed()!.RowNumber().Should().BeGreaterThan(2);

            workbook.Worksheets.Select(sheet => sheet.Name).Should().Contain(
                ["Construction", "Acceptance", "Permits", "Finance"]);
            var constructionValues = workbook.Worksheet("Construction").CellsUsed()
                .Select(cell => cell.GetString()).ToArray();
            constructionValues.Should().Contain(["Planned", "Overdue task"]);
            var acceptanceValues = workbook.Worksheet("Acceptance").CellsUsed()
                .Select(cell => cell.GetString()).ToArray();
            acceptanceValues.Should().Contain(["Submitted", "2"]);
            var permitValues = workbook.Worksheet("Permits").CellsUsed()
                .Select(cell => cell.GetString()).ToArray();
            permitValues.Should().Contain(value => value.StartsWith("OVERDUE-", StringComparison.Ordinal));
            permitValues.Should().Contain("Overdue");
            var financeValues = workbook.Worksheet("Finance").CellsUsed()
                .Select(cell => cell.GetString()).ToArray();
            financeValues.Should().Contain(["Approved", "Pending", "500"]);
        }
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        pdfBytes.Take(4).Should().Equal("%PDF"u8.ToArray());
        using (var document = PdfDocument.Open(pdfBytes))
        {
            var pdfText = string.Join('\n', document.GetPages()
                .Select(page => ContentOrderTextExtractor.GetText(page)));
            pdfText.Should().Contain("Overdue task");
            pdfText.Should().Contain("Revision 2: 1");
            pdfText.Should().Contain("Milestone schedule - Pending: 500");
            pdfText.Should().Contain("Milestones are contractual schedule values, not cash or revenue.");
        }

        var auditCount = 0;
        for (var attempt = 0; attempt < 30 && auditCount < 2; attempt++)
        {
            await Task.Delay(100);
            auditCount = await WithDbAsync(db => db.AuditLogs.CountAsync(item =>
                item.Action == "project-report.export" &&
                item.ResourceId == fixture.AccessibleProjectId.ToString()));
        }
        auditCount.Should().BeGreaterThanOrEqualTo(2);
        var metadataJson = await WithDbAsync(db => db.AuditLogs
            .Where(item => item.Action == "project-report.export" &&
                item.ResourceId == fixture.AccessibleProjectId.ToString())
            .OrderByDescending(item => item.Id)
            .Select(item => item.MetadataJson)
            .FirstAsync());
        using var metadata = JsonDocument.Parse(metadataJson!);
        metadata.RootElement.GetProperty("ProjectCount").GetInt32().Should().Be(1);
        metadata.RootElement.GetProperty("Language").GetString().Should().Be("en");
    }

    private Task AuthenticateAsync(string roleCode) => AuthTestHelper.AuthenticateAsync(
        Client,
        client => AuthTestHelper.LoginAsRoleAsync(client, roleCode));

    private async Task<ReportFixture> SeedFixtureAsync()
    {
        return await WithDbAsync(async db =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;
            var customerId = await db.Customers.Select(item => item.Id).FirstOrDefaultAsync();
            if (customerId == 0)
            {
                var customer = new Customer
                {
                    Name = $"Report customer {Guid.NewGuid():N}",
                    SourceCode = "other",
                };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
                customerId = customer.Id;
            }
            var pmId = await db.Users.Where(item =>
                    item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(item => item.Id).SingleAsync();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var accessible = new OperationalProject
            {
                Code = $"PJ-RPT-{suffix}",
                Name = $"=Report Project {suffix}",
                CustomerId = customerId,
                ProjectManagerUserId = pmId,
                Status = OperationalProjectStatus.Active,
                CreatedByUserId = pmId,
            };
            var inaccessible = new OperationalProject
            {
                Code = $"PJ-OTH-{suffix}",
                Name = $"Other report {suffix}",
                CustomerId = customerId,
                Status = OperationalProjectStatus.Active,
            };
            db.OperationalProjects.AddRange(accessible, inaccessible);
            await db.SaveChangesAsync();

            var design = new DesignProject
            {
                OperationalProjectId = accessible.Id,
                ProjectCode = $"DP-RPT-{suffix}",
                Name = $"Design report {suffix}",
                CustomerId = customerId,
                ProjectManagerUserId = pmId,
            };
            db.DesignProjects.Add(design);
            await db.SaveChangesAsync();

            db.DesignSchedulePhases.Add(new DesignSchedulePhase
            {
                OperationalProjectId = accessible.Id,
                DesignProjectId = design.Id,
                Code = DesignSchedulePhaseCode.Concept,
                PlannedStart = today.AddDays(-10),
                PlannedEnd = today.AddDays(10),
                Weight = 100,
                Status = DesignScheduleStatus.InProgress,
                CreatedByUserId = pmId,
                UpdatedByUserId = pmId,
            });
            db.ConstructionTasks.AddRange(
                new ConstructionTask
                {
                    DesignProjectId = design.Id,
                    TaskCode = $"OLD-{suffix}",
                    Name = "Overdue task",
                    PlannedStart = today.AddDays(-5),
                    PlannedEnd = today.AddDays(-1),
                    Status = ConstructionTaskStatus.Planned,
                },
                new ConstructionTask
                {
                    DesignProjectId = design.Id,
                    TaskCode = $"TODAY-{suffix}",
                    Name = "Completed today",
                    PlannedStart = today,
                    PlannedEnd = today,
                    ActualStart = today,
                    ActualEnd = today,
                    ProgressPercent = 100,
                    Status = ConstructionTaskStatus.Completed,
                });
            db.AcceptanceRecords.AddRange(
                new AcceptanceRecord
                {
                    DesignProjectId = design.Id,
                    AcceptanceCode = $"ACC-OLD-{suffix}",
                    Title = "Overdue acceptance",
                    AcceptanceDate = today.AddDays(-1),
                    Status = AcceptanceStatus.Submitted,
                    RevisionCount = 2,
                },
                new AcceptanceRecord
                {
                    DesignProjectId = design.Id,
                    AcceptanceCode = $"ACC-NOW-{suffix}",
                    Title = "Acceptance today",
                    AcceptanceDate = today,
                    Status = AcceptanceStatus.Approved,
                });
            db.PermitChecklistItems.AddRange(
                new PermitChecklistItem
                {
                    DesignProjectId = design.Id,
                    PermitTypeCode = $"OVERDUE-{suffix}",
                    TargetDeadline = today.AddDays(-1).ToDateTime(TimeOnly.MinValue),
                    Status = PermitStatus.Preparing,
                },
                new PermitChecklistItem
                {
                    DesignProjectId = design.Id,
                    PermitTypeCode = $"DUE-{suffix}",
                    TargetDeadline = today.ToDateTime(TimeOnly.MinValue),
                    Status = PermitStatus.UnderReview,
                },
                new PermitChecklistItem
                {
                    DesignProjectId = design.Id,
                    PermitTypeCode = $"EXP-{suffix}",
                    TargetDeadline = today.ToDateTime(TimeOnly.MinValue),
                    ExpiresAt = today.AddDays(1).ToDateTime(TimeOnly.MinValue),
                    Status = PermitStatus.Issued,
                });

            var opportunity = new Opportunity
            {
                Name = $"Report opportunity {suffix}",
                CustomerId = customerId,
                OperationalProjectId = accessible.Id,
                CreatedAt = now,
            };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync();
            db.Quotes.Add(new Quote
            {
                Code = $"QT-RPT-{suffix}",
                OpportunityId = opportunity.Id,
                OperationalProjectId = accessible.Id,
                GrandTotal = 2500m,
                Subtotal = 2500m,
                Status = QuoteStatus.Approved,
                CreatedAt = now,
            });
            var contract = new Contract
            {
                ContractNumber = $"HD-RPT-{suffix}",
                CustomerId = customerId,
                OperationalProjectId = accessible.Id,
                Status = ContractStatus.Signed,
                SignedDate = now.AddDays(-60),
                Value = 1000m,
                CreatedAt = now.AddDays(-60),
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            db.ContractAppendices.Add(new ContractAppendix
            {
                ContractId = contract.Id,
                VoNumber = 1,
                Title = "Approved variation",
                Reason = "Approved scope change",
                ValueDelta = 200m,
                Status = ContractAppendixStatus.Approved,
                DecidedAt = now,
                UpdatedAt = now,
            });
            db.ContractPaymentMilestones.Add(new ContractPaymentMilestone
            {
                ContractId = contract.Id,
                Order = 1,
                Name = "Contractual schedule milestone",
                PercentValue = 50m,
                DueDate = today.ToDateTime(TimeOnly.MinValue),
                Status = PaymentMilestoneStatus.Pending,
            });
            await db.SaveChangesAsync();
            return new ReportFixture(accessible.Id, inaccessible.Id, today);
        });
    }

    private sealed record ReportFixture(int AccessibleProjectId, int InaccessibleProjectId, DateOnly Today);
}
