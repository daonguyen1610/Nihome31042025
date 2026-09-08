using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ProcurementBusinessPipelineTests : IntegrationTestBase
{
    private readonly string correlationId = $"nih169-pipeline-{Guid.NewGuid():N}";

    public ProcurementBusinessPipelineTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ProcurementPipeline_CompletesDemandToWarehouseAndKpiEvidence()
    {
        var context = await CreatePipelineContextAsync();
        await LoginAsRoleAsync("SUPER_ADMIN");
        var boqKey = Guid.NewGuid().ToString();
        var boqCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new
                    {
                        itemCode = "PIPE-CEMENT",
                        description = "Pipeline Portland cement",
                        unit = "kg",
                        approvedQuantity = 100m,
                        budgetUnitPrice = 100m,
                    },
                },
            }, boqKey);
        boqCreated.StatusCode.Should().Be(HttpStatusCode.Created, await boqCreated.Content.ReadAsStringAsync());
        var boqDraft = await ReadJsonAsync(boqCreated);
        var boqId = boqDraft.GetProperty("id").GetInt32();
        var boqLineId = boqDraft.GetProperty("lines")[0].GetProperty("id").GetInt32();

        var boqReplay = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new
                    {
                        itemCode = "PIPE-CEMENT",
                        description = "Pipeline Portland cement",
                        unit = "kg",
                        approvedQuantity = 100m,
                        budgetUnitPrice = 100m,
                    },
                },
            }, boqKey);
        boqReplay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(boqReplay)).GetProperty("id").GetInt32().Should().Be(boqId);
        var boqConflict = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/boq-revisions",
            new
            {
                currency = "VND",
                lines = new[]
                {
                    new
                    {
                        itemCode = "PIPE-CEMENT",
                        description = "Conflicting retry payload",
                        unit = "kg",
                        approvedQuantity = 99m,
                        budgetUnitPrice = 100m,
                    },
                },
            }, boqKey);
        boqConflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item =>
            item.OperationalProjectId == context.ProjectId))).Should().Be(1);

        var boqSubmitted = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/boq-revisions/{boqId}/submit",
            new { }, rowVersion: boqDraft.GetProperty("rowVersion").GetString());
        boqSubmitted.StatusCode.Should().Be(HttpStatusCode.OK, await boqSubmitted.Content.ReadAsStringAsync());
        var submittedBoq = await ReadJsonAsync(boqSubmitted);
        var boqApproved = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/boq-revisions/{boqId}/decision",
            new { approved = true }, rowVersion: submittedBoq.GetProperty("rowVersion").GetString());
        boqApproved.StatusCode.Should().Be(HttpStatusCode.OK, await boqApproved.Content.ReadAsStringAsync());
        (await ReadJsonAsync(boqApproved)).GetProperty("status").GetString().Should().Be("Approved");

        var contractCreated = await SendAsync(HttpMethod.Post, "/api/contracts", new
        {
            customerId = context.CustomerId,
            vendorId = context.VendorId,
            operationalProjectId = context.ProjectId,
            ownerUserId = context.ProcurementUserId,
            direction = "Downstream",
            type = "Supply",
            status = "Draft",
            value = 9_000m,
            scopeOfWork = "Supply pipeline cement",
        });
        contractCreated.StatusCode.Should().Be(HttpStatusCode.Created, await contractCreated.Content.ReadAsStringAsync());
        var contractDraft = await ReadJsonAsync(contractCreated);
        var contractId = contractDraft.GetProperty("id").GetInt32();

        await LoginAsRoleAsync("PROCUREMENT");
        var contractLineCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/contract-lines",
            new
            {
                contractId,
                projectBoqLineId = boqLineId,
                procurementOwnerUserId = context.ProcurementUserId,
                quantity = 100m,
                negotiatedUnitPrice = 90m,
            });
        contractLineCreated.StatusCode.Should().Be(HttpStatusCode.Created, await contractLineCreated.Content.ReadAsStringAsync());
        var contractLine = await ReadJsonAsync(contractLineCreated);
        var contractLineId = contractLine.GetProperty("id").GetInt32();

        await LoginAsRoleAsync("SUPER_ADMIN");
        var contractSigned = await SendAsync(HttpMethod.Post, $"/api/contracts/{contractId}/transition",
            new { newStatus = "Signed" }, rowVersion: contractDraft.GetProperty("rowVersion").GetString());
        contractSigned.StatusCode.Should().Be(HttpStatusCode.OK, await contractSigned.Content.ReadAsStringAsync());
        var signedContract = await ReadJsonAsync(contractSigned);
        signedContract.GetProperty("status").GetString().Should().Be("Signed");

        await LoginAsRoleAsync("PM");
        var requestCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/material-requests",
            new
            {
                responsibleSiteUserId = context.ProjectManagerUserId,
                assignedProcurementUserId = context.ProcurementUserId,
                requiredAt = DateTime.UtcNow.AddDays(3),
                note = "Full procurement pipeline",
                lines = new[] { new { projectBoqLineId = boqLineId, requestedQuantity = 100m } },
            });
        requestCreated.StatusCode.Should().Be(HttpStatusCode.Created, await requestCreated.Content.ReadAsStringAsync());
        var requestDraft = await ReadJsonAsync(requestCreated);
        var requestId = requestDraft.GetProperty("id").GetInt32();
        var requestLineId = requestDraft.GetProperty("lines")[0].GetProperty("id").GetInt32();
        var requestSubmitted = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/material-requests/{requestId}/submit",
            new { }, rowVersion: requestDraft.GetProperty("rowVersion").GetString());
        requestSubmitted.StatusCode.Should().Be(HttpStatusCode.OK, await requestSubmitted.Content.ReadAsStringAsync());
        var submittedRequest = await ReadJsonAsync(requestSubmitted);

        await LoginAsRoleAsync("PROCUREMENT");
        var requestApproved = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/material-requests/{requestId}/decision",
            new { approved = true }, rowVersion: submittedRequest.GetProperty("rowVersion").GetString());
        requestApproved.StatusCode.Should().Be(HttpStatusCode.OK, await requestApproved.Content.ReadAsStringAsync());
        (await ReadJsonAsync(requestApproved)).GetProperty("status").GetString().Should().Be("Approved");
        (await WithDbAsync(db => db.Notifications.AsNoTracking().AnyAsync(item =>
            item.UserId == context.ProjectManagerUserId &&
            item.TemplateCode == "procurement.material-request.approved" &&
            item.RefEntityId == requestId))).Should().BeTrue();

        await LoginAsRoleAsync("WAREHOUSE");
        var receiptKey = Guid.NewGuid().ToString();
        var receiptCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts",
            new
            {
                inspectedAt = DateTime.UtcNow,
                receivedByUserId = context.WarehouseUserId,
                lines = new[]
                {
                    new
                    {
                        materialRequestLineId = requestLineId,
                        contractLineId,
                        receivedQuantity = 100m,
                    },
                },
            }, receiptKey);
        receiptCreated.StatusCode.Should().Be(HttpStatusCode.Created, await receiptCreated.Content.ReadAsStringAsync());
        var receiptDraft = await ReadJsonAsync(receiptCreated);
        var receiptId = receiptDraft.GetProperty("id").GetInt32();
        var receiptReplay = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts",
            new
            {
                inspectedAt = receiptDraft.GetProperty("inspectedAt").GetDateTime(),
                receivedByUserId = context.WarehouseUserId,
                lines = new[]
                {
                    new
                    {
                        materialRequestLineId = requestLineId,
                        contractLineId,
                        receivedQuantity = 100m,
                    },
                },
            }, receiptKey);
        receiptReplay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(receiptReplay)).GetProperty("id").GetInt32().Should().Be(receiptId);
        (await WithDbAsync(db => db.WarehouseReceipts.CountAsync(item =>
            item.OperationalProjectId == context.ProjectId))).Should().Be(1);

        var receiptPostedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts/{receiptId}/post",
            new { }, rowVersion: receiptDraft.GetProperty("rowVersion").GetString());
        receiptPostedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await receiptPostedResponse.Content.ReadAsStringAsync());
        var receiptPosted = await ReadJsonAsync(receiptPostedResponse);
        var fulfilledRequest = await WithDbAsync(db => db.MaterialRequests.AsNoTracking()
            .SingleAsync(item => item.Id == requestId));
        fulfilledRequest.Status.Should().Be(MaterialRequestStatus.Fulfilled);
        fulfilledRequest.FulfilledAt.Should().NotBeNull();

        var receiptDetail = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts/{receiptId}"));
        receiptDetail.GetProperty("contracts").GetArrayLength().Should().Be(1);
        receiptDetail.GetProperty("lines")[0].GetProperty("materialRequestCode").GetString()
            .Should().Be(requestDraft.GetProperty("code").GetString());
        var warehousePayload = receiptDetail.GetRawText();
        warehousePayload.Should().NotContain("budgetUnitPrice").And.NotContain("negotiatedUnitPrice").And.NotContain("\"value\"");

        var issueCreated = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/issues",
            new
            {
                issuedAt = DateTime.UtcNow,
                responsibleSiteUserId = context.ProjectManagerUserId,
                issuedByUserId = context.WarehouseUserId,
                workItemCode = "PIPELINE-CREW-A",
                lines = new[] { new { projectBoqLineId = boqLineId, issuedQuantity = 80m } },
            });
        issueCreated.StatusCode.Should().Be(HttpStatusCode.Created, await issueCreated.Content.ReadAsStringAsync());
        var issueDraft = await ReadJsonAsync(issueCreated);
        var issueId = issueDraft.GetProperty("id").GetInt32();
        var issuePostedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/issues/{issueId}/post",
            new { }, rowVersion: issueDraft.GetProperty("rowVersion").GetString());
        issuePostedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await issuePostedResponse.Content.ReadAsStringAsync());
        var issuePosted = await ReadJsonAsync(issuePostedResponse);

        var warehouseList = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{context.ProjectId}/procurement/warehouse-transactions"));
        var stock = warehouseList.GetProperty("stock").EnumerateArray()
            .Single(item => item.GetProperty("itemCode").GetString() == "PIPE-CEMENT");
        stock.GetProperty("receivedQuantity").GetDecimal().Should().Be(100m);
        stock.GetProperty("issuedQuantity").GetDecimal().Should().Be(80m);
        stock.GetProperty("onHandQuantity").GetDecimal().Should().Be(20m);
        (await Client.GetAsync(
            $"/api/operational-projects/{context.OtherProjectId}/procurement/warehouse-transactions"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await LoginAsRoleAsync("SUPER_ADMIN");
        var contractPeriod = LocalPeriod(signedContract.GetProperty("signedDate").GetDateTime());
        var deliveryPeriod = LocalPeriod(fulfilledRequest.FulfilledAt!.Value);
        var issuePeriod = LocalPeriod(issuePosted.GetProperty("postedAt").GetDateTime());
        var costKpi = await CalculateKpiAsync(contractPeriod.Year, contractPeriod.Month, context.ProcurementUserId);
        var costScore = Score(costKpi, "PROCUREMENT_COST");
        costScore.GetProperty("status").GetString().Should().Be("Available");
        costScore.GetProperty("rawValue").GetDecimal().Should().Be(10m);
        EvidenceIds(costScore).Should().ContainSingle().Which.Should().Be(contractLineId);
        var deliveryKpi = await CalculateKpiAsync(deliveryPeriod.Year, deliveryPeriod.Month, context.ProcurementUserId);
        var deliveryScore = Score(deliveryKpi, "PROCUREMENT_ON_TIME");
        deliveryScore.GetProperty("status").GetString().Should().Be("Available");
        deliveryScore.GetProperty("rawValue").GetDecimal().Should().BeGreaterThanOrEqualTo(0m).And.BeLessThan(1m);
        EvidenceIds(deliveryScore).Should().ContainSingle().Which.Should().Be(requestId);

        var siteKpi = await CalculateKpiAsync(issuePeriod.Year, issuePeriod.Month, context.ProjectManagerUserId);
        var wasteScore = Score(siteKpi, "SITE_MATERIAL_WASTE");
        wasteScore.GetProperty("status").GetString().Should().Be("Available");
        wasteScore.GetProperty("rawValue").GetDecimal().Should().Be(0m);
        EvidenceIds(wasteScore).Should().ContainSingle().Which.Should().Be(issueId);

        await LoginAsRoleAsync("WAREHOUSE");
        var blockedReceiptReverse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts/{receiptId}/reverse",
            new { reason = "Receipt correction" }, rowVersion: receiptPosted.GetProperty("rowVersion").GetString());
        blockedReceiptReverse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceipts.AsNoTracking().SingleAsync(item => item.Id == receiptId)))
            .Status.Should().Be(WarehouseLedgerStatus.Posted);

        var issueReversedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/issues/{issueId}/reverse",
            new { reason = "Wrong receiving crew" }, rowVersion: issuePosted.GetProperty("rowVersion").GetString());
        issueReversedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await issueReversedResponse.Content.ReadAsStringAsync());
        var issueReversalId = (await ReadJsonAsync(issueReversedResponse)).GetProperty("id").GetInt32();
        var receiptReversedResponse = await SendAsync(HttpMethod.Post,
            $"/api/operational-projects/{context.ProjectId}/procurement/receipts/{receiptId}/reverse",
            new { reason = "Receipt correction" }, rowVersion: receiptPosted.GetProperty("rowVersion").GetString());
        receiptReversedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await receiptReversedResponse.Content.ReadAsStringAsync());
        var receiptReversalId = (await ReadJsonAsync(receiptReversedResponse)).GetProperty("id").GetInt32();

        var correctedList = await ReadJsonAsync(await Client.GetAsync(
            $"/api/operational-projects/{context.ProjectId}/procurement/warehouse-transactions"));
        correctedList.GetProperty("stock").EnumerateArray()
            .Single(item => item.GetProperty("itemCode").GetString() == "PIPE-CEMENT")
            .GetProperty("onHandQuantity").GetDecimal().Should().Be(0m);
        (await WithDbAsync(db => db.MaterialRequests.AsNoTracking().SingleAsync(item => item.Id == requestId)))
            .Status.Should().Be(MaterialRequestStatus.Approved);

        await LoginAsRoleAsync("SUPER_ADMIN");
        var correctedCostKpi = await CalculateKpiAsync(contractPeriod.Year, contractPeriod.Month, context.ProcurementUserId);
        Score(correctedCostKpi, "PROCUREMENT_COST").GetProperty("rawValue").GetDecimal().Should().Be(10m);
        var correctedDeliveryKpi = await CalculateKpiAsync(deliveryPeriod.Year, deliveryPeriod.Month, context.ProcurementUserId);
        Score(correctedDeliveryKpi, "PROCUREMENT_ON_TIME").GetProperty("status").GetString().Should().Be("MissingData");
        var correctedSiteKpi = await CalculateKpiAsync(issuePeriod.Year, issuePeriod.Month, context.ProjectManagerUserId);
        var correctedWaste = Score(correctedSiteKpi, "SITE_MATERIAL_WASTE");
        correctedWaste.GetProperty("rawValue").GetDecimal().Should().Be(0m);
        EvidenceIds(correctedWaste).Should().BeEquivalentTo([issueId, issueReversalId]);

        await WaitForAuditAsync([
            ("proc.boq.create", "ProjectBoqRevision", boqId, context.SuperAdminUserId),
            ("proc.material-request.create", "MaterialRequest", requestId, context.ProjectManagerUserId),
            ("proc.material-request.decision", "MaterialRequest", requestId, context.ProcurementUserId),
            ("proc.contract-line.create", "ContractLine", contractLineId, context.ProcurementUserId),
            ("proc.receipt.create", "WarehouseReceipt", receiptId, context.WarehouseUserId),
            ("proc.receipt.post", "WarehouseReceipt", receiptId, context.WarehouseUserId),
            ("proc.issue.create", "WarehouseIssue", issueId, context.WarehouseUserId),
            ("proc.issue.post", "WarehouseIssue", issueId, context.WarehouseUserId),
            ("proc.issue.reverse", "WarehouseIssue", issueReversalId, context.WarehouseUserId),
            ("proc.receipt.reverse", "WarehouseReceipt", receiptReversalId, context.WarehouseUserId),
        ]);
    }

    private async Task<PipelineContext> CreatePipelineContextAsync() => await WithDbAsync(async db =>
    {
        KpiSeeder.Seed(db);
        var superAdminUserId = await db.Users.Where(user => user.PhoneNumber == "0335240370")
            .Select(user => user.Id).SingleAsync();
        var projectManagerUserId = await db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PM"])
            .Select(user => user.Id).SingleAsync();
        var procurementUserId = await db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
            .Select(user => user.Id).SingleAsync();
        var warehouseUserId = await db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["WAREHOUSE"])
            .Select(user => user.Id).SingleAsync();
        var customer = new Customer
        {
            Type = CustomerType.Individual,
            Name = "Pipeline Procurement Customer",
            SourceCode = "referral",
            RelationshipStatus = CustomerRelationshipStatus.Signed,
            OwnerUserId = projectManagerUserId,
        };
        var vendor = new Vendor
        {
            VendorCode = $"PIPE-{Guid.NewGuid():N}"[..18],
            CompanyName = "Pipeline Cement Supplier",
            VendorType = VendorType.Supplier,
            IsActive = true,
            CreatedByUserId = procurementUserId,
        };
        db.AddRange(customer, vendor);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-PIPE-{Guid.NewGuid():N}"[..28],
            Name = "Full Procurement Pipeline",
            CustomerId = customer.Id,
            ProjectManagerUserId = projectManagerUserId,
            Status = OperationalProjectStatus.Active,
        };
        var otherProject = new OperationalProject
        {
            Code = $"PJ-OTHER-{Guid.NewGuid():N}"[..28],
            Name = "Pipeline Isolation Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = superAdminUserId,
            Status = OperationalProjectStatus.Active,
        };
        db.OperationalProjects.AddRange(project, otherProject);
        await db.SaveChangesAsync();
        db.OperationalProjectMembers.AddRange(
            NewProjectMember(project.Id, procurementUserId, "PROCUREMENT", projectManagerUserId),
            NewProjectMember(project.Id, warehouseUserId, "WAREHOUSE", projectManagerUserId));
        var delivery = await db.KpiDefinitions.SingleAsync(item => item.Code == "PROCUREMENT_ON_TIME");
        delivery.TargetValue = 48m;
        var waste = await db.KpiDefinitions.SingleAsync(item => item.Code == "SITE_MATERIAL_WASTE");
        waste.TargetValue = 10m;
        await db.SaveChangesAsync();
        return new PipelineContext(
            customer.Id,
            vendor.Id,
            project.Id,
            otherProject.Id,
            superAdminUserId,
            projectManagerUserId,
            procurementUserId,
            warehouseUserId);
    });

    private static OperationalProjectMember NewProjectMember(
        int projectId,
        int userId,
        string position,
        int createdByUserId) => new()
        {
            OperationalProjectId = projectId,
            UserId = userId,
            Position = position,
            StartedAt = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
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
        string? idempotencyKey = null,
        string? rowVersion = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        request.Headers.Add("X-Correlation-Id", correlationId);
        if (!string.IsNullOrWhiteSpace(rowVersion)) request.Headers.TryAddWithoutValidation("If-Match", $"\"{rowVersion}\"");
        return await Client.SendAsync(request);
    }

    private async Task<JsonElement> CalculateKpiAsync(int year, int month, int userId)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/kpi/calculate", new { year, month, userId });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private static JsonElement Score(JsonElement dashboard, string code) =>
        dashboard.GetProperty("scores").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == code);

    private static List<int> EvidenceIds(JsonElement score)
    {
        using var evidence = JsonDocument.Parse(score.GetProperty("evidenceJson").GetString()!);
        return evidence.RootElement.GetProperty("RecordIds").EnumerateArray()
            .Select(item => item.GetInt32()).ToList();
    }

    private async Task WaitForAuditAsync(
        IReadOnlyCollection<(string Action, string ResourceType, int ResourceId, int ActorUserId)> expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (DateTime.UtcNow < deadline)
        {
            var rows = await WithDbAsync(db => db.AuditLogs.AsNoTracking()
                .Where(item => item.CorrelationId == correlationId && expected.Select(value => value.Action).Contains(item.Action))
                .Select(item => new { item.Action, item.ResourceType, item.ResourceId, item.ActorUserId })
                .ToListAsync());
            if (expected.All(value => rows.Any(item => item.Action == value.Action &&
                item.ResourceType == value.ResourceType && item.ResourceId == value.ResourceId.ToString() &&
                item.ActorUserId == value.ActorUserId))) return;
            await Task.Delay(100);
        }

        var persisted = await WithDbAsync(db => db.AuditLogs.AsNoTracking()
            .Where(item => item.CorrelationId == correlationId && expected.Select(value => value.Action).Contains(item.Action))
            .Select(item => $"{item.Action}:{item.ResourceType}:{item.ResourceId}:{item.ActorUserId}")
            .ToListAsync());
        persisted.Should().Contain(expected.Select(value =>
            $"{value.Action}:{value.ResourceType}:{value.ResourceId}:{value.ActorUserId}"));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    private static (int Year, int Month) LocalPeriod(DateTime utcTimestamp)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc), ResolveVietnamTimeZone());
        return (local.Year, local.Month);
    }

    private sealed record PipelineContext(
        int CustomerId,
        int VendorId,
        int ProjectId,
        int OtherProjectId,
        int SuperAdminUserId,
        int ProjectManagerUserId,
        int ProcurementUserId,
        int WarehouseUserId);
}
