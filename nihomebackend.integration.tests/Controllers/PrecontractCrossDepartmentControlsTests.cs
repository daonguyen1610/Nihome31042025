using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// MoM §5 — cross-department work before the contract. Proves the three
/// pre-contract joins through the real HTTP pipeline: Procurement input prices
/// feed the Sales output quote, Finance – Accounting reviews commercial terms
/// after the quote and the tender financial dossier before submission, and the
/// signed-BOQ control stays on the contract side of the boundary.
/// </summary>
public sealed class PrecontractCrossDepartmentControlsTests(NihomeWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task InputPriceOffer_IndependentApprovalGatesQuoteLinkage()
    {
        var scope = await SeedScopeAsync();

        // Sales routes a pre-contract work package to the Procurement department
        // as the opportunity owner — no separate screen, just the project group.
        await LoginAsync("SALES_MANAGER");
        var package = await PostAsync("/api/work-packages", new
        {
            kind = "Project",
            purpose = "ProcurementInputPricing",
            name = "Báo giá đầu vào gói cơ điện nhà máy",
            operationalProjectId = scope.ProjectId,
            sourceOpportunityId = scope.OpportunityId,
            departmentRoleGroupId = scope.ProcurementGroupId,
            leadUserId = scope.ProcurementUserId,
        });
        var packageId = Id(package);
        package.GetProperty("purpose").GetString().Should().Be("ProcurementInputPricing");

        // Procurement captures supplier input prices.
        await LoginAsync("PROCUREMENT");
        var offerPath = $"/api/work-packages/{packageId}/input-price-offers";
        var offer = await PostAsync(offerPath, new
        {
            vendorId = scope.VendorId,
            supplierReference = "BG-DIEN-2026-01",
            currency = "VND",
            validUntil = DateTime.UtcNow.AddDays(30),
            note = "Báo giá thiết bị điện theo phạm vi cơ điện.",
            lines = new[]
            {
                new { itemCode = "MEP-CABLE-01", description = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                    quantity = 320m, unitPrice = 415_000m, vatPercent = 8m, sortOrder = 0 },
                new { itemCode = "MEP-TRAY-01", description = "Máng cáp thép mạ kẽm 300x100", unit = "m",
                    quantity = 180m, unitPrice = 268_000m, vatPercent = 8m, sortOrder = 0 },
            },
        });
        var offerId = Id(offer);
        offer.GetProperty("status").GetString().Should().Be("Draft");
        offer.GetProperty("grandTotal").GetDecimal().Should().Be(195_523_200m);

        offer = await MoveAsync($"{offerPath}/{offerId}", offer, "submit");
        offer.GetProperty("status").GetString().Should().Be("Submitted");

        // The author cannot approve their own input price.
        await RejectAsync($"{offerPath}/{offerId}/decision",
            new { approved = true, rowVersion = Version(offer) }, HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProcurementInputPriceOffers.AsNoTracking().SingleAsync(item => item.Id == offerId)))
            .Status.Should().Be(InputPriceOfferStatus.Submitted);

        // An independent approver releases the price.
        await LoginAsync("SUPER_ADMIN");
        offer = await PostAsync($"{offerPath}/{offerId}/decision",
            new { approved = true, rowVersion = Version(offer) });
        offer.GetProperty("status").GetString().Should().Be("Approved");

        // Sales now builds the output quote on the approved input price, and the
        // quote keeps a snapshot of it for later traceability.
        await LoginAsync("SALES_MANAGER");
        var quote = await PostAsync("/api/quotes", new
        {
            opportunityId = scope.OpportunityId,
            method = "Boq",
            packageDescription = "Gói cơ điện nhà máy theo giá đầu vào Cung ứng",
            inputPriceOfferId = offerId,
            items = new[]
            {
                new { itemCode = "MEP-CABLE-01", name = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                    quantity = 320m, unitPrice = 480_000m },
            },
            discountPercent = 0m,
            vatPercent = 8m,
        });
        quote.GetProperty("inputPriceOfferId").GetInt32().Should().Be(offerId);
        quote.GetProperty("inputPriceVendorName").GetString().Should().Be(scope.VendorName);
        quote.GetProperty("inputPriceSupplierReference").GetString().Should().Be("BG-DIEN-2026-01");
        quote.GetProperty("inputPriceSnapshotJson").GetString().Should().Contain("MEP-CABLE-01");

        // A price that has not been released by the independent approver is not
        // usable for an output quote.
        var pendingOfferId = await AddPendingOfferAsync(packageId, scope.VendorId);
        var rejected = await SendAsync(HttpMethod.Post, "/api/quotes", new
        {
            opportunityId = scope.OpportunityId,
            method = "Boq",
            inputPriceOfferId = pendingOfferId,
            items = new[]
            {
                new { itemCode = "MEP-TRAY-01", name = "Máng cáp thép mạ kẽm", unit = "m", quantity = 180m, unitPrice = 300_000m },
            },
            vatPercent = 8m,
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("giá đầu vào");
    }

    [Fact]
    public async Task QuoteSend_RequiresApprovedFinanceCommercialReview()
    {
        var scope = await SeedScopeAsync();
        await LoginAsync("SALES_MANAGER");
        var quoteId = await CreateQuoteAsync(scope);
        await MoveQuoteAsync(quoteId, "submit");
        await MoveQuoteAsync(quoteId, "approve");

        // Without the Finance – Accounting commercial review the quote cannot
        // reach the customer, and the status stays Approved.
        var blocked = await SendQuoteAsync(quoteId);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("Tài chính");
        (await ReadAsync($"/api/quotes/{quoteId}")).GetProperty("status").GetString().Should().Be("Approved");

        // Finance – Accounting joins after the quote: contract conditions,
        // guarantees and payment terms, submitted by the department and
        // independently approved. Sales requests the review as the opportunity
        // owner; Finance performs it.
        await LoginAsync("SALES_MANAGER");
        var package = await PostAsync("/api/work-packages", new
        {
            kind = "Project",
            purpose = "FinanceCommercialReview",
            name = "Rà soát điều kiện hợp đồng và thanh toán báo giá cơ điện",
            operationalProjectId = scope.ProjectId,
            sourceOpportunityId = scope.OpportunityId,
            sourceQuoteId = quoteId,
            departmentRoleGroupId = scope.FinanceGroupId,
            leadUserId = scope.AccountantUserId,
        });
        var packageId = Id(package);
        var reviewPath = $"/api/work-packages/{packageId}/finance-review";
        await LoginAsync("ACCOUNTANT");
        var review = await WriteAsync(HttpMethod.Put, reviewPath, new
        {
            contractConditionsReviewed = true,
            guaranteesReviewed = true,
            paymentTermsReviewed = true,
            financialDossierReviewed = false,
            findings = "Điều kiện thanh toán 3 đợt theo tiến độ thi công đã thống nhất.",
        });
        review = await MoveAsync(reviewPath, review, "submit");
        review.GetProperty("status").GetString().Should().Be("Submitted");

        // Submitted but not yet approved still blocks the customer send.
        await LoginAsync("SALES_MANAGER");
        (await SendQuoteAsync(quoteId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await LoginAsync("SUPER_ADMIN");
        await PostAsync($"{reviewPath}/decision", new { approved = true, rowVersion = Version(review) });

        await LoginAsync("SALES_MANAGER");
        var preview = await ReadAsync($"/api/quotes/{quoteId}/email-preview");
        var sent = await PostAsync($"/api/quotes/{quoteId}/send", new
        {
            rowVersion = await VersionAsync($"/api/quotes/{quoteId}"),
            toEmail = "khachhang@nicon.example.com",
            subject = preview.GetProperty("subject").GetString(),
            body = preview.GetProperty("body").GetString(),
        });
        sent.GetProperty("status").GetString().Should().Be("SentToCustomer");
        (await WithDbAsync(db => db.Quotes.AsNoTracking().SingleAsync(item => item.Id == quoteId)))
            .SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TenderSubmission_RequiresSharedProjectAndApprovedFinanceTenderReview()
    {
        var scope = await SeedScopeAsync();
        await LoginAsync("SALES_MANAGER");
        var tender = await PostAsync("/api/tenders", new
        {
            name = "Gói thầu cơ điện nhà máy cơ khí NICON",
            customerId = scope.CustomerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        var tenderId = Id(tender);
        var transitionPath = $"/api/tenders/{tenderId}/transition";

        await PrepareApprovedEstimateAsync(tenderId);
        await CompleteChecklistAsync(tenderId);

        // A tender with no shared project cannot be submitted: Finance cannot
        // review a dossier that is not bound to the business project.
        var orphan = await SendAsync(HttpMethod.Post, transitionPath, new { status = "Submitted" });
        orphan.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await orphan.Content.ReadAsStringAsync()).Should().Contain("Dự án vận hành");

        await WithDbAsync(async db =>
        {
            var entity = await db.Tenders.SingleAsync(item => item.Id == tenderId);
            entity.OperationalProjectId = scope.ProjectId;
            await db.SaveChangesAsync();
        });

        // Linked to the project and fully estimated, but still without the
        // Finance – Accounting review of the tender financial dossier.
        await RejectAsync(transitionPath, new { status = "Submitted" }, HttpStatusCode.BadRequest);

        // The project manager assigns the tender financial review to Finance.
        await LoginAsync("SUPER_ADMIN");
        var package = await PostAsync("/api/work-packages", new
        {
            kind = "Project",
            purpose = "FinanceTenderReview",
            name = "Rà soát hồ sơ tài chính gói thầu cơ điện",
            operationalProjectId = scope.ProjectId,
            sourceTenderId = tenderId,
            departmentRoleGroupId = scope.FinanceGroupId,
            leadUserId = scope.AccountantUserId,
        });
        var packageId = Id(package);
        var reviewPath = $"/api/work-packages/{packageId}/finance-review";
        await LoginAsync("ACCOUNTANT");
        // The tender review needs the financial dossier and guarantees only.
        var review = await WriteAsync(HttpMethod.Put, reviewPath, new
        {
            contractConditionsReviewed = false,
            guaranteesReviewed = true,
            paymentTermsReviewed = false,
            financialDossierReviewed = true,
        });
        review = await MoveAsync(reviewPath, review, "submit");
        review.GetProperty("status").GetString().Should().Be("Submitted");

        await LoginAsync("SUPER_ADMIN");
        await PostAsync($"{reviewPath}/decision", new { approved = true, rowVersion = Version(review) });

        await LoginAsync("SALES_MANAGER");
        var submitted = await PostAsync(transitionPath, new { status = "Submitted" });
        submitted.GetProperty("status").GetString().Should().Be("Submitted");
    }

    [Fact]
    public async Task ExecutionBoq_RequiresSignedUpstreamContract()
    {
        var scope = await SeedScopeAsync();
        var draftContractId = await AddContractAsync(scope, ContractStatus.Draft);
        var signedContractId = await AddContractAsync(scope, ContractStatus.Signed);

        await LoginAsync("PROCUREMENT");
        var path = $"/api/operational-projects/{scope.ProjectId}/procurement/boq-revisions";
        object[] lines =
        [
            new { itemCode = "MEP-CABLE-01", description = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                approvedQuantity = 320m, budgetUnitPrice = 415_000m },
        ];

        // No contract reference at all.
        await RejectAsync(path, new { currency = "VND", lines }, HttpStatusCode.BadRequest);
        // A contract that is not signed yet cannot release the execution BOQ.
        await RejectAsync(path,
            new { upstreamContractId = draftContractId, currency = "VND", lines },
            HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ProjectBoqRevisions.CountAsync(item => item.OperationalProjectId == scope.ProjectId)))
            .Should().Be(0);

        var boq = await PostAsync(path,
            new { upstreamContractId = signedContractId, currency = "VND", lines });
        boq.GetProperty("status").GetString().Should().Be("Draft");
        (await WithDbAsync(db => db.ProjectBoqRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == Id(boq)))).UpstreamContractId.Should().Be(signedContractId);
    }

    // ---------- fixture ----------

    private sealed record Scope(
        int ProjectId, int OpportunityId, int CustomerId, int VendorId, string VendorName,
        int ProcurementGroupId, int FinanceGroupId, int ProcurementUserId, int AccountantUserId);

    private Task<Scope> SeedScopeAsync() => WithDbAsync(async db =>
    {
        var adminId = await db.Users.Where(item => item.RoleEntity != null && item.RoleEntity.Code == "SUPER_ADMIN")
            .Select(item => item.Id).SingleAsync();
        var procurementUserId = await db.Users
            .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["PROCUREMENT"])
            .Select(item => item.Id).SingleAsync();
        var accountantUserId = await db.Users
            .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
            .Select(item => item.Id).SingleAsync();
        var salesManagerId = await db.Users
            .Where(item => item.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["SALES_MANAGER"])
            .Select(item => item.Id).SingleAsync();
        var procurementGroupId = await db.RoleGroups.Where(item => item.Code == "PROCUREMENT")
            .Select(item => item.Id).SingleAsync();
        var financeGroupId = await db.RoleGroups.Where(item => item.Code == "FINANCE")
            .Select(item => item.Id).SingleAsync();

        var customer = new Customer
        {
            Name = "Nhà máy cơ khí chính xác NICON",
            Type = CustomerType.Company,
            SourceCode = "referral",
        };
        var project = new OperationalProject
        {
            Code = $"NIC-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            Name = "Nhà máy cơ khí chính xác NICON",
            Customer = customer,
            ProjectManagerUserId = adminId,
            Status = OperationalProjectStatus.Active,
        };
        // Sales owns the opportunity, so Sales can route the pre-contract
        // package to Procurement and Finance without a separate screen.
        var opportunity = new Opportunity
        {
            Name = "Cơ hội cải tạo hệ thống cơ điện nhà máy",
            Customer = customer,
            OperationalProject = project,
            OwnerUserId = salesManagerId,
            Stage = OpportunityStage.Proposal,
            EstimatedValue = 12_000_000_000m,
        };
        var vendor = new Vendor
        {
            VendorCode = $"NCC-DIEN-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            CompanyName = "Công ty Thiết bị Điện NICON",
            VendorType = VendorType.Supplier,
            IsActive = true,
            CreatedByUserId = procurementUserId,
            UpdatedByUserId = procurementUserId,
        };
        db.AddRange(opportunity, vendor);
        await db.SaveChangesAsync();
        // Procurement and Finance work the project through membership, which is
        // what the procurement and finance workspaces check for scope.
        foreach (var (userId, position) in new[]
                 {
                     (procurementUserId, "PROCUREMENT"),
                     (accountantUserId, "ACCOUNTANT"),
                 })
            db.OperationalProjectMembers.Add(new OperationalProjectMember
            {
                OperationalProjectId = project.Id,
                UserId = userId,
                Position = position,
                StartedAt = DateTime.UtcNow,
                CreatedByUserId = adminId,
                UpdatedByUserId = adminId,
            });
        await db.SaveChangesAsync();
        return new Scope(project.Id, opportunity.Id, customer.Id, vendor.Id, vendor.CompanyName,
            procurementGroupId, financeGroupId, procurementUserId, accountantUserId);
    });

    private Task<int> AddContractAsync(Scope scope, ContractStatus status) => WithDbAsync(async db =>
    {
        var contract = new Contract
        {
            ContractNumber = $"HD-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            CustomerId = scope.CustomerId,
            Direction = ContractDirection.Upstream,
            Type = ContractType.Construction,
            OperationalProjectId = scope.ProjectId,
            Status = status,
            SignedDate = status == ContractStatus.Signed ? DateTime.UtcNow.AddDays(-1) : null,
            Value = 12_000_000_000m,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract.Id;
    });

    private Task<int> AddPendingOfferAsync(int packageId, int vendorId) => WithDbAsync(async db =>
    {
        var offer = new ProcurementInputPriceOffer
        {
            WorkPackageId = packageId,
            VendorId = vendorId,
            SupplierReference = "BG-DIEN-2026-02",
            Currency = "VND",
            ValidUntil = DateTime.UtcNow.AddDays(15),
            Status = InputPriceOfferStatus.Submitted,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines =
            [
                new ProcurementInputPriceLine
                {
                    ItemCode = "MEP-TRAY-01",
                    Description = "Máng cáp thép mạ kẽm 300x100",
                    Unit = "m",
                    Quantity = 180m,
                    UnitPrice = 300_000m,
                    VatPercent = 8m,
                    Amount = 54_000_000m,
                    SortOrder = 1,
                },
            ],
        };
        db.ProcurementInputPriceOffers.Add(offer);
        await db.SaveChangesAsync();
        return offer.Id;
    });

    private async Task<int> CreateQuoteAsync(Scope scope)
    {
        var quote = await PostAsync("/api/quotes", new
        {
            opportunityId = scope.OpportunityId,
            method = "Boq",
            packageDescription = "Thi công hệ thống cơ điện nhà máy",
            items = new[]
            {
                new { itemCode = "MEP-CABLE-01", name = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                    quantity = 320m, unitPrice = 480_000m },
            },
            discountPercent = 0m,
            vatPercent = 8m,
        });
        return Id(quote);
    }

    private async Task MoveQuoteAsync(int quoteId, string action) =>
        await PostAsync($"/api/quotes/{quoteId}/{action}",
            new { rowVersion = await VersionAsync($"/api/quotes/{quoteId}") });

    private async Task<HttpResponseMessage> SendQuoteAsync(int quoteId) =>
        await SendAsync(HttpMethod.Post, $"/api/quotes/{quoteId}/send", new
        {
            rowVersion = await VersionAsync($"/api/quotes/{quoteId}"),
            toEmail = "khachhang@nicon.example.com",
            subject = "Báo giá cơ điện nhà máy",
            body = "Kính gửi Quý khách hàng, NICON xin gửi báo giá đính kèm.",
        });

    private async Task<int> PrepareApprovedEstimateAsync(int tenderId)
    {
        using var multipart = new MultipartFormDataContent();
        var csv = "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\r\n"
            + "MEP-CABLE-01,Cáp điện Cu/XLPE 4x25mm2,m,320,415000,480000,8,Ghi chú\r\n";
        using var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", "estimate.csv");
        using var imported = await Client.PostAsync($"/api/tenders/{tenderId}/estimates/import", multipart);
        imported.IsSuccessStatusCode.Should().BeTrue(await imported.Content.ReadAsStringAsync());
        var revisionId = (await ReadJsonAsync(imported)).GetProperty("revision").GetProperty("id").GetInt32();
        await PostAsync($"/api/tenders/{tenderId}/estimates/{revisionId}/submit", new { });
        await PostAsync($"/api/tenders/{tenderId}/estimates/{revisionId}/approve",
            new { note = "Đạt yêu cầu kỹ thuật" });
        return revisionId;
    }

    private Task CompleteChecklistAsync(int tenderId) => WithDbAsync(async db =>
    {
        var items = await db.TenderChecklistItems.Where(item => item.TenderId == tenderId).ToListAsync();
        items.ForEach(item => item.Status = TenderChecklistItemStatus.Done);
        await db.SaveChangesAsync();
    });

    private Task LoginAsync(string role) =>
        AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private async Task<JsonElement> PostAsync(string path, object body) =>
        await SendJsonAsync(HttpMethod.Post, path, body);

    private async Task<JsonElement> WriteAsync(HttpMethod method, string path, object body) =>
        await SendJsonAsync(method, path, body);

    private async Task<JsonElement> SendJsonAsync(HttpMethod method, string path, object body)
    {
        using var response = await SendAsync(method, path, body);
        response.IsSuccessStatusCode.Should().BeTrue($"{path}: {await response.Content.ReadAsStringAsync()}");
        return await ReadJsonAsync(response);
    }

    private async Task<JsonElement> MoveAsync(string recordPath, JsonElement record, string action) =>
        await PostAsync($"{recordPath}/{action}", new { rowVersion = Version(record) });

    private async Task RejectAsync(string path, object body, HttpStatusCode expected)
    {
        using var response = await SendAsync(HttpMethod.Post, path, body);
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await Client.SendAsync(request);
    }

    private async Task<JsonElement> ReadAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<string> VersionAsync(string path) => Version(await ReadAsync(path));

    private static int Id(JsonElement value) => value.GetProperty("id").GetInt32();
    private static string Version(JsonElement value) => value.GetProperty("rowVersion").GetString()!;
}
