using System.Net;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ContractsController</c> — CRUD, RBAC scoping
/// (Sales sees only own, Manager sees all), duplicate handling, validation.
/// NIH-102 scope: list + minimal CRUD. Payment milestones / VOs are follow-up.
/// </summary>
public class ContractsControllerTests : IntegrationTestBase
{
    public ContractsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateCustomerAsync()
    {
        var payload = new
        {
            type = "Individual",
            name = "Contract Test " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Contact",
                phone = "0911" + Guid.NewGuid().ToString("N")[..6],
                isPrimary = true,
            },
        };
        var res = await Client.PostAsJsonAsync("/api/customers", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    private static object ContractBody(int customerId, string status = "Draft", decimal value = 100_000_000)
        => new { customerId, status, value, signedDate = "2026-06-01T00:00:00Z" };

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/contracts")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsSalesManager_ReturnsOkShape()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.GetAsync("/api/contracts");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("items").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        body.GetProperty("total").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
    }

    [Fact]
    public async Task Create_WithoutManagePermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var res = await Client.PostAsJsonAsync("/api/contracts", ContractBody(1));
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FullRoundTrip_AsSalesManager_Create_Update_Delete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var created = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        body.GetProperty("contractNumber").GetString().Should().StartWith("HD-");
        body.GetProperty("customerId").GetInt32().Should().Be(customerId);
        body.GetProperty("status").GetString().Should().Be("Draft");

        var update = await Client.PutAsJsonAsync($"/api/contracts/{id}", new
        {
            customerId,
            status = "Signed",
            value = 500_000_000,
            signedDate = "2026-06-15T00:00:00Z",
            startDate = "2026-07-01T00:00:00Z",
            endDate = "2026-12-31T00:00:00Z",
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("status").GetString().Should().Be("Signed");

        (await Client.DeleteAsync($"/api/contracts/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/contracts/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateExplicitNumber_ReturnsConflict()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var number = "HD-INT-" + Guid.NewGuid().ToString("N")[..6];

        var first = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            contractNumber = number,
            status = "Draft",
            value = 100,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            contractNumber = number,
            status = "Draft",
            value = 100,
        });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_UnknownCustomer_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var res = await Client.PostAsJsonAsync("/api/contracts", ContractBody(9_999_999));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AsSales_IgnoresRequestedOwner_AndPinsToCaller()
    {
        // SALE has crm.contracts.manage but NOT crm.contracts.view.all —
        // the service must ignore the caller-supplied ownerUserId and pin
        // the row to the SALE user so they can still see it. If the row
        // ended up owned by a different user, GET /api/contracts (scoped to
        // owner) would return zero.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            ownerUserId = 9_999_999,
            status = "Draft",
            value = 100,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        // The caller can list it back — proof that the owner stayed with
        // the SALE caller, not the caller-supplied id.
        var list = await Client.GetAsync($"/api/contracts?customerId={customerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(list);
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_WithMilestonesSumming100_PersistsSchedule()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            status = "Signed",
            value = 1_000_000_000,
            signedDate = "2026-06-01T00:00:00Z",
            paymentMilestones = new object[]
            {
                new { order = 1, name = "Tạm ứng", percentValue = 30m, status = "Pending" },
                new { order = 2, name = "Nghiệm thu", percentValue = 60m, status = "Pending" },
                new { order = 3, name = "Quyết toán", percentValue = 10m, status = "Pending" },
            },
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        var milestones = body.GetProperty("paymentMilestones");
        milestones.GetArrayLength().Should().Be(3);
        milestones[0].GetProperty("amount").GetDecimal().Should().Be(300_000_000m);
        milestones[1].GetProperty("amount").GetDecimal().Should().Be(600_000_000m);
        milestones[2].GetProperty("amount").GetDecimal().Should().Be(100_000_000m);
    }

    [Fact]
    public async Task Create_WithMilestonesNotSumming100_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var res = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            status = "Draft",
            value = 100_000_000,
            paymentMilestones = new object[]
            {
                new { order = 1, name = "Only", percentValue = 40m, status = "Pending" },
            },
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- NIH-104: state transitions, milestone status, VO workflow ----------

    private async Task<int> CreateContractAsync(int customerId, string status = "Draft", decimal value = 100_000_000m)
    {
        var res = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId, status, value));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Transition_DraftToSigned_Succeeds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        var res = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/transition", new { newStatus = "Signed" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("status").GetString().Should().Be("Signed");
    }

    [Fact]
    public async Task Transition_SignedToInProgress_WithoutScan_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId, status: "Signed");

        var res = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/transition", new { newStatus = "InProgress" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transition_IllegalPath_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        var res = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/transition", new { newStatus = "Completed" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Milestone_StatusUpdate_UpdatesStatus()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var body = new
        {
            customerId,
            status = "Signed",
            value = 100_000_000,
            paymentMilestones = new object[]
            {
                new { order = 1, name = "M1", percentValue = 100m, status = "Pending" },
            },
        };
        var created = await Client.PostAsJsonAsync("/api/contracts", body);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdJson = await ReadJsonAsync(created);
        var contractId = createdJson.GetProperty("id").GetInt32();
        var milestoneId = createdJson.GetProperty("paymentMilestones")[0].GetProperty("id").GetInt32();

        var res = await Client.PatchAsJsonAsync(
            $"/api/contracts/{contractId}/milestones/{milestoneId}/status",
            new { status = "Paid" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).GetProperty("paymentMilestones")[0].GetProperty("status").GetString()
            .Should().Be("Paid");
    }

    [Fact]
    public async Task VoWorkflow_CreateSubmitApprove_UpdatesCurrentValue()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId, status: "Signed", value: 500_000_000m);

        var create = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/appendices", new
        {
            title = "VO test",
            reason = "test reason",
            valueDelta = 50_000_000m,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var voId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var submit = await Client.PostAsync($"/api/contracts/{contractId}/appendices/{voId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var approve = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/appendices/{voId}/approve",
            new { note = "OK" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(approve)).GetProperty("status").GetString().Should().Be("Approved");

        var refreshed = await Client.GetAsync($"/api/contracts/{contractId}");
        var body = await ReadJsonAsync(refreshed);
        body.GetProperty("approvedVoTotal").GetDecimal().Should().Be(50_000_000m);
        body.GetProperty("currentValue").GetDecimal().Should().Be(550_000_000m);
    }

    [Fact]
    public async Task VoReject_WithoutNote_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId, status: "Signed");

        var create = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/appendices", new
        {
            title = "VO test",
            reason = "reason",
            valueDelta = 10_000_000m,
        });
        var voId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();
        await Client.PostAsync($"/api/contracts/{contractId}/appendices/{voId}/submit", null);

        var rej = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/appendices/{voId}/reject",
            new { note = (string?)null });
        rej.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approve_AsSaleWithoutViewAll_Returns403()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId, status: "Signed");
        var create = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/appendices", new
        {
            title = "VO", reason = "R", valueDelta = 1_000m,
        });
        var voId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();
        await Client.PostAsync($"/api/contracts/{contractId}/appendices/{voId}/submit", null);

        var approve = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/appendices/{voId}/approve",
            new { note = "" });
        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_ReturnsCurrentValueAndCounts()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        var res = await Client.GetAsync($"/api/contracts/{contractId}");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(res);
        body.GetProperty("approvedVoTotal").GetDecimal().Should().Be(0);
        body.GetProperty("currentValue").GetDecimal().Should().Be(100_000_000m);
        body.GetProperty("hasSignedScan").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Timeline_ReturnsAuditEvents()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);
        // Trigger a couple of audited actions so the timeline has content.
        await Client.PostAsJsonAsync($"/api/contracts/{contractId}/transition", new { newStatus = "Signed" });

        var res = await Client.GetAsync($"/api/contracts/{contractId}/timeline");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(res)).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }
}
