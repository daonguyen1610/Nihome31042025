using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class MaterialAlertsControllerTests : IntegrationTestBase
{
    public MaterialAlertsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Evaluate_AcknowledgesResolvesAndReopensProjectAlerts()
    {
        var fixture = await CreateAlertFixtureAsync();
        await LoginAsRoleAsync("SUPER_ADMIN");
        var revisionCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new { itemCode = "ALERT-OVER", description = "Over allowance material", unit = "kg", approvedQuantity = 100m, budgetUnitPrice = 10m },
                    new { itemCode = "ALERT-SHORT", description = "Short supply material", unit = "kg", approvedQuantity = 100m, budgetUnitPrice = 10m },
                },
            });
        revisionCreated.StatusCode.Should().Be(HttpStatusCode.Created, await revisionCreated.Content.ReadAsStringAsync());
        var revisionDraft = await ReadJsonAsync(revisionCreated);
        var revisionSubmitted = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/boq-revisions/{revisionDraft.GetProperty("id").GetInt32()}/submit",
            new { }, revisionDraft.GetProperty("rowVersion").GetString());
        revisionSubmitted.StatusCode.Should().Be(HttpStatusCode.OK, await revisionSubmitted.Content.ReadAsStringAsync());
        var submitted = await ReadJsonAsync(revisionSubmitted);
        var revisionApproved = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/boq-revisions/{revisionDraft.GetProperty("id").GetInt32()}/decision",
            new { approved = true }, submitted.GetProperty("rowVersion").GetString());
        revisionApproved.StatusCode.Should().Be(HttpStatusCode.OK, await revisionApproved.Content.ReadAsStringAsync());

        await LoginAsRoleAsync("PM");
        var detectedList = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts");
        detectedList.StatusCode.Should().Be(HttpStatusCode.OK);
        var alerts = (await ReadJsonAsync(detectedList)).GetProperty("items").EnumerateArray().ToList();
        alerts.Should().HaveCount(2);
        alerts.Select(item => item.GetProperty("code").GetString()).Should().OnlyHaveUniqueItems();
        var overBoq = alerts.Single(item => item.GetProperty("type").GetString() == "OverBoq");
        var shortage = alerts.Single(item => item.GetProperty("type").GetString() == "Shortage");
        overBoq.GetProperty("severity").GetString().Should().Be("Critical");
        overBoq.GetProperty("issuedQuantity").GetDecimal().Should().Be(110m);
        overBoq.GetProperty("boqAllowance").GetDecimal().Should().Be(100m);
        overBoq.GetProperty("varianceQuantity").GetDecimal().Should().Be(10m);
        overBoq.GetProperty("assignedToUserId").GetInt32().Should().Be(fixture.ProjectManagerUserId);
        shortage.GetProperty("requiredQuantity").GetDecimal().Should().Be(50m);
        shortage.GetProperty("onHandQuantity").GetDecimal().Should().Be(0m);
        shortage.GetProperty("varianceQuantity").GetDecimal().Should().Be(50m);
        shortage.GetProperty("assignedToUserId").GetInt32().Should().Be(fixture.ProcurementUserId);
        shortage.GetRawText().Should().NotContain("budgetUnitPrice").And.NotContain("negotiatedUnitPrice").And.NotContain("\"value\"");
        var alertIds = alerts.Select(item => item.GetProperty("id").GetInt32()).ToList();
        (await WithDbAsync(db => db.Notifications.AsNoTracking().CountAsync(item =>
            item.TemplateCode == "procurement.material-alert.detected" &&
            item.RefEntityType == "MaterialAlert" &&
            item.RefEntityId.HasValue && alertIds.Contains(item.RefEntityId.Value) &&
            (item.UserId == fixture.ProjectManagerUserId || item.UserId == fixture.ProcurementUserId))))
            .Should().Be(3);

        var list = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts?status=Open&severity=Critical&sortBy=variance&sortDirection=desc");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await ReadJsonAsync(list);
        listJson.GetProperty("total").GetInt32().Should().Be(2);
        listJson.GetProperty("statusCounts").GetProperty("Open").GetInt32().Should().Be(2);

        var shortageId = shortage.GetProperty("id").GetInt32();
        var shortageRowVersion = shortage.GetProperty("rowVersion").GetString();
        await LoginAsRoleAsync("PROCUREMENT");
        var acknowledged = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{shortageId}/acknowledge",
            new { note = "Đã đặt lịch giao bổ sung." }, rowVersion: shortageRowVersion);
        acknowledged.StatusCode.Should().Be(HttpStatusCode.OK, await acknowledged.Content.ReadAsStringAsync());
        var acknowledgedJson = await ReadJsonAsync(acknowledged);
        acknowledgedJson.GetProperty("status").GetString().Should().Be("Acknowledged");
        acknowledgedJson.GetProperty("acknowledgementNote").GetString().Should().Be("Đã đặt lịch giao bổ sung.");
        acknowledgedJson.GetProperty("events").EnumerateArray().Should().Contain(item =>
            item.GetProperty("type").GetString() == "Acknowledged" &&
            item.GetProperty("changedByUserId").GetInt32() == fixture.ProcurementUserId);

        async Task<System.Text.Json.JsonElement> CreateAndPostReceipt(decimal quantity)
        {
            var created = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts",
                new
                {
                    inspectedAt = DateTime.UtcNow,
                    receivedByUserId = fixture.WarehouseUserId,
                    lines = new[] { new { materialRequestLineId = fixture.ShortageRequestLineId, receivedQuantity = quantity } },
                });
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var draft = await ReadJsonAsync(created);
            var posted = await SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/receipts/{draft.GetProperty("id").GetInt32()}/post",
                new { rowVersion = draft.GetProperty("rowVersion").GetString() });
            posted.StatusCode.Should().Be(HttpStatusCode.OK, await posted.Content.ReadAsStringAsync());
            return await ReadJsonAsync(posted);
        }

        await LoginAsRoleAsync("WAREHOUSE");
        await CreateAndPostReceipt(20m);
        await LoginAsRoleAsync("PM");
        var partiallySupplied = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{shortageId}");
        partiallySupplied.StatusCode.Should().Be(HttpStatusCode.OK);
        var partialShortage = await ReadJsonAsync(partiallySupplied);
        partialShortage.GetProperty("status").GetString().Should().Be("Acknowledged");
        partialShortage.GetProperty("onHandQuantity").GetDecimal().Should().Be(20m);
        partialShortage.GetProperty("varianceQuantity").GetDecimal().Should().Be(30m);
        partialShortage.GetProperty("events").EnumerateArray().Should().Contain(item =>
            item.GetProperty("type").GetString() == "MetricsUpdated");

        await LoginAsRoleAsync("WAREHOUSE");
        var finalReceipt = await CreateAndPostReceipt(30m);
        await LoginAsRoleAsync("PM");
        var automaticallyResolved = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{shortageId}");
        automaticallyResolved.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedShortage = await ReadJsonAsync(automaticallyResolved);
        resolvedShortage.GetProperty("status").GetString().Should().Be("Resolved");
        resolvedShortage.GetProperty("events").EnumerateArray().Should().Contain(item =>
            item.GetProperty("type").GetString() == "AutoResolved");

        await WithDbAsync(async db =>
        {
            var issue = await db.WarehouseIssues.Include(item => item.Lines)
                .SingleAsync(item => item.Id == fixture.OverIssueId);
            db.WarehouseIssues.Add(new WarehouseIssue
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "WI-ALERT-REV",
                Status = WarehouseLedgerStatus.Posted,
                ReversalOfIssueId = issue.Id,
                ResponsibleSiteUserId = fixture.ProjectManagerUserId,
                IssuedByUserId = fixture.WarehouseUserId,
                IssuedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                PostedByUserId = fixture.WarehouseUserId,
                ReversalReason = "Imported quantity corrected",
                Lines = issue.Lines.Select(line => new WarehouseIssueLine
                {
                    ProjectBoqLineId = line.ProjectBoqLineId,
                    IssuedQuantity = line.IssuedQuantity,
                }).ToList(),
            });
            issue.Status = WarehouseLedgerStatus.Reversed;
            await db.SaveChangesAsync();
        });

        var resolved = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { });
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolvedRows = (await ReadJsonAsync(resolved)).EnumerateArray().ToList();
        resolvedRows.Should().OnlyContain(item => item.GetProperty("status").GetString() == "Resolved");
        resolvedRows.Should().OnlyContain(item => item.GetProperty("events").EnumerateArray().Any(entry =>
            entry.GetProperty("type").GetString() == "AutoResolved"));

        await WithDbAsync(async db =>
        {
            var reversal = await db.WarehouseIssues.SingleAsync(item => item.ReversalOfIssueId == fixture.OverIssueId);
            db.WarehouseIssues.Remove(reversal);
            var original = await db.WarehouseIssues.SingleAsync(item => item.Id == fixture.OverIssueId);
            original.Status = WarehouseLedgerStatus.Posted;
            var finalReceiptId = finalReceipt.GetProperty("id").GetInt32();
            var receipt = await db.WarehouseReceipts.SingleAsync(item => item.Id == finalReceiptId);
            db.WarehouseReceipts.Remove(receipt);
            await db.SaveChangesAsync();
        });

        var reopened = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopenedRows = (await ReadJsonAsync(reopened)).EnumerateArray().ToList();
        reopenedRows.Should().OnlyContain(item => item.GetProperty("status").GetString() == "Open");
        reopenedRows.Should().OnlyContain(item => item.GetProperty("events").EnumerateArray().Any(entry =>
            entry.GetProperty("type").GetString() == "Reopened"));
    }

    [Fact]
    public async Task Alerts_EnforcePermissionAndProjectScope()
    {
        var fixture = await CreateAlertFixtureAsync();
        await LoginAsRoleAsync("DESIGN");
        (await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await LoginAsRoleAsync("WAREHOUSE");
        (await Client.GetAsync($"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var inaccessibleProjectId = await WithDbAsync(async db =>
        {
            var customer = new Customer { Type = CustomerType.Individual, Name = "Alert isolation customer", SourceCode = "referral" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = $"PJ-ALERT-OTHER-{Guid.NewGuid():N}"[..30],
                Name = "Alert Isolation Project",
                CustomerId = customer.Id,
                Status = OperationalProjectStatus.Active,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
        (await Client.GetAsync($"/api/operational-projects/{inaccessibleProjectId}/procurement/material-alerts"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Evaluate_ConcurrentRequests_CreateOneProjectionPerCondition()
    {
        var fixture = await CreateAlertFixtureAsync();
        await LoginAsRoleAsync("PM");

        var responses = await Task.WhenAll(
            SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { }),
            SendAsync(HttpMethod.Post,
                $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { }));

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        var alerts = await WithDbAsync(db => db.MaterialAlerts.AsNoTracking()
            .Where(item => item.OperationalProjectId == fixture.ProjectId)
            .OrderBy(item => item.Code)
            .ToListAsync());
        alerts.Should().HaveCount(2);
        alerts.Select(item => item.Code).Should().OnlyHaveUniqueItems();
        (await WithDbAsync(db => db.MaterialAlertEvents.AsNoTracking().CountAsync(item =>
            alerts.Select(alert => alert.Id).Contains(item.MaterialAlertId) &&
            item.Type == MaterialAlertEventType.Detected))).Should().Be(2);
        (await WithDbAsync(db => db.Notifications.AsNoTracking().CountAsync(item =>
            item.TemplateCode == "procurement.material-alert.detected" &&
            item.RefEntityType == "MaterialAlert" &&
            alerts.Select(alert => alert.Id).Contains(item.RefEntityId!.Value)))).Should().Be(3);
    }

    [Fact]
    public async Task IssuePostAndReverse_AutomaticallyDetectAndResolveOverBoq()
    {
        var fixture = await CreateAlertFixtureAsync();
        var overBoqLineId = await WithDbAsync<int>(async db =>
        {
            var seededIssue = await db.WarehouseIssues.SingleAsync(item => item.Id == fixture.OverIssueId);
            db.WarehouseIssues.Remove(seededIssue);
            var lineId = await db.ProjectBoqLines.Where(item =>
                    item.ProjectBoqRevision.OperationalProjectId == fixture.ProjectId &&
                    item.ItemCode == "ALERT-OVER")
                .Select(item => item.Id).SingleAsync();
            var request = new MaterialRequest
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "MR-ALERT-OVER-STOCK",
                Status = MaterialRequestStatus.Fulfilled,
                SiteRequesterUserId = fixture.ProjectManagerUserId,
                ResponsibleSiteUserId = fixture.ProjectManagerUserId,
                AssignedProcurementUserId = fixture.ProcurementUserId,
                RequiredAt = DateTime.UtcNow.AddDays(2),
                ApprovedAt = DateTime.UtcNow,
                FulfilledAt = DateTime.UtcNow,
                Lines = [new MaterialRequestLine { ProjectBoqLineId = lineId, RequestedQuantity = 150m }],
            };
            db.MaterialRequests.Add(request);
            await db.SaveChangesAsync();
            db.WarehouseReceipts.Add(new WarehouseReceipt
            {
                OperationalProjectId = fixture.ProjectId,
                Code = "WR-ALERT-OVER-STOCK",
                Status = WarehouseLedgerStatus.Posted,
                ReceivedByUserId = fixture.WarehouseUserId,
                InspectedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow,
                PostedByUserId = fixture.WarehouseUserId,
                Lines = [new WarehouseReceiptLine { MaterialRequestLineId = request.Lines.Single().Id, ReceivedQuantity = 150m }],
            });
            await db.SaveChangesAsync();
            return lineId;
        });

        await LoginAsRoleAsync("WAREHOUSE");
        var created = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues",
            new
            {
                issuedAt = DateTime.UtcNow,
                responsibleSiteUserId = fixture.ProjectManagerUserId,
                issuedByUserId = fixture.WarehouseUserId,
                workItemCode = "ALERT-CREW",
                lines = new[] { new { projectBoqLineId = overBoqLineId, issuedQuantity = 110m } },
            });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var draft = await ReadJsonAsync(created);
        var postedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{draft.GetProperty("id").GetInt32()}/post",
            new { rowVersion = draft.GetProperty("rowVersion").GetString() });
        postedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await postedResponse.Content.ReadAsStringAsync());
        var posted = await ReadJsonAsync(postedResponse);

        await LoginAsRoleAsync("PM");
        var detectedResponse = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts?type=OverBoq");
        detectedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detected = (await ReadJsonAsync(detectedResponse)).GetProperty("items").EnumerateArray().Single();
        detected.GetProperty("status").GetString().Should().Be("Open");
        detected.GetProperty("varianceQuantity").GetDecimal().Should().Be(10m);
        detected.GetProperty("sourceEntityId").GetInt32().Should().Be(posted.GetProperty("id").GetInt32());

        await LoginAsRoleAsync("WAREHOUSE");
        var reversed = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/issues/{posted.GetProperty("id").GetInt32()}/reverse",
            new { rowVersion = posted.GetProperty("rowVersion").GetString(), reason = "Issue quantity corrected" });
        reversed.StatusCode.Should().Be(HttpStatusCode.OK, await reversed.Content.ReadAsStringAsync());

        await LoginAsRoleAsync("PM");
        var resolved = await Client.GetAsync(
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{detected.GetProperty("id").GetInt32()}");
        resolved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(resolved)).GetProperty("status").GetString().Should().Be("Resolved");
    }

    [Fact]
    public async Task Acknowledge_EnforcesManageOwnershipAndConcurrency()
    {
        var fixture = await CreateAlertFixtureAsync();
        await LoginAsRoleAsync("PM");
        var evaluated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/evaluate", new { });
        evaluated.StatusCode.Should().Be(HttpStatusCode.OK);
        var shortage = (await ReadJsonAsync(evaluated)).EnumerateArray()
            .Single(item => item.GetProperty("type").GetString() == "Shortage");
        var alertId = shortage.GetProperty("id").GetInt32();
        var rowVersion = shortage.GetProperty("rowVersion").GetString();

        await LoginAsRoleAsync("WAREHOUSE");
        (await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{alertId}/acknowledge",
            new { note = "Warehouse cannot acknowledge this alert.", rowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await LoginAsRoleAsync("PM");
        var staleBytes = Convert.FromBase64String(rowVersion!);
        staleBytes[0] ^= 0xFF;
        var staleRowVersion = Convert.ToBase64String(staleBytes);
        (await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{alertId}/acknowledge",
            new { note = "Project manager owns the response.", rowVersion = staleRowVersion }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.MaterialAlerts.AsNoTracking()
            .Where(item => item.Id == alertId).Select(item => item.Status).SingleAsync()))
            .Should().Be(MaterialAlertStatus.Open);

        var acknowledged = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{fixture.ProjectId}/procurement/material-alerts/{alertId}/acknowledge",
            new { note = "Project manager owns the response.", rowVersion });
        acknowledged.StatusCode.Should().Be(HttpStatusCode.OK, await acknowledged.Content.ReadAsStringAsync());
        var response = await ReadJsonAsync(acknowledged);
        response.GetProperty("status").GetString().Should().Be("Acknowledged");
        response.GetProperty("acknowledgedByUserId").GetInt32().Should().Be(fixture.ProjectManagerUserId);
        response.GetProperty("assignedToUserId").GetInt32().Should().Be(fixture.ProcurementUserId);
    }

    private async Task<AlertFixture> CreateAlertFixtureAsync() => await WithDbAsync(async db =>
    {
        var pmId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync();
        var procurementId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
            .Select(user => user.Id).SingleAsync();
        var warehouseId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["WAREHOUSE"])
            .Select(user => user.Id).SingleAsync();
        var customer = new Customer { Type = CustomerType.Individual, Name = "Material Alert Customer", SourceCode = "referral" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-ALERT-{Guid.NewGuid():N}"[..30],
            Name = "Material Alert Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = pmId,
            Status = OperationalProjectStatus.Active,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        db.OperationalProjectMembers.AddRange(
            NewMember(project.Id, procurementId, "PROCUREMENT", pmId),
            NewMember(project.Id, warehouseId, "WAREHOUSE", pmId));
        var boq = new ProjectBoqRevision
        {
            OperationalProjectId = project.Id,
            RevisionNumber = 1,
            Status = ProjectBoqRevisionStatus.Approved,
            ApprovedAt = DateTime.UtcNow,
            PreparedByUserId = pmId,
            Lines =
            [
                new ProjectBoqLine { ItemCode = "ALERT-OVER", Description = "Over allowance material", Unit = "kg", ApprovedQuantity = 100m, BudgetUnitPrice = 10m, Amount = 1_000m, SortOrder = 0 },
                new ProjectBoqLine { ItemCode = "ALERT-SHORT", Description = "Short supply material", Unit = "kg", ApprovedQuantity = 100m, BudgetUnitPrice = 10m, Amount = 1_000m, SortOrder = 1 },
            ],
        };
        db.ProjectBoqRevisions.Add(boq);
        await db.SaveChangesAsync();
        var shortageRequest = new MaterialRequest
        {
            OperationalProjectId = project.Id,
            Code = "MR-ALERT-SHORT",
            Status = MaterialRequestStatus.Approved,
            SiteRequesterUserId = pmId,
            ResponsibleSiteUserId = pmId,
            AssignedProcurementUserId = procurementId,
            RequiredAt = DateTime.UtcNow.AddDays(2),
            ApprovedAt = DateTime.UtcNow,
            Lines = [new MaterialRequestLine { ProjectBoqLineId = boq.Lines[1].Id, RequestedQuantity = 50m }],
        };
        db.MaterialRequests.Add(shortageRequest);
        var overIssue = new WarehouseIssue
        {
            OperationalProjectId = project.Id,
            Code = "WI-ALERT-OVER",
            Status = WarehouseLedgerStatus.Posted,
            ResponsibleSiteUserId = pmId,
            IssuedByUserId = warehouseId,
            IssuedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            PostedByUserId = warehouseId,
            WorkItemCode = "ALERT-CREW",
            Lines = [new WarehouseIssueLine { ProjectBoqLineId = boq.Lines[0].Id, IssuedQuantity = 110m }],
        };
        db.WarehouseIssues.Add(overIssue);
        await db.SaveChangesAsync();
        return new AlertFixture(project.Id, pmId, procurementId, warehouseId,
            shortageRequest.Lines.Single().Id, overIssue.Id);
    });

    private static OperationalProjectMember NewMember(int projectId, int userId, string position, int actorId) => new()
    {
        OperationalProjectId = projectId,
        UserId = userId,
        Position = position,
        StartedAt = DateTime.UtcNow,
        CreatedByUserId = actorId,
        UpdatedByUserId = actorId,
    };

    private async Task LoginAsRoleAsync(string role)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object body,
        string? rowVersion = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (!string.IsNullOrWhiteSpace(rowVersion)) request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        return await Client.SendAsync(request);
    }

    private sealed record AlertFixture(
        int ProjectId,
        int ProjectManagerUserId,
        int ProcurementUserId,
        int WarehouseUserId,
        int ShortageRequestLineId,
        int OverIssueId);
}
