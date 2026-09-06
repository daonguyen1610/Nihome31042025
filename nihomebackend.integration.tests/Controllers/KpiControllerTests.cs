using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class KpiControllerTests : IntegrationTestBase
{
    public KpiControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Calculate_UsesVersionedDefinitionsEvidenceAndLockedSnapshots()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var saleUserId = await SeedSalesDataAsync();
        await WithDbAsync(db =>
        {
            KpiSeeder.Seed(db);
            return Task.CompletedTask;
        });

        var definitionsResponse = await Client.GetAsync("/api/kpi/definitions");
        definitionsResponse.EnsureSuccessStatusCode();
        var definitions = (await ReadJsonAsync(definitionsResponse)).EnumerateArray().ToList();
        definitions.Should().HaveCountGreaterThanOrEqualTo(19);
        var eligibleUsers = await ReadJsonAsync(await Client.GetAsync("/api/kpi/eligible-users"));
        eligibleUsers.EnumerateArray().Should().Contain(item =>
            item.GetProperty("userId").GetInt32() == saleUserId &&
            item.GetProperty("positionCode").GetString() == "SALES");
        eligibleUsers.EnumerateArray().Should().NotContain(item =>
            item.GetProperty("positionCode").GetString() == "SUPER_ADMIN" ||
            item.GetProperty("positionCode").GetString() == "ADMIN");
        var revenue = definitions.Single(item => item.GetProperty("code").GetString() == "SALES_REVENUE");
        var responseTime = definitions.Single(item => item.GetProperty("code").GetString() == "SALES_FIRST_RESPONSE");

        var invalidWeight = await Client.PutAsJsonAsync(
            $"/api/kpi/definitions/{revenue.GetProperty("id").GetInt32()}",
            new
            {
                weight = 0.7m,
                targetValue = 200_000_000m,
                targetDirection = "HigherIsBetter",
                isActive = true,
                rowVersion = revenue.GetProperty("rowVersion").GetString(),
            });
        invalidWeight.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var firstCalculate = await SendWithIdempotencyAsync(
            HttpMethod.Post,
            "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = saleUserId });
        firstCalculate.EnsureSuccessStatusCode();
        var firstDashboard = await ReadJsonAsync(firstCalculate);
        var conversion = firstDashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "SALES_CONVERSION");
        conversion.GetProperty("rawValue").GetDecimal().Should().Be(50m);
        conversion.GetProperty("numerator").GetDecimal().Should().Be(1m);
        conversion.GetProperty("denominator").GetDecimal().Should().Be(2m);
        conversion.GetProperty("status").GetString().Should().Be("Available");
        conversion.GetProperty("evidenceJson").GetString().Should().Contain("RecordIds");
        firstDashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "SALES_REVENUE")
            .GetProperty("status").GetString().Should().Be("MissingConfiguration");
        var period = await WithDbAsync(db => db.KpiPeriods.AsNoTracking()
            .SingleAsync(item => item.Year == 2026 && item.Month == 8 && item.UserId == saleUserId));
        period.PeriodStartUtc.Should().Be(new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc));
        period.PeriodEndUtc.Should().Be(new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc));

        var updateDefinition = await Client.PutAsJsonAsync(
            $"/api/kpi/definitions/{revenue.GetProperty("id").GetInt32()}",
            new
            {
                weight = 0.4m,
                targetValue = 200_000_000m,
                minimumAcceptableScore = 60m,
                targetDirection = "HigherIsBetter",
                isActive = true,
                rowVersion = revenue.GetProperty("rowVersion").GetString(),
            });
        updateDefinition.EnsureSuccessStatusCode();
        (await ReadJsonAsync(updateDefinition)).GetProperty("version").GetInt32()
            .Should().Be(revenue.GetProperty("version").GetInt32() + 1);
        var updateResponseTarget = await Client.PutAsJsonAsync(
            $"/api/kpi/definitions/{responseTime.GetProperty("id").GetInt32()}",
            new
            {
                weight = 0.2m,
                targetValue = 24m,
                targetDirection = "LowerIsBetter",
                isActive = true,
                rowVersion = responseTime.GetProperty("rowVersion").GetString(),
            });
        updateResponseTarget.EnsureSuccessStatusCode();

        using var recalculatedResponse = await SendWithIdempotencyAsync(
            HttpMethod.Post,
            "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = saleUserId });
        recalculatedResponse.EnsureSuccessStatusCode();
        var recalculated = await ReadJsonAsync(recalculatedResponse);
        var revenueScore = recalculated.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "SALES_REVENUE");
        revenueScore.GetProperty("rawValue").GetDecimal().Should().Be(100_000_000m);
        revenueScore.GetProperty("score").GetDecimal().Should().Be(50m);

        async Task ValidateProcurementAndMaterialMetricsAsync()
        {
            await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
            var users = await WithDbAsync(async db =>
            {
                KpiSeeder.Seed(db);
                var procurementUserId = await db.Users
                    .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
                    .Select(user => user.Id).SingleAsync();
                var pmUserId = await db.Users
                    .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                    .Select(user => user.Id).SingleAsync();
                var tenderUserId = await db.Users
                    .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["QS"])
                    .Select(user => user.Id).SingleAsync();
                var customer = new Customer { Type = CustomerType.Company, Name = "KPI Procurement Customer", SourceCode = "referral" };
                var vendor = new Vendor { VendorCode = $"KPI-{Guid.NewGuid():N}"[..20], CompanyName = "KPI Supplier", VendorType = VendorType.Supplier, CreatedByUserId = procurementUserId };
                db.AddRange(customer, vendor);
                await db.SaveChangesAsync();
                var project = new OperationalProject { Code = $"PJ-KPI-PROC-{Guid.NewGuid():N}"[..32], Name = "KPI Procurement", CustomerId = customer.Id, ProjectManagerUserId = pmUserId };
                db.OperationalProjects.Add(project);
                await db.SaveChangesAsync();
                var tender = new Tender
                {
                    Code = $"TD-KPI-{Guid.NewGuid():N}"[..24],
                    Name = "KPI Tender",
                    CustomerId = customer.Id,
                    SubmissionDeadline = new DateTime(2026, 7, 1),
                    PreparerUserId = tenderUserId,
                    Status = TenderStatus.Won,
                    ClosedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                };
                db.Tenders.Add(tender);
                await db.SaveChangesAsync();
                var estimate = new TenderEstimateRevision
                {
                    TenderId = tender.Id,
                    VersionNumber = 1,
                    Status = TenderEstimateRevisionStatus.Approved,
                    Currency = "VND",
                    CostSubtotal = 10_000m,
                    BidSubtotal = 11_000m,
                    GrandBidTotal = 11_000m,
                    SourceFileName = "kpi.xlsx",
                    SourceSha256 = new string('a', 64),
                    ImportedByUserId = tenderUserId,
                    ImportedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    ApprovedByUserId = pmUserId,
                    ApprovedAt = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                };
                db.TenderEstimateRevisions.Add(estimate);
                await db.SaveChangesAsync();
                var boq = new ProjectBoqRevision
                {
                    OperationalProjectId = project.Id,
                    RevisionNumber = 1,
                    Status = ProjectBoqRevisionStatus.Approved,
                    Currency = "VND",
                    CostTotal = 10_000m,
                    PreparedByUserId = procurementUserId,
                    ApprovedByUserId = pmUserId,
                    ApprovedAt = new DateTime(2026, 7, 25, 0, 0, 0, DateTimeKind.Utc),
                    SourceTenderEstimateRevisionId = estimate.Id,
                    Lines = [new ProjectBoqLine { ItemCode = "MAT-1", Description = "Material", Unit = "kg", ApprovedQuantity = 100m, BudgetUnitPrice = 100m, Amount = 10_000m }],
                };
                db.ProjectBoqRevisions.Add(boq);
                await db.SaveChangesAsync();
                project.FinalProjectBoqRevisionId = boq.Id;
                project.Status = OperationalProjectStatus.Completed;
                project.CompletedAt = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
                var contract = new Contract
                {
                    ContractNumber = $"HD-KPI-PROC-{Guid.NewGuid():N}"[..30],
                    CustomerId = customer.Id,
                    VendorId = vendor.Id,
                    OperationalProjectId = project.Id,
                    Direction = ContractDirection.Downstream,
                    Type = ContractType.Supply,
                    Status = ContractStatus.Signed,
                    SignedDate = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
                    Value = 9_000m,
                };
                db.Contracts.Add(contract);
                await db.SaveChangesAsync();
                db.ContractLines.Add(new ContractLine { ContractId = contract.Id, ProjectBoqLineId = boq.Lines[0].Id, ProcurementOwnerUserId = procurementUserId, Quantity = 100m, NegotiatedUnitPrice = 90m });
                db.MaterialRequests.Add(new MaterialRequest
                {
                    OperationalProjectId = project.Id,
                    Code = "MR-KPI-1",
                    SiteRequesterUserId = pmUserId,
                    ResponsibleSiteUserId = pmUserId,
                    AssignedProcurementUserId = procurementUserId,
                    RequiredAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                    Status = MaterialRequestStatus.Fulfilled,
                    ApprovedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    FulfilledAt = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    Lines = [new MaterialRequestLine { ProjectBoqLineId = boq.Lines[0].Id, RequestedQuantity = 100m }],
                });
                db.WarehouseIssues.Add(new WarehouseIssue
                {
                    OperationalProjectId = project.Id,
                    Code = "WI-KPI-1",
                    ResponsibleSiteUserId = pmUserId,
                    IssuedByUserId = procurementUserId,
                    IssuedAt = new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
                    PostedAt = new DateTime(2026, 8, 8, 1, 0, 0, DateTimeKind.Utc),
                    PostedByUserId = procurementUserId,
                    Status = WarehouseLedgerStatus.Posted,
                    Lines = [new WarehouseIssueLine { ProjectBoqLineId = boq.Lines[0].Id, IssuedQuantity = 110m }],
                });
                db.VendorRatings.Add(new VendorRating
                {
                    OperationalProjectId = project.Id,
                    ContractId = contract.Id,
                    VendorId = vendor.Id,
                    VersionNumber = 1,
                    Status = VendorRatingStatus.Approved,
                    ProcurementOwnerUserId = procurementUserId,
                    QualityScore = 80m,
                    ScheduleScore = 80m,
                    CostScore = 80m,
                    HseScore = 80m,
                    OverallScore = 80m,
                    PreparedByUserId = procurementUserId,
                    ApprovedByUserId = pmUserId,
                    ApprovedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                });
                var delivery = await db.KpiDefinitions.SingleAsync(item => item.Code == "PROCUREMENT_ON_TIME");
                delivery.TargetValue = 48m;
                var waste = await db.KpiDefinitions.SingleAsync(item => item.Code == "SITE_MATERIAL_WASTE");
                waste.TargetValue = 10m;
                await db.SaveChangesAsync();
                return new { procurementUserId, pmUserId, tenderUserId };
            });

            using var procurementResponse = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate", new { year = 2026, month = 8, userId = users.procurementUserId });
            procurementResponse.EnsureSuccessStatusCode();
            var procurement = await ReadJsonAsync(procurementResponse);
            var procurementScores = procurement.GetProperty("scores").EnumerateArray().ToList();
            procurementScores.Single(item => item.GetProperty("code").GetString() == "PROCUREMENT_COST").GetProperty("score").GetDecimal().Should().Be(10m);
            procurementScores.Single(item => item.GetProperty("code").GetString() == "PROCUREMENT_ON_TIME").GetProperty("rawValue").GetDecimal().Should().Be(24m);
            procurementScores.Single(item => item.GetProperty("code").GetString() == "PROCUREMENT_VENDOR_RATING").GetProperty("score").GetDecimal().Should().Be(80m);

            using var siteResponse = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate", new { year = 2026, month = 8, userId = users.pmUserId });
            siteResponse.EnsureSuccessStatusCode();
            var site = await ReadJsonAsync(siteResponse);
            var wasteScore = site.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "SITE_MATERIAL_WASTE");
            wasteScore.GetProperty("rawValue").GetDecimal().Should().Be(10m);
            wasteScore.GetProperty("score").GetDecimal().Should().Be(100m);

            using var tenderResponse = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate", new { year = 2026, month = 8, userId = users.tenderUserId });
            tenderResponse.EnsureSuccessStatusCode();
            var tenderDashboard = await ReadJsonAsync(tenderResponse);
            var accuracy = tenderDashboard.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "TENDER_ESTIMATE_ACCURACY");
            accuracy.GetProperty("score").GetDecimal().Should().Be(100m);
        }
        await ValidateProcurementAndMaterialMetricsAsync();
        revenueScore.GetProperty("status").GetString().Should().Be("Available");
        (await WithDbAsync(db => db.Notifications.CountAsync(item =>
            item.UserId == saleUserId && item.Module == "analytics.kpi" &&
            item.RefEntityType == "KpiPeriod"))).Should().Be(1);

        var locked = await Client.PostAsJsonAsync("/api/kpi/periods/2026/8/lock", new
        {
            userId = saleUserId,
            note = "Monthly HR close",
            rowVersion = recalculated.GetProperty("periodRowVersion").GetString(),
        });
        locked.EnsureSuccessStatusCode();
        var lockedBody = await ReadJsonAsync(locked);
        lockedBody.GetProperty("periodStatus").GetString().Should().Be("Locked");
        lockedBody.GetProperty("lockNote").GetString().Should().Be("Monthly HR close");
        lockedBody.GetProperty("lockedByName").GetString().Should().NotBeNullOrWhiteSpace();

        var updatedRevenue = await ReadJsonAsync(updateDefinition);
        var newerDefinition = await Client.PutAsJsonAsync(
            $"/api/kpi/definitions/{updatedRevenue.GetProperty("id").GetInt32()}",
            new
            {
                weight = 0.4m,
                targetValue = 50_000_000m,
                minimumAcceptableScore = 30m,
                targetDirection = "HigherIsBetter",
                isActive = true,
                rowVersion = updatedRevenue.GetProperty("rowVersion").GetString(),
            });
        newerDefinition.EnsureSuccessStatusCode();
        var lockedDashboard = await ReadJsonAsync(await Client.GetAsync(
            $"/api/kpi/dashboard?year=2026&month=8&userId={saleUserId}"));
        var lockedRevenue = lockedDashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "SALES_REVENUE");
        lockedRevenue.GetProperty("targetValue").GetDecimal().Should().Be(200_000_000m);
        lockedRevenue.GetProperty("weight").GetDecimal().Should().Be(0.4m);
        lockedRevenue.GetProperty("minimumAcceptableScore").GetDecimal().Should().Be(60m);
        lockedRevenue.GetProperty("definitionVersion").GetInt32()
            .Should().Be(updatedRevenue.GetProperty("version").GetInt32());

        var export = await Client.GetAsync($"/api/kpi/export?year=2026&month=8&userId={saleUserId}");
        export.EnsureSuccessStatusCode();
        export.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        (await export.Content.ReadAsStringAsync()).Should().Contain("SALES_CONVERSION");

        using var blockedRecalculation = await SendWithIdempotencyAsync(
            HttpMethod.Post,
            "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = saleUserId });
        blockedRecalculation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.KpiScoreSnapshots.CountAsync(item =>
            item.KpiPeriod.Year == 2026 && item.KpiPeriod.Month == 8 && item.UserId == saleUserId)))
            .Should().Be(3);
    }

    [Fact]
    public async Task Dashboard_OtherUserRequiresViewAll()
    {
        var saleUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
            .Select(user => user.Id)
            .SingleAsync());
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));

        (await Client.GetAsync($"/api/kpi/dashboard?year=2026&month=8&userId={saleUserId + 1}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Calculate_DesignAndSiteMetrics_UsesOperationalSourcesAndKeepsMissingSourcesExplicit()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var users = await WithDbAsync(async db =>
        {
            KpiSeeder.Seed(db);
            var designUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["DESIGN"])
                .Select(user => user.Id).SingleAsync();
            var pmUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id).SingleAsync();
            var customer = new Customer { Type = CustomerType.Individual, Name = "KPI Project Customer", SourceCode = "marketing" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject { Code = $"PJ-KPI-{Guid.NewGuid():N}"[..24], Name = "KPI Project", CustomerId = customer.Id, ProjectManagerUserId = pmUserId };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var designProject = new DesignProject
            {
                OperationalProjectId = project.Id,
                ProjectCode = $"DP-KPI-{Guid.NewGuid():N}"[..24],
                Name = "KPI Design",
                CustomerId = customer.Id,
                StartDate = new DateTime(2026, 8, 1),
                Deadline = new DateTime(2026, 9, 30),
                ProjectManagerUserId = pmUserId,
                DesignLeadUserId = designUserId,
            };
            var designMember = new OperationalProjectMember { OperationalProjectId = project.Id, UserId = designUserId, Position = "Designer", StartedAt = DateTime.UtcNow, CreatedByUserId = pmUserId, UpdatedByUserId = pmUserId };
            db.AddRange(designProject, designMember);
            await db.SaveChangesAsync();
            var phase = new DesignSchedulePhase { OperationalProjectId = project.Id, DesignProjectId = designProject.Id, Code = DesignSchedulePhaseCode.Concept, PlannedStart = new DateOnly(2026, 8, 1), PlannedEnd = new DateOnly(2026, 8, 20), ActualStart = new DateOnly(2026, 8, 1), ActualEnd = new DateOnly(2026, 8, 19), Status = DesignScheduleStatus.Completed, Weight = 100, CreatedByUserId = pmUserId, UpdatedByUserId = pmUserId };
            db.DesignSchedulePhases.Add(phase);
            await db.SaveChangesAsync();
            db.DesignScheduleTasks.Add(new DesignScheduleTask { OperationalProjectId = project.Id, DesignProjectId = designProject.Id, PhaseId = phase.Id, Code = "KPI-DES-1", Name = "Issue drawing", DepartmentCode = "design", AssigneeMemberId = designMember.Id, PlannedStart = new DateOnly(2026, 8, 1), PlannedEnd = new DateOnly(2026, 8, 15), ActualStart = new DateOnly(2026, 8, 1), ActualEnd = new DateOnly(2026, 8, 14), Status = DesignScheduleStatus.Completed, Weight = 100, CreatedByUserId = pmUserId, UpdatedByUserId = pmUserId });
            db.BasicDesignDocs.Add(new BasicDesignDoc { DesignProjectId = designProject.Id, DisciplineCode = "architecture", DocumentCode = "KPI-BD-1", Title = "First pass", OwnerUserId = designUserId, Status = BasicDesignDocStatus.InternallyApproved, CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc) });
            db.ConstructionTasks.Add(new ConstructionTask { DesignProjectId = designProject.Id, TaskCode = "KPI-SITE-1", Name = "Site work", PlannedStart = new DateOnly(2026, 8, 1), PlannedEnd = new DateOnly(2026, 8, 10), ActualStart = new DateOnly(2026, 8, 1), ActualEnd = new DateOnly(2026, 8, 11), OwnerUserId = pmUserId, Status = ConstructionTaskStatus.Completed, ProgressPercent = 100 });
            db.PunchItems.Add(new PunchItem { DesignProjectId = designProject.Id, PunchCode = "KPI-P-1", Title = "Design error", RootCause = PunchRootCause.Design, ResponsibleDesignUserId = designUserId, RootCauseConfirmedAt = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc), RootCauseConfirmedByUserId = pmUserId, Status = PunchStatus.Verified, VerifiedAt = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc), VerifiedByUserId = pmUserId });
            db.AcceptanceRecords.Add(new AcceptanceRecord { DesignProjectId = designProject.Id, AcceptanceCode = "KPI-ACC-1", Title = "First acceptance", AcceptanceDate = new DateOnly(2026, 8, 20), Status = AcceptanceStatus.Approved, ApprovedAt = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc), CreatedByUserId = pmUserId, RevisionCount = 0 });
            var progress = await db.KpiDefinitions.SingleAsync(item => item.Code == "SITE_PROGRESS");
            progress.TargetValue = 10m;
            var designErrors = await db.KpiDefinitions.SingleAsync(item => item.Code == "DESIGN_SITE_ERRORS");
            designErrors.TargetValue = 1m;
            await db.SaveChangesAsync();
            return new { designUserId, pmUserId };
        });

        using var designResponse = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate", new { year = 2026, month = 8, userId = users.designUserId });
        designResponse.EnsureSuccessStatusCode();
        var design = await ReadJsonAsync(designResponse);
        design.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "DESIGN_ON_TIME").GetProperty("score").GetDecimal().Should().Be(100m);
        design.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "DESIGN_FIRST_PASS").GetProperty("score").GetDecimal().Should().Be(100m);
        var designErrors = design.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "DESIGN_SITE_ERRORS");
        designErrors.GetProperty("status").GetString().Should().Be("Available");
        designErrors.GetProperty("rawValue").GetDecimal().Should().Be(1m);

        using var siteResponse = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate", new { year = 2026, month = 8, userId = users.pmUserId });
        siteResponse.EnsureSuccessStatusCode();
        var site = await ReadJsonAsync(siteResponse);
        site.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "SITE_PROGRESS").GetProperty("status").GetString().Should().Be("Available");
        site.GetProperty("scores").EnumerateArray().Single(item => item.GetProperty("code").GetString() == "SITE_FIRST_ACCEPTANCE").GetProperty("score").GetDecimal().Should().Be(100m);
        site.GetProperty("isComplete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Calculate_AccountingCollection_UsesDueMonthAndResponsibleAccountant()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var accountantUserId = await WithDbAsync(async db =>
        {
            KpiSeeder.Seed(db);
            var accountantId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id)
                .SingleAsync();
            var saleUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id)
                .SingleAsync();
            var customer = new Customer { Type = CustomerType.Individual, Name = "KPI Receivable Customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var contract = new Contract
            {
                ContractNumber = $"HD-KPI-AR-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Design,
                Status = ContractStatus.InProgress,
                Value = 100_000_000m,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            db.ContractPaymentMilestones.AddRange(
                new ContractPaymentMilestone
                {
                    ContractId = contract.Id,
                    Order = 1,
                    Name = "Paid on time",
                    PercentValue = 50m,
                    DueDate = new DateTime(2026, 8, 15),
                    ActualPaymentDate = new DateTime(2026, 8, 15),
                    ResponsibleAccountantUserId = accountantId,
                    Status = PaymentMilestoneStatus.Paid,
                },
                new ContractPaymentMilestone
                {
                    ContractId = contract.Id,
                    Order = 2,
                    Name = "Overdue unpaid",
                    PercentValue = 40m,
                    DueDate = new DateTime(2026, 8, 20),
                    ResponsibleAccountantUserId = accountantId,
                    Status = PaymentMilestoneStatus.Requested,
                },
                new ContractPaymentMilestone
                {
                    ContractId = contract.Id,
                    Order = 3,
                    Name = "Different owner",
                    PercentValue = 10m,
                    DueDate = new DateTime(2026, 8, 25),
                    ActualPaymentDate = new DateTime(2026, 8, 25),
                    ResponsibleAccountantUserId = saleUserId,
                    Status = PaymentMilestoneStatus.Paid,
                });
            await db.SaveChangesAsync();
            return accountantId;
        });

        using var response = await SendWithIdempotencyAsync(
            HttpMethod.Post,
            "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = accountantUserId });
        response.EnsureSuccessStatusCode();
        var dashboard = await ReadJsonAsync(response);
        var collection = dashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "ACCOUNTING_COLLECTION");
        collection.GetProperty("status").GetString().Should().Be("Available");
        collection.GetProperty("numerator").GetInt32().Should().Be(1);
        collection.GetProperty("denominator").GetInt32().Should().Be(2);
        collection.GetProperty("score").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task Calculate_AccountingPaymentSpeed_UsesPaidBoundaryAndAssignedAccountant()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var accountantUserId = await WithDbAsync(async db =>
        {
            KpiSeeder.Seed(db);
            var accountantId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id).SingleAsync();
            var otherUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id).SingleAsync();
            var customer = new Customer { Type = CustomerType.Company, Name = "Payment KPI Customer", SourceCode = "referral" };
            var vendor = new Vendor { VendorCode = $"KPI-{Guid.NewGuid():N}"[..16], CompanyName = "Payment KPI Vendor", VendorType = VendorType.Supplier };
            db.AddRange(customer, vendor);
            await db.SaveChangesAsync();
            var contract = new Contract
            {
                ContractNumber = $"HD-KPI-PAY-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                VendorId = vendor.Id,
                Direction = ContractDirection.Downstream,
                Type = ContractType.Supply,
                Status = ContractStatus.InProgress,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            db.PaymentRequests.AddRange(
                PaidRequest("PAY-KPI-1", "KPI-INV-1", contract.Id, vendor.Id, accountantId,
                    new DateTime(2026, 7, 30, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc)),
                PaidRequest("PAY-KPI-2", "KPI-INV-2", contract.Id, vendor.Id, accountantId,
                    new DateTime(2026, 8, 8, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 10, 17, 0, 0, DateTimeKind.Utc)),
                PaidRequest("PAY-KPI-3", "KPI-INV-3", contract.Id, vendor.Id, accountantId,
                    new DateTime(2026, 8, 30, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc)),
                PaidRequest("PAY-KPI-4", "KPI-INV-4", contract.Id, vendor.Id, otherUserId,
                    new DateTime(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 11, 17, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
            return accountantId;
        });

        using var response = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = accountantUserId });
        response.EnsureSuccessStatusCode();
        var score = (await ReadJsonAsync(response)).GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "ACCOUNTING_PAYMENT_SPEED");
        score.GetProperty("status").GetString().Should().Be("Available");
        score.GetProperty("rawValue").GetDecimal().Should().Be(36m);
        score.GetProperty("score").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Calculate_AccountingAccuracy_CountsOriginalPostCloseCorrectionOnceAfterReversal()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var accountantUserId = await WithDbAsync(async db =>
        {
            KpiSeeder.Seed(db);
            var accountantId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
                .Select(user => user.Id).SingleAsync();
            var otherUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id).SingleAsync();
            var pmId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id).SingleAsync();
            var customer = new Customer { Type = CustomerType.Company, Name = "Correction KPI Customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-KPI-AC-{Guid.NewGuid():N}"[..30],
                Name = "Correction KPI Project",
                CustomerId = customer.Id,
                ProjectManagerUserId = pmId,
            };
            var accountingPeriod = new AccountingPeriod
            {
                Year = 2031,
                Month = 6,
                PeriodStartUtc = new DateTime(2031, 5, 31, 17, 0, 0, DateTimeKind.Utc),
                PeriodEndUtc = new DateTime(2031, 6, 30, 17, 0, 0, DateTimeKind.Utc),
                Status = AccountingPeriodStatus.Closed,
                ClosedAt = new DateTime(2031, 7, 2),
                CloseReason = "June close",
            };
            db.AddRange(project, accountingPeriod);
            await db.SaveChangesAsync();
            var original = Correction("AC-KPI-1", project.Id, accountingPeriod.Id, accountantId,
                AccountingCorrectionStatus.Reversed, new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc));
            var differentOwner = Correction("AC-KPI-2", project.Id, accountingPeriod.Id, otherUserId,
                AccountingCorrectionStatus.Approved, new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc));
            var endBoundary = Correction("AC-KPI-3", project.Id, accountingPeriod.Id, accountantId,
                AccountingCorrectionStatus.Approved, new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc));
            db.AddRange(original, differentOwner, endBoundary);
            await db.SaveChangesAsync();
            db.AccountingCorrections.Add(new AccountingCorrection
            {
                Code = "AC-KPI-REV",
                OperationalProjectId = project.Id,
                AccountingPeriodId = accountingPeriod.Id,
                SourceEntityType = nameof(Contract),
                SourceEntityId = 1,
                ReasonCode = "REVERSAL",
                OriginalValue = 90m,
                CorrectedValue = 100m,
                ResponsibleAccountantUserId = accountantId,
                RecordedByUserId = accountantId,
                Status = AccountingCorrectionStatus.Approved,
                ApprovedAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                ReversalOfCorrectionId = original.Id,
            });
            await db.SaveChangesAsync();
            return accountantId;
        });

        using var response = await SendWithIdempotencyAsync(HttpMethod.Post, "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = accountantUserId });
        response.EnsureSuccessStatusCode();
        var score = (await ReadJsonAsync(response)).GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "ACCOUNTING_ACCURACY");
        score.GetProperty("status").GetString().Should().Be("Available");
        score.GetProperty("rawValue").GetDecimal().Should().Be(1m);
        score.GetProperty("score").GetDecimal().Should().Be(100m);
    }

    [Fact]
    public async Task Calculate_SiteHse_UsesConfirmedAtAttributionStatusAndPeriodBoundary()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var pmUserId = await WithDbAsync(async db =>
        {
            KpiSeeder.Seed(db);
            var pmId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
                .Select(user => user.Id)
                .SingleAsync();
            var otherUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id)
                .SingleAsync();
            var customer = new Customer { Type = CustomerType.Company, Name = "KPI HSE Customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-KPI-HSE-{Guid.NewGuid():N}"[..30],
                Name = "KPI HSE Project",
                CustomerId = customer.Id,
                ProjectManagerUserId = pmId,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var start = new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc);
            db.HseViolations.AddRange(
                Hse(project.Id, pmId, HseViolationStatus.Confirmed, start, "START"),
                Hse(project.Id, pmId, HseViolationStatus.Remediated, end.AddTicks(-1), "END-MINUS"),
                Hse(project.Id, pmId, HseViolationStatus.Closed, new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc), "CLOSED"),
                Hse(project.Id, pmId, HseViolationStatus.Cancelled, new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc), "CANCELLED"),
                Hse(project.Id, otherUserId, HseViolationStatus.Confirmed, new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc), "OTHER"),
                Hse(project.Id, pmId, HseViolationStatus.Confirmed, end, "NEXT"));
            var definition = await db.KpiDefinitions.SingleAsync(item => item.Code == "SITE_HSE");
            definition.TargetValue = 2m;
            await db.SaveChangesAsync();
            return pmId;
        });

        using var response = await SendWithIdempotencyAsync(
            HttpMethod.Post,
            "/api/kpi/calculate",
            new { year = 2026, month = 8, userId = pmUserId });
        response.EnsureSuccessStatusCode();
        var dashboard = await ReadJsonAsync(response);
        var hse = dashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "SITE_HSE");
        hse.GetProperty("status").GetString().Should().Be("Available");
        hse.GetProperty("rawValue").GetDecimal().Should().Be(3m);
        hse.GetProperty("score").GetDecimal().Should().BeApproximately(66.6667m, 0.0001m);
        hse.GetProperty("evidenceJson").GetString().Should().Contain("HseViolation");
    }

    private static HseViolation Hse(
        int projectId,
        int responsibleUserId,
        HseViolationStatus status,
        DateTime confirmedAt,
        string suffix) => new()
        {
            OperationalProjectId = projectId,
            Code = $"HSE-KPI-{suffix}",
            OfflineClientId = Guid.NewGuid().ToString(),
            OccurredAt = confirmedAt.AddHours(-1),
            Location = "Site",
            Category = "General",
            Severity = HseViolationSeverity.Low,
            Description = suffix,
            ResponsibleSiteUserId = responsibleUserId,
            Status = status,
            ConfirmedAt = confirmedAt,
            ConfirmedByUserId = responsibleUserId,
            CreatedByUserId = responsibleUserId,
            UpdatedByUserId = responsibleUserId,
        };

    private static PaymentRequest PaidRequest(
        string code,
        string invoice,
        int contractId,
        int vendorId,
        int accountantId,
        DateTime validatedAt,
        DateTime paidAt) => new()
        {
            Code = code,
            ContractId = contractId,
            VendorId = vendorId,
            SupplierInvoiceNumber = invoice,
            InvoiceDate = DateOnly.FromDateTime(validatedAt),
            InvoiceAmount = 100m,
            Status = PaymentRequestStatus.Paid,
            ReceivedAt = validatedAt.AddDays(-1),
            ValidatedAt = validatedAt,
            AssignedAccountantUserId = accountantId,
            PaidAt = paidAt,
            PaidByUserId = accountantId,
            CreatedByUserId = accountantId,
        };

    private static AccountingCorrection Correction(
        string code,
        int projectId,
        int periodId,
        int accountantId,
        AccountingCorrectionStatus status,
        DateTime approvedAt) => new()
        {
            Code = code,
            OperationalProjectId = projectId,
            AccountingPeriodId = periodId,
            SourceEntityType = nameof(Contract),
            SourceEntityId = 1,
            ReasonCode = "AMOUNT_ERROR",
            OriginalValue = 100m,
            CorrectedValue = 90m,
            ResponsibleAccountantUserId = accountantId,
            RecordedByUserId = accountantId,
            Status = status,
            ApprovedAt = approvedAt,
        };

    private async Task<int> SeedSalesDataAsync()
    {
        return await WithDbAsync(async db =>
        {
            var saleUserId = await db.Users
                .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALE"])
                .Select(user => user.Id)
                .SingleAsync();
            var customer = new Customer
            {
                Type = CustomerType.Individual,
                Name = "KPI Customer",
                SourceCode = "marketing",
                OwnerUserId = saleUserId,
            };
            db.Customers.Add(customer);
            db.Leads.AddRange(
                new Lead
                {
                    Name = "Converted KPI Lead",
                    SourceCode = "marketing",
                    OwnerUserId = saleUserId,
                    Status = LeadStatus.Converted,
                    ConvertedAt = new DateTime(2026, 8, 15, 1, 0, 0, DateTimeKind.Utc),
                    CreatedAt = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc),
                },
                new Lead
                {
                    Name = "Open KPI Lead",
                    SourceCode = "marketing",
                    OwnerUserId = saleUserId,
                    Status = LeadStatus.Contacted,
                    CreatedAt = new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc),
                },
                new Lead
                {
                    Name = "Next local month KPI Lead",
                    SourceCode = "marketing",
                    OwnerUserId = saleUserId,
                    Status = LeadStatus.Converted,
                    ConvertedAt = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
                    CreatedAt = new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc),
                });
            await db.SaveChangesAsync();
            var scoreableLeads = await db.Leads
                .Where(item => item.OwnerUserId == saleUserId &&
                    item.CreatedAt >= new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc) &&
                    item.CreatedAt < new DateTime(2026, 8, 31, 17, 0, 0, DateTimeKind.Utc))
                .ToListAsync();
            foreach (var lead in scoreableLeads)
            {
                db.LeadActivities.Add(new LeadActivity
                {
                    LeadId = lead.Id,
                    Type = LeadActivityType.Call,
                    Content = "KPI response",
                    CreatedByUserId = saleUserId,
                    CreatedAt = lead.CreatedAt.AddHours(2),
                });
            }
            await db.SaveChangesAsync();
            var vendor = new Vendor
            {
                VendorCode = $"KPI-{Guid.NewGuid():N}"[..20],
                CompanyName = "KPI downstream vendor",
                VendorType = VendorType.SubContractor,
                CreatedByUserId = saleUserId,
            };
            db.Vendors.Add(vendor);
            await db.SaveChangesAsync();
            db.Contracts.AddRange(new Contract
            {
                ContractNumber = $"HD-KPI-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                OwnerUserId = saleUserId,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Design,
                Status = ContractStatus.Signed,
                SignedDate = new DateTime(2026, 8, 20, 1, 0, 0, DateTimeKind.Utc),
                Value = 100_000_000m,
            }, new Contract
            {
                ContractNumber = $"HD-KPI-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                VendorId = vendor.Id,
                OwnerUserId = saleUserId,
                Direction = ContractDirection.Downstream,
                Type = ContractType.Subcontract,
                Status = ContractStatus.Signed,
                SignedDate = new DateTime(2026, 8, 21, 1, 0, 0, DateTimeKind.Utc),
                Value = 900_000_000m,
            });
            await db.SaveChangesAsync();
            return saleUserId;
        });
    }

    private async Task<HttpResponseMessage> SendWithIdempotencyAsync(HttpMethod method, string url, object payload)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}
