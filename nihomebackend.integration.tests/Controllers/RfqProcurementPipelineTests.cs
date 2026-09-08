using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class RfqProcurementPipelineTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData(VendorType.Supplier, ContractType.Supply, "Paid")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "RejectedUnderValidation")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "RejectedReadyForApproval")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "Cancelled")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "CancelRfqDraft")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "CancelRfqIssued")]
    [InlineData(VendorType.Supplier, ContractType.Supply, "CancelRfqEvaluation")]
    [InlineData(VendorType.SubContractor, ContractType.Subcontract, "Paid")]
    [InlineData(VendorType.Both, ContractType.Subcontract, "Paid")]
    [InlineData(VendorType.Both, ContractType.Supply, "Paid")]
    public async Task ApprovedScope_ToAward_AndInvoiceOutcome_PreservesBusinessHandoffs(
        VendorType vendorType, ContractType contractType, string outcome)
    {
        var fixture = await SeedFoundationAsync(vendorType);
        var procurement = $"/api/operational-projects/{fixture.ProjectId}/procurement";
        var rfqs = procurement + "/rfqs";

        int? estimateId = null;
        if (vendorType == VendorType.Supplier && outcome == "Paid")
            estimateId = await PrepareWonTenderEstimateAsync(fixture);

        // Every intermediate state is reached through a public endpoint using
        // its ordinary business role, rather than seeded as already approved.
        await LoginAsync("PROCUREMENT");
        var boq = await PostAsync(procurement + "/boq-revisions", new
        {
            currency = "VND",
            sourceTenderEstimateRevisionId = estimateId,
            lines = new[] { new { itemCode = "CABLE-PIPELINE", description = "Factory power cable", unit = "m", approvedQuantity = 10m, budgetUnitPrice = 120m } },
        });
        var boqId = Id(boq);
        var storedBoq = await WithDbAsync(db => db.ProjectBoqRevisions.SingleAsync(x => x.Id == boqId));
        storedBoq.SourceTenderEstimateRevisionId.Should().Be(estimateId);
        storedBoq.OperationalProjectId.Should().Be(fixture.ProjectId);
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
        if (outcome == "CancelRfqDraft") { await AssertRfqCancellationAsync(fixture, rfqPath, rfq); return; }
        rfq = await PostAsync(rfqPath + "/issue", new { rowVersion = RfqVersion(rfq) });
        if (outcome == "CancelRfqIssued") { await AssertRfqCancellationAsync(fixture, rfqPath, rfq); return; }
        var rfqLineId = Id(rfq.GetProperty("lines")[0]);
        // Both supports an explicit non-lowest selection (Supply) and a tie
        // (Subcontract); the business decision must never be automatic.
        var alternativePrice = vendorType == VendorType.Both ? (contractType == ContractType.Supply ? 90m : 100m) : 110m;
        foreach (var vendor in new[] { (fixture.VendorId, Price: 100m), (fixture.OtherVendorId, Price: alternativePrice) })
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
        if (vendorType == VendorType.Supplier && outcome == "Paid")
        {
            var original = rfq.GetProperty("bids").EnumerateArray().Single(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId);
            rfq = await SubmitQuoteAsync(rfqPath, rfq, fixture.VendorId, 95m);
            var revised = rfq.GetProperty("bids").EnumerateArray().Single(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId && x.GetProperty("isCurrent").GetBoolean());
            rfq = await PostAsync(rfqPath + $"/bids/{Id(revised)}/withdraw", new { reason = "Supplier corrected the delivery scope", rowVersion = RfqVersion(rfq) });
            var supplierBids = rfq.GetProperty("bids").EnumerateArray().Where(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId).ToArray();
            supplierBids.Should().OnlyContain(x => !x.GetProperty("isEligible").GetBoolean());
            supplierBids.Single(x => Id(x) == Id(original)).GetProperty("isCurrent").GetBoolean().Should().BeFalse();
            rfq = await SubmitQuoteAsync(rfqPath, rfq, fixture.VendorId, 100m);
            rfq.GetProperty("bids").EnumerateArray().Count(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId).Should().Be(3);
        }
        rfq = await PostAsync(rfqPath + "/evaluate", new { rowVersion = RfqVersion(rfq) });
        if (outcome == "CancelRfqEvaluation") { await AssertRfqCancellationAsync(fixture, rfqPath, rfq); return; }
        if (vendorType == VendorType.Both && contractType == ContractType.Subcontract)
            rfq.GetProperty("bids").EnumerateArray().Count(x => x.GetProperty("isLowest").GetBoolean()).Should().Be(2);
        var winningBid = rfq.GetProperty("bids").EnumerateArray().Single(x => x.GetProperty("vendorId").GetInt32() == fixture.VendorId && x.GetProperty("isCurrent").GetBoolean());
        winningBid.GetProperty("isLowest").GetBoolean().Should().Be(alternativePrice >= 100m);
        var award = new { bidId = Id(winningBid), reason = "Complete scope, inspected delivery and best price", contractType = contractType.ToString(), rowVersion = RfqVersion(rfq) };
        await RejectAsync(rfqPath + "/award", award, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        await LoginAsync("BGD");
        if (vendorType != VendorType.Both)
        {
            await RejectAsync(rfqPath + "/award", new { bidId = Id(winningBid), reason = "Wrong commercial contract type", contractType = contractType == ContractType.Supply ? "Subcontract" : "Supply", rowVersion = RfqVersion(rfq) }, HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        }
        rfq = await PostAsync(rfqPath + "/award", award);
        var contractId = rfq.GetProperty("contractId").GetInt32();
        rfq.GetProperty("awardSnapshotJson").GetString().Should().NotBeNullOrWhiteSpace();
        var contract = await WithDbAsync(db => db.Contracts.AsNoTracking().SingleAsync(x => x.Id == contractId));
        contract.CustomerId.Should().Be(fixture.CustomerId);
        contract.OperationalProjectId.Should().Be(fixture.ProjectId);
        contract.VendorId.Should().Be(fixture.VendorId);
        contract.Value.Should().Be(1000m);
        contract.Direction.Should().Be(ContractDirection.Downstream);
        contract.Type.Should().Be(contractType);
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

        await LoginAsync("PROCUREMENT");
        rfq = await PostAsync(rfqPath + "/close", new { rowVersion = RfqVersion(rfq) });
        rfq.GetProperty("header").GetProperty("status").GetString().Should().Be("Closed");

        // Physical receipt/issue is the Supply branch. Subcontract payables use
        // the existing signed-contract contract; acceptance integration is absent.
        if (contractType == ContractType.Supply)
        {
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
            // Warehouse validation now rejects excess quantities before a draft
            // is persisted, as well as rechecking quantities when posting.
            await RejectAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 11m), HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.WarehouseReceipts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
            (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.Approved);
            var partialReceipt = await PostAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 4m));
            partialReceipt = await MoveAsync(procurement + $"/receipts/{Id(partialReceipt)}", partialReceipt, "post");
            (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);
            var correctedReceipt = await PostAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 6m));
            correctedReceipt = await MoveAsync(procurement + $"/receipts/{Id(correctedReceipt)}", correctedReceipt, "post");
            (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.Fulfilled);
            await PostAsync(procurement + $"/receipts/{Id(correctedReceipt)}/reverse", new { reason = "Correct delivery inspection record", rowVersion = Version(correctedReceipt) });
            (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.PartiallyFulfilled);
            var receipt = await PostAsync(procurement + "/receipts", Receipt(fixture, requestLineId, contractLine.Id, 6m));
            receipt = await MoveAsync(procurement + $"/receipts/{Id(receipt)}", receipt, "post");
            receipt.GetProperty("status").GetString().Should().Be("Posted");
            (await WithDbAsync(db => db.MaterialRequests.SingleAsync(x => x.Id == Id(materialRequest)))).Status.Should().Be(MaterialRequestStatus.Fulfilled);
            var storedReceipt = await WithDbAsync(db => db.WarehouseReceiptLines.SingleAsync(x => x.WarehouseReceiptId == Id(receipt)));
            storedReceipt.ContractLineId.Should().Be(contractLine.Id);
            storedReceipt.MaterialRequestLineId.Should().Be(requestLineId);
            storedReceipt.ReceivedQuantity.Should().Be(6m);

            await RejectAsync(procurement + "/issues", Issue(fixture, boqLineId, 11m), HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.WarehouseIssues.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
            var issue = await PostAsync(procurement + "/issues", Issue(fixture, boqLineId, 10m));
            issue = await MoveAsync(procurement + $"/issues/{Id(issue)}", issue, "post");
            issue.GetProperty("status").GetString().Should().Be("Posted");
            // Consumption prevents reversal of the receipt that supplied the stock.
            await RejectAsync(procurement + $"/receipts/{Id(receipt)}/reverse", new { rowVersion = Version(receipt), reason = "Cannot reverse consumed cable" }, HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.WarehouseReceipts.SingleAsync(x => x.Id == Id(receipt)))).Status.Should().Be(WarehouseLedgerStatus.Posted);

        }

        // Payables are entered manually. Current domain has no typed receipt /
        // acceptance foreign key or three-way match; do not invent one here.
        await LoginAsync("ACCOUNTANT");
        using var referencesResponse = await Client.GetAsync("/api/finance/payment-references");
        referencesResponse.IsSuccessStatusCode.Should().BeTrue(await referencesResponse.Content.ReadAsStringAsync());
        var referenceContract = (await ReadJsonAsync(referencesResponse)).GetProperty("contracts").EnumerateArray()
            .Single(x => x.GetProperty("contractNumber").GetString() == contract.ContractNumber);
        var payableContractId = Id(referenceContract);
        payableContractId.Should().Be(contractId);
        referenceContract.GetProperty("vendorId").GetInt32().Should().Be(fixture.VendorId);
        await RejectAsync("/api/finance/payment-requests", Invoice(fixture with { VendorId = fixture.OtherVendorId }, contractId, invoiceNumber), HttpStatusCode.BadRequest);
        var payment = await PostAsync("/api/finance/payment-requests", Invoice(fixture, payableContractId, invoiceNumber));
        var paymentId = Id(payment);
        var paymentPath = $"/api/finance/payment-requests/{paymentId}";
        await RejectAsync("/api/finance/payment-requests", Invoice(fixture, contractId, invoiceNumber), HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(x => x.ContractId == contractId))).Should().Be(1);
        await RejectAsync(paymentPath + "/pay", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.Draft);
        payment = await MoveAsync(paymentPath, payment, "submit");
        // Having the permission does not authorize validating another
        // accountant's assignment; an administrator must also be rejected.
        await LoginAsync("ADMIN");
        await RejectAsync(paymentPath + "/validate", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
        var stillAssigned = await WithDbAsync(db => db.PaymentRequests.Include(x => x.Events).SingleAsync(x => x.Id == paymentId));
        stillAssigned.Status.Should().Be(PaymentRequestStatus.UnderValidation);
        stillAssigned.Events.Should().HaveCount(2);
        stillAssigned.ValidatedAt.Should().BeNull();
        await LoginAsync("ACCOUNTANT");
        if (outcome != "RejectedUnderValidation")
            payment = await MoveAsync(paymentPath, payment, "validate");
        await RejectAsync(paymentPath + "/decision", new { approved = true, rowVersion = Version(payment) }, HttpStatusCode.Forbidden);
        var beforeDecision = outcome == "RejectedUnderValidation" ? PaymentRequestStatus.UnderValidation : PaymentRequestStatus.ReadyForApproval;
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(beforeDecision);
        await LoginAsync("BGD");
        if (outcome.StartsWith("Rejected", StringComparison.Ordinal))
        {
            await RejectAsync(paymentPath + "/decision", new { approved = false, rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(beforeDecision);
            payment = await PostAsync(paymentPath + "/decision", new { approved = false, reason = "Invoice scope requires supplier correction", rowVersion = Version(payment) });
            await AssertUnpayableTerminalAsync(fixture, payment, "Rejected", contractId);
            return;
        }
        payment = await PostAsync(paymentPath + "/decision", new { approved = true, rowVersion = Version(payment) });
        if (outcome == "Cancelled")
        {
            await RejectAsync(paymentPath + "/cancel", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
            (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.Approved);
            payment = await PostAsync(paymentPath + "/cancel", new { reason = "Supplier invoice replaced before payment", rowVersion = Version(payment) });
            await AssertUnpayableTerminalAsync(fixture, payment, "Cancelled", contractId);
            return;
        }
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
        // The finance list is the delivered payment read model. Project reports
        // explicitly mark actual cashflow/ledger finance unavailable; a Paid
        // request must not be presented as a bank transaction or a P&L posting.
        using var paymentListResponse = await Client.GetAsync("/api/finance/payment-requests");
        paymentListResponse.EnsureSuccessStatusCode();
        var listedPayment = (await ReadJsonAsync(paymentListResponse)).EnumerateArray().Single(x => Id(x) == paymentId);
        listedPayment.GetProperty("status").GetString().Should().Be("Paid");
        listedPayment.GetProperty("contractNumber").GetString().Should().Be(contract.ContractNumber);
        listedPayment.GetProperty("supplierInvoiceNumber").GetString().Should().Be(invoiceNumber);
        listedPayment.GetProperty("invoiceAmount").GetDecimal().Should().Be(1000m);
        listedPayment.GetProperty("currency").GetString().Should().Be("VND");
        listedPayment.GetProperty("paidAt").ValueKind.Should().Be(JsonValueKind.String);
        await LoginAsync("BGD");
        await RejectAsync(paymentPath + "/cancel", new { rowVersion = Version(payment), reason = "Paid invoice cannot be cancelled" }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.SingleAsync(x => x.Id == paymentId))).Status.Should().Be(PaymentRequestStatus.Paid);
    }

    private async Task AssertRfqCancellationAsync(Foundation fixture, string path, JsonElement rfq)
    {
        var originalState = rfq.GetProperty("header").GetProperty("status").GetString();
        var originalEvents = rfq.GetProperty("events").GetArrayLength();
        await RejectAsync(path + "/cancel", new { reason = " ", rowVersion = RfqVersion(rfq) }, HttpStatusCode.BadRequest);
        using (var unchanged = await Client.GetAsync(path))
        {
            unchanged.EnsureSuccessStatusCode();
            var body = await ReadJsonAsync(unchanged);
            body.GetProperty("header").GetProperty("status").GetString().Should().Be(originalState);
            body.GetProperty("events").GetArrayLength().Should().Be(originalEvents);
        }
        var bodyRequest = new { reason = "Customer cancelled the electrical package", rowVersion = RfqVersion(rfq) };
        var key = Guid.NewGuid().ToString();
        rfq = await PostAsync(path + "/cancel", bodyRequest, key);
        await PostAsync(path + "/cancel", bodyRequest, key);
        await RejectAsync(path + "/issue", new { rowVersion = RfqVersion(rfq) }, HttpStatusCode.BadRequest);
        using var finalResponse = await Client.GetAsync(path);
        finalResponse.EnsureSuccessStatusCode();
        var final = await ReadJsonAsync(finalResponse);
        final.GetProperty("header").GetProperty("status").GetString().Should().Be("Cancelled");
        final.GetProperty("events").GetArrayLength().Should().Be(originalEvents + 1);
        final.GetProperty("contractId").ValueKind.Should().Be(JsonValueKind.Null);
        (await WithDbAsync(db => db.Contracts.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        (await WithDbAsync(db => db.MaterialRequests.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
    }

    private async Task<int> PrepareWonTenderEstimateAsync(Foundation fixture)
    {
        await LoginAsync("SALES_MANAGER");
        var opportunity = await PostAsync("/api/opportunities", new { name = "Factory tender opportunity", customerId = fixture.CustomerId, operationalProjectId = fixture.ProjectId });
        var tender = await PostAsync("/api/tenders", new { name = "Factory electrical installation tender", customerId = fixture.CustomerId, submissionDeadline = DateTime.UtcNow.AddDays(14) });
        var tenderPath = $"/api/tenders/{Id(tender)}";
        await RejectAsync(tenderPath + "/transition", new { status = "Submitted" }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Tenders.SingleAsync(x => x.Id == Id(tender)))).Status.Should().Be(TenderStatus.Preparing);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\r\nCABLE-PIPELINE,Factory power cable,m,10,120,150,0,Factory delivery\r\n"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "factory-estimate.csv");
        using var imported = await Client.PostAsync(tenderPath + "/estimates/import", form);
        imported.IsSuccessStatusCode.Should().BeTrue(await imported.Content.ReadAsStringAsync());
        var estimate = (await ReadJsonAsync(imported)).GetProperty("revision");
        var estimateId = Id(estimate);
        var estimatePath = tenderPath + $"/estimates/{estimateId}";
        await PostAsync(estimatePath + "/submit", new { });
        await LoginAsync("SALE");
        await RejectAsync(estimatePath + "/approve", new { note = "Unauthorized estimate decision" }, HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.TenderEstimateRevisions.SingleAsync(x => x.Id == estimateId))).Status.Should().Be(TenderEstimateRevisionStatus.Submitted);
        await LoginAsync("SALES_MANAGER");
        await PostAsync(estimatePath + "/approve", new { note = "Approved scope and costing" });
        foreach (var item in tender.GetProperty("checklistItems").EnumerateArray())
        {
            using var completed = await Client.PatchAsJsonAsync(tenderPath + $"/checklist/{Id(item)}", new { status = "Done" });
            completed.IsSuccessStatusCode.Should().BeTrue(await completed.Content.ReadAsStringAsync());
        }
        await PostAsync(tenderPath + "/transition", new { status = "Submitted" });
        await LoginAsync("PROCUREMENT");
        // Approved estimating alone is not a won project procurement source.
        await RejectAsync($"/api/operational-projects/{fixture.ProjectId}/procurement/boq-revisions", new
        {
            currency = "VND",
            sourceTenderEstimateRevisionId = estimateId,
            lines = new[] { new { itemCode = "CABLE-PIPELINE", description = "Factory power cable", unit = "m", approvedQuantity = 10m, budgetUnitPrice = 120m } },
        }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(x => x.OperationalProjectId == fixture.ProjectId))).Should().Be(0);
        await LoginAsync("SALES_MANAGER");
        tender = await PostAsync(tenderPath + "/mark-won", new { opportunityId = Id(opportunity), note = "Customer selected factory installation tender" });
        tender.GetProperty("status").GetString().Should().Be("Won");
        tender.GetProperty("wonOpportunityId").GetInt32().Should().Be(Id(opportunity));
        return estimateId;
    }

    private Task<JsonElement> SubmitQuoteAsync(string path, JsonElement rfq, int vendorId, decimal price) =>
        PostAsync(path + "/bids", new
        {
            vendorId,
            rowVersion = RfqVersion(rfq),
            leadTimeDays = 3,
            paymentTerms = "100% after inspected delivery",
            validUntil = DateTime.UtcNow.AddDays(14),
            lines = new[] { new { rfqLineId = Id(rfq.GetProperty("lines")[0]), unitPrice = price } },
            documentIds = Array.Empty<long>(),
        });

    private async Task AssertUnpayableTerminalAsync(Foundation fixture, JsonElement payment, string status, int contractId)
    {
        payment.GetProperty("status").GetString().Should().Be(status);
        var paymentId = Id(payment);
        var eventsBefore = payment.GetProperty("events").GetArrayLength();
        await LoginAsync("ACCOUNTANT");
        var path = $"/api/finance/payment-requests/{paymentId}";
        await RejectAsync(path + "/pay", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
        await RejectAsync(path + "/submit", new { rowVersion = Version(payment) }, HttpStatusCode.BadRequest);
        var stored = await WithDbAsync(db => db.PaymentRequests.Include(x => x.Events).SingleAsync(x => x.Id == paymentId));
        stored.Status.ToString().Should().Be(status);
        stored.PaidAt.Should().BeNull();
        stored.PaidByUserId.Should().BeNull();
        stored.Events.Should().HaveCount(eventsBefore);
        stored.Events.Should().NotContain(x => x.ToStatus == PaymentRequestStatus.Paid);
        stored.Events.Single(x => x.ToStatus.ToString() == status).ChangedByUserId.Should().Be(fixture.BgdId);
        stored.ContractId.Should().Be(contractId);
        (await WithDbAsync(db => db.Contracts.SingleAsync(x => x.Id == contractId))).Status.Should().Be(ContractStatus.Signed);
        using var list = await Client.GetAsync("/api/finance/payment-requests");
        list.EnsureSuccessStatusCode();
        var persisted = (await ReadJsonAsync(list)).EnumerateArray().Single(x => Id(x) == paymentId);
        persisted.GetProperty("status").GetString().Should().Be(status);
        persisted.GetProperty("decisionReason").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private sealed record Foundation(int ProjectId, int CustomerId, int ProcurementId, int PmId, int WarehouseId, int AccountantId, int BgdId, int VendorId, int OtherVendorId);

    private Task<Foundation> SeedFoundationAsync(VendorType vendorType = VendorType.Supplier) => WithDbAsync(async db =>
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
            VendorType = vendorType,
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
