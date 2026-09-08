using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class RfqProcurementPipelineTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ApprovedScope_ToAward_Delivery_Consumption_AndPaidInvoice_PreservesBusinessHandoffs()
    {
        var fixture = await SeedFoundationAsync();
        var procurement = $"/api/operational-projects/{fixture.ProjectId}/procurement";
        var rfqs = procurement + "/rfqs";

        // Every intermediate state is reached through a public endpoint using
        // its ordinary business role, rather than seeded as already approved.
        await LoginAsync("PROCUREMENT");
        var boq = await PostAsync(procurement + "/boq-revisions", new
        {
            currency = "VND",
            lines = new[] { new { itemCode = "CABLE-PIPELINE", description = "Factory power cable", unit = "m", approvedQuantity = 10m, budgetUnitPrice = 120m } },
        });
        var boqId = Id(boq);
        var boqLineId = Id(boq.GetProperty("lines")[0]);
        await RejectAsync(procurement + $"/boq-revisions/{boqId}/decision", new { approved = true, rowVersion = Version(boq) }, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.ProjectBoqRevisions.SingleAsync(x => x.Id == boqId))).Status.Should().Be(ProjectBoqRevisionStatus.Draft);
        boq = await MoveAsync(procurement + $"/boq-revisions/{boqId}", boq, "submit");
        await LoginAsync("BGD");
        boq = await PostAsync(procurement + $"/boq-revisions/{boqId}/decision", new { approved = true, rowVersion = Version(boq) });
        boq.GetProperty("status").GetString().Should().Be("Approved");
        boq.GetProperty("costTotal").GetDecimal().Should().Be(1200m);

        await LoginAsync("PROCUREMENT");
        var rfq = await PostAsync(rfqs, new
        {
            title = "Factory cable delivery",
            sourceBoqRevisionId = boqId,
            ownerUserId = fixture.ProcurementId,
            dueAt = DateTime.UtcNow.AddDays(7),
            vendorIds = new[] { fixture.VendorId, fixture.OtherVendorId },
            lines = new[] { new { projectBoqLineId = boqLineId, quantity = 10m } },
        });
        var rfqId = Id(rfq.GetProperty("header"));
        var rfqPath = rfqs + $"/{rfqId}";
        rfq = await PostAsync(rfqPath + "/issue", new { rowVersion = RfqVersion(rfq) });
        var rfqLineId = Id(rfq.GetProperty("lines")[0]);
        foreach (var vendor in new[] { (fixture.VendorId, Price: 100m), (fixture.OtherVendorId, Price: 110m) })
            rfq = await PostAsync(rfqPath + "/bids", new
            {
                vendorId = vendor.Item1,
                rowVersion = RfqVersion(rfq),
                leadTimeDays = 3,
                paymentTerms = "100% after inspected delivery",
                validUntil = DateTime.UtcNow.AddDays(14),
                lines = new[] { new { rfqLineId, unitPrice = vendor.Price } },
                documentIds = Array.Empty<long>(),
            });
        rfq = await PostAsync(rfqPath + "/evaluate", new { rowVersion = RfqVersion(rfq) });
        var winningBid = rfq.GetProperty("bids").EnumerateArray().Single(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId);
        winningBid.GetProperty("isLowest").GetBoolean().Should().BeTrue();
        var award = new { bidId = Id(winningBid), reason = "Complete scope, inspected delivery and best price", contractType = "Supply", rowVersion = RfqVersion(rfq) };
        await RejectAsync(rfqPath + "/award", award, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        await LoginAsync("BGD");
        rfq = await PostAsync(rfqPath + "/award", award);
        var contractId = rfq.GetProperty("contractId").GetInt32();
        rfq.GetProperty("awardSnapshotJson").GetString().Should().NotBeNullOrWhiteSpace();
        var contract = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId));
        contract.CustomerId.Should().Be(fixture.CustomerId);
        contract.OperationalProjectId.Should().Be(fixture.ProjectId);
        contract.VendorId.Should().Be(fixture.VendorId);
        contract.Value.Should().Be(1000m);
        contract.Direction.Should().Be(ContractDirection.Downstream);
        contract.Status.Should().Be(ContractStatus.Draft);
        var contractLine = await WithDbAsync(db => db.ContractLines.AsNoTracking().SingleAsync(x => x.ContractId == contractId));
        contractLine.ProjectBoqLineId.Should().Be(boqLineId);
        contractLine.Quantity.Should().Be(10m);
        contractLine.NegotiatedUnitPrice.Should().Be(100m);
        contractLine.ProcurementOwnerUserId.Should().Be(fixture.ProcurementId);

        var invoiceNumber = UniqueSlug("INV-PIPELINE");
        await LoginAsync("ACCOUNTANT");
        await RejectAsync("/api/finance/payment-requests", Invoice(fixture, contractId, invoiceNumber), HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(x => x.ContractId == contractId))).Should().Be(0);
        // Procurement owns the generated contract, but signing is delegated to
        // the existing contract manager role, which has manage + view.all.
        await LoginAsync("SALES_MANAGER");
        var signed = await PostAsync($"/api/contracts/{contractId}/transition", new
        {
            newStatus = "Signed",
            rowVersion = Convert.ToBase64String(contract.RowVersion),
        });
        signed.GetProperty("status").GetString().Should().Be("Signed");

        // RFQs do not create/reserve Material Requests. The site raises its own
        // demand against the same approved BOQ, then Procurement approves it.
        await LoginAsync("PM");
        var materialRequest = await PostAsync(procurement + "/material-requests", new
        {
            responsibleSiteUserId = fixture.PmId,
            assignedProcurementUserId = fixture.ProcurementId,
            requiredAt = DateTime.UtcNow.AddDays(3),
            note = "Power cable installation",
            lines = new[] { new { projectBoqLineId = boqLineId, requestedQuantity = 10m } },
        });
        var requestPath = procurement + $"/material-requests/{Id(materialRequest)}";
        materialRequest = await MoveAsync(requestPath, materialRequest, "submit");
        await LoginAsync("PROCUREMENT");
        materialRequest = await PostAsync(requestPath + "/decision", new { approved = true, rowVersion = Version(materialRequest) });
        var requestLineId = Id(materialRequest.GetProperty("lines")[0]);

        await LoginAsync("WAREHOUSE");
        var overReceipt = await PostAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 11m));
        await RejectAsync(procurement + $"/receipts/{Id(overReceipt)}/post", new { rowVersion = Version(overReceipt) }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceipts.SingleAsync(x => x.Id == Id(overReceipt)))).Status.Should().Be(WarehouseLedgerStatus.Draft);
        (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.Approved);
        var receipt = await PostAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 10m));
        receipt = await MoveAsync(procurement + $"/receipts/{Id(receipt)}", receipt, "post");
        receipt.GetProperty("status").GetString().Should().Be("Posted");
        (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.Fulfilled);
        var storedReceipt = await WithDbAsync(db => db.WarehouseReceiptLines.SingleAsync(x => x.WarehouseReceiptId == Id(receipt)));
        storedReceipt.ContractLineId.Should().Be(contractLine.Id);
        storedReceipt.MaterialRequestLineId.Should().Be(requestLineId);
        storedReceipt.ReceivedQuantity.Should().Be(10m);

        var overIssue = await PostAsync(procurement + "/issues", Issue(fixture, boqLineId, 11m));
        await RejectAsync(procurement + $"/issues/{Id(overIssue)}/post", new { rowVersion = Version(overIssue) }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseIssues.SingleAsync(x => x.Id == Id(overIssue)))).Status.Should().Be(WarehouseLedgerStatus.Draft);
        var issue = await PostAsync(procurement + "/issues", Issue(fixture, boqLineId, 10m));
        issue = await MoveAsync(procurement + $"/issues/{Id(issue)}", issue, "post");
        issue.GetProperty("status").GetString().Should().Be("Posted");
        // Consumption prevents reversal of the receipt that supplied the stock.
        await RejectAsync(procurement + $"/receipts/{Id(receipt)}/reverse", new { rowVersion = Version(receipt), reason = "Cannot reverse consumed cable" }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.WarehouseReceipts.SingleAsync(x => x.Id == Id(receipt)))).Status.Should().Be(WarehouseLedgerStatus.Posted);

        // Payables are entered manually. Current domain has no typed receipt /
        // acceptance foreign key or three-way match; do not invent one here.
        await LoginAsync("ACCOUNTANT");
        await RejectAsync("/api/finance/payment-requests", Invoice(fixture with { VendorId = fixture.OtherVendorId }, contractId, invoiceNumber), HttpStatusCode.BadRequest);
        var payment = await PostAsync("/api/finance/payment-requests", Invoice(fixture, contractId, invoiceNumber));
        var paymentId = Id(payment);
        var paymentPath = $"/api/finance/payment-requests/{paymentId}";
        await RejectAsync("/api/finance/payment-requests", Invoice(fixture, contractId, invoiceNumber), HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(x => x.ContractId == contractId))).Should().Be(1);
        await RejectAsync(paymentPath + "/pay", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.Draft);
        payment = await MoveAsync(paymentPath, payment, "submit");
        payment = await MoveAsync(paymentPath, payment, "validate");
        await RejectAsync(paymentPath + "/decision", new { approved = true, rowVersion = Version(payment) }, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.ReadyForApproval);
        await LoginAsync("BGD");
        payment = await PostAsync(paymentPath + "/decision", new { approved = true, rowVersion = Version(payment) });
        var approvedVersion = Version(payment);
        await LoginAsync("ACCOUNTANT");
        var payKey = Guid.NewGuid().ToString();
        payment = await PostAsync(paymentPath + "/pay", new { rowVersion = approvedVersion }, payKey);
        var replay = await PostAsync(paymentPath + "/pay", new { rowVersion = approvedVersion }, payKey);
        Id(replay).Should().Be(paymentId);
        payment.GetProperty("status").GetString().Should().Be("Paid");
        payment.GetProperty("events").EnumerateArray().Select(x => x.GetProperty("toStatus").GetString())
            .Should().Equal("Draft", "UnderValidation", "ReadyForApproval", "Approved", "Paid");
        var storedPayment = await WithDbAsync(db => db.PaymentRequests.Include(x => x.Attachments).Include(x => x.Events).SingleAsync(x => x.Id == paymentId));
        storedPayment.ContractId.Should().Be(contractId);
        storedPayment.VendorId.Should().Be(fixture.VendorId);
        storedPayment.InvoiceAmount.Should().Be(contract.Value);
        storedPayment.Currency.Should().Be("VND");
        storedPayment.ValidatedByUserId.Should().Be(fixture.AccountantId);
        storedPayment.PaidByUserId.Should().Be(fixture.AccountantId);
        storedPayment.ApprovedByUserId.Should().Be(fixture.BgdId);
        storedPayment.Attachments.Single().FilePath.Should().Be($"/files/finance/{invoiceNumber}.pdf");
        storedPayment.Events.Count(x => x.ToStatus == PaymentRequestStatus.Paid).Should().Be(1);
        await LoginAsync("BGD");
        await RejectAsync(paymentPath + "/cancel", new { rowVersion = Version(payment), reason = "Paid invoice cannot be cancelled" }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.Paid);
    }

    private sealed record Foundation(int ProjectId, int CustomerId, int ProcurementId, int PmId, int WarehouseId, int AccountantId, int BgdId, int VendorId, int OtherVendorId);

    private Task<Foundation> SeedFoundationAsync() => WithDbAsync(async db =>
    {
        var users = new Dictionary<string, int>();
        foreach (var role in new[] { "PROCUREMENT", "PM", "WAREHOUSE", "ACCOUNTANT", "BGD" })
            users[role] = (await db.Users.SingleAsync(x => x.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[role])).Id;
        var customer = new Customer
        {
            Name = UniqueSlug("Factory customer"),
            Type = CustomerType.Individual,
            SourceCode = "referral",
            Contacts = [new() { FullName = "Factory owner", Phone = "0912345678", IsPrimary = true }],
        };
        var project = new OperationalProject
        {
            Code = UniqueSlug("PJ-PIPELINE"),
            Name = "Cable procurement business pipeline",
            Customer = customer,
            ProjectManagerUserId = users["PM"],
            Status = OperationalProjectStatus.Active,
        };
        var vendors = new[] { "Primary supplier", "Alternative supplier" }.Select(name => new Vendor
        {
            VendorCode = UniqueSlug("NCC-PIPE"),
            CompanyName = name,
            VendorType = VendorType.Supplier,
            IsActive = true,
            Phone = "0912345678",
            CreatedByUserId = users["PROCUREMENT"],
        }).ToArray();
        db.OperationalProjects.Add(project);
        db.Vendors.AddRange(vendors);
        await db.SaveChangesAsync();
        foreach (var role in new[] { "PROCUREMENT", "WAREHOUSE" })
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = users[role],
                Position = role,
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = users["PM"],
                UpdatedByUserId = users["PM"],
            });
        await db.SaveChangesAsync();
        return new Foundation(project.Id, customer.Id, users["PROCUREMENT"], users["PM"], users["WAREHOUSE"], users["ACCOUNTANT"], users["BGD"], vendors[0].Id, vendors[1].Id);
    });

    private static object Receipt(Foundation fixture, int requestLineId, int contractLineId, decimal quantity) => new
    {
        inspectedAt = DateTime.UtcNow,
        receivedByUserId = fixture.WarehouseId,
        lines = new[] { new { materialRequestLineId = requestLineId, contractLineId, receivedQuantity = quantity } },
    };
    private static object Issue(Foundation fixture, int boqLineId, decimal quantity) => new
    {
        issuedAt = DateTime.UtcNow,
        issuedByUserId = fixture.WarehouseId,
        responsibleSiteUserId = fixture.PmId,
        workItemCode = "INSTALL-CABLE",
        lines = new[] { new { projectBoqLineId = boqLineId, issuedQuantity = quantity } },
    };
    private static object Invoice(Foundation fixture, int contractId, string invoiceNumber) => new
    {
        contractId,
        vendorId = fixture.VendorId,
        supplierInvoiceNumber = invoiceNumber,
        invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
        invoiceAmount = 1000m,
        currency = "VND",
        receivedAt = DateTime.UtcNow,
        assignedAccountantUserId = fixture.AccountantId,
        attachments = new[] { new { fileName = "Supplier cable invoice.pdf", filePath = $"/files/finance/{invoiceNumber}.pdf" } },
    };
    private async Task LoginAsync(string role)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));
    }
    private static int Id(JsonElement value) => value.GetProperty("id").GetInt32();
    private static string Version(JsonElement value) => value.GetProperty("rowVersion").GetString()!;
    private static string RfqVersion(JsonElement value) => Version(value.GetProperty("header"));
    private Task<JsonElement> MoveAsync(string path, JsonElement value, string action) => PostAsync(path + "/" + action, new { rowVersion = Version(value) });
    private async Task<JsonElement> PostAsync(string path, object body, string? key = null)
    {
        using var response = await SendAsync(path, body, key);
        response.IsSuccessStatusCode.Should().BeTrue($"{path}: {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }
    private async Task RejectAsync(string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(path, body);
        response.StatusCode.Should().Be(expected, $"{path}: {await response.Content.ReadAsStringAsync()}");
    }
    private async Task<HttpResponseMessage> SendAsync(string path, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }
}
