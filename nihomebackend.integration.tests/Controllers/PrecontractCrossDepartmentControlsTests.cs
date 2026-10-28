using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Data;
using NihomeBackend.Models;
using NihomeBackend.Services;

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

        // Sales creates the basic quote first, then routes its input-pricing
        // request to Procurement from the quote detail.
        await LoginAsync("SALES_MANAGER");
        var draft = await PostAsync("/api/quotes", new
        {
            opportunityId = scope.OpportunityId,
            draftOnly = true,
            note = "Chờ Cung ứng cung cấp giá đầu vào.",
        });
        var quoteId = Id(draft);
        var package = await PostAsync("/api/work-packages", new
        {
            kind = "Project",
            purpose = "ProcurementInputPricing",
            name = "Báo giá đầu vào gói cơ điện nhà máy",
            operationalProjectId = scope.ProjectId,
            sourceOpportunityId = scope.OpportunityId,
            sourceQuoteId = quoteId,
            departmentRoleGroupId = scope.ProcurementGroupId,
            leadUserId = scope.ProcurementLeadUserId,
        });
        var packageId = Id(package);
        package.GetProperty("purpose").GetString().Should().Be("ProcurementInputPricing");
        package.GetProperty("sourceQuoteId").GetInt32().Should().Be(quoteId);

        // Repeating the API request must not create a second active request for
        // the same quote, even if a client retries or bypasses the UI guard.
        var duplicatePackage = await SendAsync(HttpMethod.Post, "/api/work-packages", new
        {
            kind = "Project",
            purpose = "ProcurementInputPricing",
            name = "Yêu cầu giá đầu vào trùng cho cùng báo giá",
            operationalProjectId = scope.ProjectId,
            sourceOpportunityId = scope.OpportunityId,
            sourceQuoteId = quoteId,
            departmentRoleGroupId = scope.ProcurementGroupId,
            leadUserId = scope.ProcurementLeadUserId,
        });
        duplicatePackage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await duplicatePackage.Content.ReadAsStringAsync()).Should().Contain("đã có một yêu cầu giá đầu vào");
        (await WithDbAsync(db => db.DepartmentWorkPackages.CountAsync(item =>
            item.SourceQuoteId == quoteId && item.Purpose == WorkPackagePurpose.ProcurementInputPricing)))
            .Should().Be(1);

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

        // Editing a Draft offer in place must survive the unique (offer, item
        // code) index on the line table when the item codes stay the same: the
        // service replaces the whole line set on every update.
        offer = await WriteAsync(HttpMethod.Put, $"{offerPath}/{offerId}", new
        {
            vendorId = scope.VendorId,
            supplierReference = "BG-DIEN-2026-01",
            currency = "VND",
            validUntil = DateTime.UtcNow.AddDays(30),
            note = "Bổ sung khối lượng dự phòng.",
            rowVersion = Version(offer),
            lines = new[]
            {
                new { itemCode = "MEP-CABLE-01", description = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                    quantity = 320m, unitPrice = 415_000m, vatPercent = 8m, sortOrder = 0 },
                new { itemCode = "MEP-TRAY-01", description = "Máng cáp thép mạ kẽm 300x100", unit = "m",
                    quantity = 180m, unitPrice = 268_000m, vatPercent = 8m, sortOrder = 0 },
            },
        });
        offer.GetProperty("status").GetString().Should().Be("Draft");
        offer.GetProperty("lines").GetArrayLength().Should().Be(2);
        offer.GetProperty("grandTotal").GetDecimal().Should().Be(195_523_200m);

        offer = await SubmitForApprovalAsync($"{offerPath}/{offerId}", offer);
        offer.GetProperty("status").GetString().Should().Be("Submitted");

        // The author is not the assigned lead, so the approval-scope guard
        // conceals the decision endpoint and leaves the submitted price intact.
        await RejectAsync($"{offerPath}/{offerId}/decision",
            new { approved = true, rowVersion = Version(offer) }, HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.ProcurementInputPriceOffers.AsNoTracking().SingleAsync(item => item.Id == offerId)))
            .Status.Should().Be(InputPriceOfferStatus.Submitted);

        // An independent approver releases the price.
        await LoginAsync("SUPER_ADMIN");
        offer = await PostAsync($"{offerPath}/{offerId}/decision",
            new { approved = true, rowVersion = Version(offer) });
        offer.GetProperty("status").GetString().Should().Be("Approved");

        // Sales now completes the existing basic draft from the approved input
        // price, and the quote keeps a snapshot for later traceability.
        await LoginAsync("SALES_MANAGER");
        var quote = await WriteAsync(HttpMethod.Put, $"/api/quotes/{quoteId}", new
        {
            rowVersion = draft.GetProperty("rowVersion").GetString(),
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
        var rejected = await SendAsync(HttpMethod.Put, $"/api/quotes/{quoteId}", new
        {
            rowVersion = quote.GetProperty("rowVersion").GetString(),
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
        var unchanged = await ReadAsync($"/api/quotes/{quoteId}");
        unchanged.GetProperty("inputPriceOfferId").GetInt32().Should().Be(offerId);
        unchanged.GetProperty("rowVersion").GetString().Should().Be(quote.GetProperty("rowVersion").GetString());
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
            leadUserId = scope.FinanceLeadUserId,
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
        review = await SubmitForApprovalAsync(reviewPath, review);
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
    public async Task TenderUpdate_KeepsSharedProjectWhenFieldOmitted()
    {
        var scope = await SeedScopeAsync();
        await LoginAsync("SALES_MANAGER");
        var tender = await PostAsync("/api/tenders", new
        {
            name = "Gói thầu cơ điện nhà máy cơ khí",
            customerId = scope.CustomerId,
            operationalProjectId = scope.ProjectId,
            invitationReceivedAt = DateTime.UtcNow.AddDays(-1),
            invitationSource = "Email khách hàng",
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        var tenderId = Id(tender);

        // A later save that omits the project must not silently unlink it: both
        // the submission gate and the Finance dossier review depend on the link.
        var updated = await WriteAsync(HttpMethod.Put, $"/api/tenders/{tenderId}", new
        {
            name = tender.GetProperty("name").GetString(),
            submissionDeadline = DateTime.UtcNow.AddDays(21),
            note = "Dời hạn nộp theo thông báo của chủ đầu tư.",
        });
        updated.GetProperty("operationalProjectId").GetInt32().Should().Be(scope.ProjectId);
        (await WithDbAsync(db => db.Tenders.AsNoTracking().SingleAsync(item => item.Id == tenderId)))
            .OperationalProjectId.Should().Be(scope.ProjectId);
    }

    [Fact]
    public async Task QuoteSend_RequiresFreshFinanceReviewAfterMaterialChange()
    {
        var scope = await SeedScopeAsync();
        await LoginAsync("SALES_MANAGER");
        var quoteId = await CreateQuoteAsync(scope);
        await MoveQuoteAsync(quoteId, "submit");
        await MoveQuoteAsync(quoteId, "approve");

        // Finance – Accounting releases the review for the quote as it stands now.
        var package = await PostAsync("/api/work-packages", new
        {
            kind = "Project",
            purpose = "FinanceCommercialReview",
            name = "Rà soát thương mại báo giá cơ điện nhà máy",
            operationalProjectId = scope.ProjectId,
            sourceOpportunityId = scope.OpportunityId,
            sourceQuoteId = quoteId,
            departmentRoleGroupId = scope.FinanceGroupId,
            leadUserId = scope.FinanceLeadUserId,
        });
        var reviewPath = $"/api/work-packages/{Id(package)}/finance-review";
        await LoginAsync("ACCOUNTANT");
        var review = await WriteAsync(HttpMethod.Put, reviewPath, new
        {
            contractConditionsReviewed = true,
            guaranteesReviewed = true,
            paymentTermsReviewed = true,
            financialDossierReviewed = false,
            findings = "Điều kiện thanh toán và bảo lãnh đã đối chiếu.",
        });
        review.GetProperty("reviewedQuoteVersion").GetInt32().Should().Be(1);
        var listedPackages = await ReadAsync($"/api/work-packages?purpose=FinanceCommercialReview&sourceQuoteId={quoteId}");
        listedPackages.GetProperty("items")[0].GetProperty("financeReview")
            .GetProperty("reviewedQuoteVersion").GetInt32().Should().Be(1);
        review = await SubmitForApprovalAsync(reviewPath, review);
        await LoginAsync("SUPER_ADMIN");
        await PostAsync($"{reviewPath}/decision", new { approved = true, rowVersion = Version(review) });

        // Sales then changes the price, which re-versions the quote and sends it
        // back to Draft for a fresh internal approval.
        await LoginAsync("SALES_MANAGER");
        var versioned = await WriteAsync(HttpMethod.Put, $"/api/quotes/{quoteId}", new
        {
            rowVersion = await VersionAsync($"/api/quotes/{quoteId}"),
            discountPercent = 5m,
            vatPercent = 8m,
            items = new[]
            {
                new { itemCode = "MEP-CABLE-01", name = "Cáp điện Cu/XLPE 4x25mm2", unit = "m",
                    quantity = 320m, unitPrice = 480_000m },
            },
        });
        versioned.GetProperty("version").GetInt32().Should().Be(2);
        versioned.GetProperty("status").GetString().Should().Be("Draft");
        await MoveQuoteAsync(quoteId, "submit");
        await MoveQuoteAsync(quoteId, "approve");

        // The review approved version 1 only, so version 2 cannot reach the
        // customer until Finance – Accounting reviews the changed terms too.
        var blocked = await SendQuoteAsync(quoteId);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("rà soát lại");
        (await ReadAsync($"/api/quotes/{quoteId}")).GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task TenderSubmission_RequiresSharedProjectAndApprovedFinanceTenderReview()
    {
        var scope = await SeedScopeAsync();
        await LoginAsync("SALES_MANAGER");

        // A tender cannot be created outside the shared OperationalProject
        // aggregate because its plan and Finance review use that root.
        using var orphanCreate = await SendAsync(HttpMethod.Post, "/api/tenders", new
        {
            name = "Gói thầu chưa gắn dự án vận hành",
            customerId = scope.CustomerId,
            invitationReceivedAt = DateTime.UtcNow.AddDays(-1),
            invitationSource = "Cổng đấu thầu quốc gia",
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        orphanCreate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await orphanCreate.Content.ReadAsStringAsync()).Should().Contain("Dự án vận hành");

        var tender = await PostAsync("/api/tenders", new
        {
            name = "Gói thầu cơ điện nhà máy cơ khí NICON",
            customerId = scope.CustomerId,
            operationalProjectId = scope.ProjectId,
            invitationReceivedAt = DateTime.UtcNow.AddDays(-1),
            invitationSource = "Cổng đấu thầu quốc gia",
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        var tenderId = Id(tender);
        await PrepareApprovedEstimateAsync(tenderId);
        await CreateCompletedTenderPlanAsync(scope.ProjectId, tenderId);
        await WithDbAsync(async db =>
        {
            var checklist = await db.TenderChecklistItems
                .Where(item => item.TenderId == tenderId)
                .ToListAsync();
            checklist.ForEach(item => item.Status = TenderChecklistItemStatus.Done);
            await db.SaveChangesAsync();
        });

        // A completed status alone is not dossier evidence. Every project-
        // specific row needs its own upload and capability uses the library.
        (await SubmitTenderAsync(tenderId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await CompleteTenderChecklistWithEvidenceAsync(tenderId);

        // Planned and fully estimated, but still without the Finance –
        // Accounting review of the tender financial dossier.
        (await SubmitTenderAsync(tenderId)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

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
            leadUserId = scope.FinanceLeadUserId,
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
        review = await SubmitForApprovalAsync(reviewPath, review);
        review.GetProperty("status").GetString().Should().Be("Submitted");

        await LoginAsync("SUPER_ADMIN");
        await PostAsync($"{reviewPath}/decision", new { approved = true, rowVersion = Version(review) });

        await LoginAsync("SALES_MANAGER");
        using var submittedResponse = await SubmitTenderAsync(tenderId);
        submittedResponse.StatusCode.Should().Be(HttpStatusCode.OK, await submittedResponse.Content.ReadAsStringAsync());
        var submitted = await ReadJsonAsync(submittedResponse);
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
            new
            {
                itemCode = "MEP-CABLE-01",
                description = "Cáp điện Cu/XLPE 4x25mm2",
                unit = "m",
                approvedQuantity = 320m,
                budgetUnitPrice = 415_000m
            },
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
        int ProcurementGroupId, int FinanceGroupId, int ProcurementLeadUserId, int FinanceLeadUserId);

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

        // The existing role accounts remain the dossier authors. Notification
        // leads must be separate, active users in the receiving department.
        var procurementLeadId = await SeedDepartmentLeadAsync(db, "PROCUREMENT", "0971000016",
            "Trần Minh Khoa — Trưởng nhóm Cung ứng", "procurement.lead@nicon.example.com");
        var financeLeadId = await SeedDepartmentLeadAsync(db, "ACCOUNTANT", "0971000007",
            "Nguyễn Thu Hà — Trưởng nhóm Tài chính", "finance.lead@nicon.example.com");

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
                     (procurementLeadId, "PROCUREMENT"),
                     (financeLeadId, "ACCOUNTANT"),
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
            procurementGroupId, financeGroupId, procurementLeadId, financeLeadId);
    });

    private static async Task<int> SeedDepartmentLeadAsync(
        AppDbContext db, string roleCode, string phoneNumber, string fullName, string email)
    {
        var existing = await db.Users.SingleOrDefaultAsync(item => item.PhoneNumber == phoneNumber);
        if (existing is not null) return existing.Id;

        var lead = new ApplicationUser
        {
            PhoneNumber = phoneNumber,
            FullName = fullName,
            Email = email,
            Role = UserRole.USER,
            RoleEntityId = await db.Roles.Where(item => item.Code == roleCode).Select(item => item.Id).SingleAsync(),
            IsActive = true,
        };
        lead.PasswordHash = new PasswordService().Hash(lead, TestDataSeeder.DefaultPassword);
        db.Users.Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
    }

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

    private async Task<JsonElement> SubmitForApprovalAsync(string recordPath, JsonElement record)
    {
        var preview = await PostAsync($"{recordPath}/approval-email-preview", new { });
        return await PostAsync($"{recordPath}/submit", new
        {
            rowVersion = Version(record),
            previewToken = preview.GetProperty("previewToken").GetString(),
        });
    }

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
