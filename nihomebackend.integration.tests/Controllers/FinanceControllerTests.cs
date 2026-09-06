using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class FinanceControllerTests : IntegrationTestBase
{
    public FinanceControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task ScopedLists_WithoutAuthentication_ReturnUnauthorized()
    {
        (await Client.GetAsync("/api/finance/payment-requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/finance/periods")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/finance/corrections")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePayment_AsSaleWithoutPermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "SALE"));
        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests", ValidPayment(1, 1, 1));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PaymentLifecycle_EnforcesSelfApprovalAndPaidImmutability()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var draft = await CreatePaymentAsync(fixture);
        draft.GetProperty("status").GetString().Should().Be("Draft");

        var underValidation = await TransitionAsync("payment-requests", draft, "submit");
        underValidation.GetProperty("submittedByUserId").GetInt32().Should().Be(fixture.AccountantId);
        var ready = await TransitionAsync("payment-requests", underValidation, "validate");
        ready.GetProperty("validatedAt").ValueKind.Should().Be(JsonValueKind.String);

        using var selfApproval = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{ready.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = ready.GetProperty("rowVersion").GetString() });
        selfApproval.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item => item.Id == ready.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.ReadyForApproval);

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("payment-requests", ready, "decision", new
        {
            approved = true,
            rowVersion = ready.GetProperty("rowVersion").GetString(),
        });
        approved.GetProperty("status").GetString().Should().Be("Approved");

        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var paid = await TransitionAsync("payment-requests", approved, "pay");
        paid.GetProperty("status").GetString().Should().Be("Paid");
        paid.GetProperty("paidByUserId").GetInt32().Should().Be(fixture.AccountantId);
        paid.GetProperty("events").EnumerateArray()
            .Select(item => item.GetProperty("toStatus").GetString())
            .Should().Equal("Draft", "UnderValidation", "ReadyForApproval", "Approved", "Paid");

        using var updatePaid = await SendAsync(HttpMethod.Put,
            $"/api/finance/payment-requests/{paid.GetProperty("id").GetInt32()}",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId, paid.GetProperty("rowVersion").GetString()));
        updatePaid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var cancelPaid = await SendAsync(HttpMethod.Post,
            $"/api/finance/payment-requests/{paid.GetProperty("id").GetInt32()}/cancel",
            new { reason = "Cannot cancel a paid request", rowVersion = paid.GetProperty("rowVersion").GetString() });
        cancelPaid.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.PaymentRequests.AsNoTracking().SingleAsync(item => item.Id == paid.GetProperty("id").GetInt32())))
            .Status.Should().Be(PaymentRequestStatus.Paid);
    }

    [Fact]
    public async Task DuplicateSupplierInvoice_PerVendor_IsRejectedWithoutSecondRow()
    {
        var fixture = await CreateFixtureAsync();
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        await CreatePaymentAsync(fixture);
        using var duplicate = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
        duplicate.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.PaymentRequests.CountAsync(item => item.VendorId == fixture.VendorId &&
            item.SupplierInvoiceNumber == "INV-2026-001"))).Should().Be(1);
    }

    [Fact]
    public async Task PeriodClose_UsesVietnamBoundaries_RequiresSequence_AndCannotReopen()
    {
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/periods", new { year = 2030, month = 2 });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var period = await ReadJsonAsync(createdResponse);
        period.GetProperty("periodStartUtc").GetDateTime().Should().Be(new DateTime(2030, 1, 31, 17, 0, 0, DateTimeKind.Utc));
        period.GetProperty("periodEndUtc").GetDateTime().Should().Be(new DateTime(2030, 2, 28, 17, 0, 0, DateTimeKind.Utc));

        using var prematureClose = await SendAsync(HttpMethod.Post,
            $"/api/finance/periods/{period.GetProperty("id").GetInt32()}/close",
            new { reason = "Month-end close", rowVersion = period.GetProperty("rowVersion").GetString() });
        prematureClose.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.AccountingPeriods.AsNoTracking().SingleAsync(item => item.Id == period.GetProperty("id").GetInt32())))
            .Status.Should().Be(AccountingPeriodStatus.Open);

        var closing = await TransitionAsync("periods", period, "start-closing");
        var closed = await TransitionAsync("periods", closing, "close", new
        {
            reason = "Month-end close completed",
            rowVersion = closing.GetProperty("rowVersion").GetString(),
        });
        closed.GetProperty("status").GetString().Should().Be("Closed");
        using var noReopenRoute = await SendAsync(HttpMethod.Post,
            $"/api/finance/periods/{closed.GetProperty("id").GetInt32()}/reopen",
            new { reason = "Not allowed", rowVersion = closed.GetProperty("rowVersion").GetString() });
        noReopenRoute.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task CorrectionWorkflow_RejectsInvalidTransition_ApprovesAndReversesOnce()
    {
        var fixture = await CreateFixtureAsync();
        var sourcePaymentId = await SeedPaidPaymentAsync(fixture);
        var periodId = await SeedClosedPeriodAsync(2026, 8);
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        using var createdResponse = await SendAsync(HttpMethod.Post, "/api/finance/corrections", new
        {
            operationalProjectId = fixture.ProjectId,
            accountingPeriodId = periodId,
            sourceEntityType = "PaymentRequest",
            sourceEntityId = sourcePaymentId,
            reasonCode = "AMOUNT_ERROR",
            originalValue = 1000m,
            correctedValue = 900m,
            currency = "VND",
            note = "Supplier credit note received",
            responsibleAccountantUserId = fixture.AccountantId,
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var draft = await ReadJsonAsync(createdResponse);

        using var invalidDecision = await SendAsync(HttpMethod.Post,
            $"/api/finance/corrections/{draft.GetProperty("id").GetInt32()}/decision",
            new { approved = true, rowVersion = draft.GetProperty("rowVersion").GetString() });
        invalidDecision.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.AccountingCorrections.AsNoTracking().SingleAsync(item => item.Id == draft.GetProperty("id").GetInt32())))
            .Status.Should().Be(AccountingCorrectionStatus.Draft);

        var submitted = await TransitionAsync("corrections", draft, "submit");
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, "BGD"));
        var approved = await TransitionAsync("corrections", submitted, "decision", new
        {
            approved = true,
            rowVersion = submitted.GetProperty("rowVersion").GetString(),
        });
        var reversal = await TransitionAsync("corrections", approved, "reverse", new
        {
            reason = "Reverse after approved replacement entry",
            rowVersion = approved.GetProperty("rowVersion").GetString(),
        });
        reversal.GetProperty("reversalOfCorrectionId").GetInt32().Should().Be(approved.GetProperty("id").GetInt32());
        reversal.GetProperty("originalValue").GetDecimal().Should().Be(900m);
        reversal.GetProperty("correctedValue").GetDecimal().Should().Be(1000m);
        (await WithDbAsync(db => db.AccountingCorrections.CountAsync(item => item.ReversalOfCorrectionId == approved.GetProperty("id").GetInt32())))
            .Should().Be(1);
    }

    private async Task<FinanceFixture> CreateFixtureAsync() => await WithDbAsync(async db =>
    {
        var accountantId = await UserIdAsync(db, "ACCOUNTANT");
        var pmId = await UserIdAsync(db, "PM");
        var vendor = new Vendor
        {
            VendorCode = $"V-{Guid.NewGuid():N}"[..16],
            CompanyName = "Finance Test Vendor",
            VendorType = VendorType.Supplier,
        };
        var customer = new Customer { Type = CustomerType.Company, Name = "Finance Test Customer", SourceCode = "referral" };
        db.AddRange(vendor, customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject
        {
            Code = $"PJ-FIN-{Guid.NewGuid():N}"[..30],
            Name = "Finance Test Project",
            CustomerId = customer.Id,
            ProjectManagerUserId = pmId,
        };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var contract = new Contract
        {
            ContractNumber = $"HD-FIN-{Guid.NewGuid():N}"[..30],
            CustomerId = customer.Id,
            VendorId = vendor.Id,
            OperationalProjectId = project.Id,
            Direction = ContractDirection.Downstream,
            Type = ContractType.Supply,
            Status = ContractStatus.InProgress,
            SignedDate = new DateTime(2026, 7, 1),
            Value = 100_000m,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return new FinanceFixture(project.Id, contract.Id, vendor.Id, accountantId);
    });

    private async Task<JsonElement> CreatePaymentAsync(FinanceFixture fixture)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/finance/payment-requests",
            ValidPayment(fixture.ContractId, fixture.VendorId, fixture.AccountantId));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadJsonAsync(response);
    }

    private static object ValidPayment(int contractId, int vendorId, int accountantId, string? rowVersion = null) => new
    {
        contractId,
        vendorId,
        supplierInvoiceNumber = "INV-2026-001",
        invoiceDate = "2026-08-01",
        invoiceAmount = 1000m,
        currency = "VND",
        receivedAt = new DateTime(2026, 8, 2, 1, 0, 0, DateTimeKind.Utc),
        assignedAccountantUserId = accountantId,
        attachments = new[] { new { fileName = "invoice.pdf", filePath = "/files/finance/invoice.pdf" } },
        rowVersion,
    };

    private async Task<int> SeedPaidPaymentAsync(FinanceFixture fixture) => await WithDbAsync(async db =>
    {
        var payment = new PaymentRequest
        {
            Code = $"PAY-{Guid.NewGuid():N}"[..20],
            ContractId = fixture.ContractId,
            VendorId = fixture.VendorId,
            SupplierInvoiceNumber = $"INV-{Guid.NewGuid():N}"[..25],
            InvoiceDate = new DateOnly(2026, 7, 1),
            InvoiceAmount = 1000m,
            Status = PaymentRequestStatus.Paid,
            ReceivedAt = new DateTime(2026, 7, 1),
            ValidatedAt = new DateTime(2026, 7, 2),
            AssignedAccountantUserId = fixture.AccountantId,
            PaidAt = new DateTime(2026, 7, 5),
            PaidByUserId = fixture.AccountantId,
            CreatedByUserId = fixture.AccountantId,
        };
        db.PaymentRequests.Add(payment);
        await db.SaveChangesAsync();
        return payment.Id;
    });

    private async Task<int> SeedClosedPeriodAsync(int year, int month) => await WithDbAsync(async db =>
    {
        var existing = await db.AccountingPeriods.SingleOrDefaultAsync(item => item.Year == year && item.Month == month);
        if (existing is not null) return existing.Id;
        var period = new AccountingPeriod
        {
            Year = year,
            Month = month,
            PeriodStartUtc = new DateTime(year, month, 1).AddHours(-7),
            PeriodEndUtc = new DateTime(year, month, 1).AddMonths(1).AddHours(-7),
            Status = AccountingPeriodStatus.Closed,
            ClosedAt = DateTime.UtcNow,
            CloseReason = "Test close",
        };
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        return period.Id;
    });

    private async Task<JsonElement> TransitionAsync(string root, JsonElement current, string action, object? payload = null)
    {
        payload ??= new { rowVersion = current.GetProperty("rowVersion").GetString() };
        using var response = await SendAsync(HttpMethod.Post,
            $"/api/finance/{root}/{current.GetProperty("id").GetInt32()}/{action}", payload);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    private static Task<int> UserIdAsync(NihomeBackend.Data.AppDbContext db, string roleCode) => db.Users
        .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[roleCode])
        .Select(user => user.Id).SingleAsync();

    private sealed record FinanceFixture(int ProjectId, int ContractId, int VendorId, int AccountantId);
}