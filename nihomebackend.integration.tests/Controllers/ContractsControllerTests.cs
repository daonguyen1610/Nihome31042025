using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;

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
                phone = "0911" + Random.Shared.Next(100000, 999999),
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
    public async Task PreviewNextNumber_WithManagePermission_ReturnsEditableSuggestion()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var res = await Client.GetAsync("/api/contracts/next-number");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var contractNumber = (await ReadJsonAsync(res)).GetProperty("contractNumber").GetString();
        contractNumber.Should().MatchRegex($"^HD-{DateTime.UtcNow.Year}-[0-9]{{4,}}$");
    }

    [Fact]
    public async Task PreviewNextNumber_WithoutManagePermission_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await Client.GetAsync("/api/contracts/next-number")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
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
        (await ReadJsonAsync(first)).GetProperty("contractNumber").GetString().Should().Be(number);

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
    public async Task PrivateFiles_RequireAuthenticatedContentRoutesAndBlockStaticPaths()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        using var attachmentForm = CreateFileForm("contract attachment", "contract.pdf");
        attachmentForm.Add(new StringContent("Supporting"), "kind");
        attachmentForm.Add(new StringContent("Contract attachment"), "label");
        var attachmentUpload = await Client.PostAsync($"/api/contracts/{contractId}/attachments", attachmentForm);
        attachmentUpload.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await ReadJsonAsync(attachmentUpload);
        var attachmentId = attachment.GetProperty("id").GetInt32();
        var attachmentPath = attachment.GetProperty("filePath").GetString();

        (await Client.GetAsync(attachmentPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var attachmentContent = await Client.GetAsync($"/api/contracts/{contractId}/attachments/{attachmentId}/content");
        attachmentContent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await attachmentContent.Content.ReadAsStringAsync()).Should().Be("contract attachment");

        using var appendixForm = CreateFileForm("contract appendix", "appendix.pdf");
        var appendixUpload = await Client.PostAsync($"/api/contracts/{contractId}/appendices/files", appendixForm);
        appendixUpload.StatusCode.Should().Be(HttpStatusCode.OK);
        var appendix = await ReadJsonAsync(appendixUpload);
        var appendixPath = appendix.GetProperty("filePath").GetString();
        var appendixFileName = Path.GetFileName(appendixPath);

        (await Client.GetAsync(appendixPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/contracts/{contractId}/appendices/files/{appendixFileName}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var appendixCreate = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/appendices", new
        {
            title = "Private appendix",
            reason = "Content authorization regression",
            valueDelta = 1m,
            filePath = appendixPath,
            originalFileName = "appendix.pdf",
            fileSize = Encoding.UTF8.GetByteCount("contract appendix"),
            contentType = "application/pdf",
        });
        appendixCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        var appendixContent = await Client.GetAsync($"/api/contracts/{contractId}/appendices/files/{appendixFileName}/content");
        appendixContent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await appendixContent.Content.ReadAsStringAsync()).Should().Be("contract appendix");

        var otherContractId = await CreateContractAsync(customerId);
        (await Client.GetAsync($"/api/contracts/{otherContractId}/appendices/files/{appendixFileName}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var anonymousClient = Factory.CreateClient();
        (await anonymousClient.GetAsync($"/api/contracts/{contractId}/attachments/{attachmentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymousClient.GetAsync($"/api/contracts/{contractId}/appendices/files/{appendixFileName}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static MultipartFormDataContent CreateFileForm(string content, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
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
        var before = await WithDbAsync(db => db.Contracts.AsNoTracking()
            .SingleAsync(contract => contract.Id == contractId));
        var projectCountBefore = await WithDbAsync(db => db.DesignProjects
            .CountAsync(project => project.ContractId == contractId));

        var res = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/transition", new { newStatus = "InProgress" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var after = await WithDbAsync(db => db.Contracts.AsNoTracking()
            .SingleAsync(contract => contract.Id == contractId));
        after.Status.Should().Be(before.Status);
        after.SignedDate.Should().Be(before.SignedDate);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
        after.UpdatedByUserId.Should().Be(before.UpdatedByUserId);
        after.RowVersion.Should().Equal(before.RowVersion);
        (await WithDbAsync(db => db.DesignProjects.CountAsync(project => project.ContractId == contractId)))
            .Should().Be(projectCountBefore);
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

        (await Client.DeleteAsync($"/api/contracts/{contractId}/appendices/{voId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterDelete = await Client.GetAsync($"/api/contracts/{contractId}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterDeleteBody = await ReadJsonAsync(afterDelete);
        afterDeleteBody.GetProperty("approvedVoTotal").GetDecimal().Should().Be(0);
        afterDeleteBody.GetProperty("currentValue").GetDecimal().Should().Be(500_000_000m);
        (await Client.GetAsync($"/api/customers/{customerId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateAppendix_SameIdempotencyKey_ReplaysWithoutDuplicate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);
        var key = $"contract-vo-{Guid.NewGuid():N}";
        var payload = new { title = "VO idempotent", reason = "Retry", valueDelta = 1_000_000m };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{contractId}/appendices")
        {
            Content = JsonContent.Create(payload),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var first = await Client.SendAsync(firstRequest);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await ReadJsonAsync(first);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{contractId}/appendices")
        {
            Content = JsonContent.Create(payload),
        };
        replayRequest.Headers.Add("Idempotency-Key", key);
        using var replay = await Client.SendAsync(replayRequest);

        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        (await ReadJsonAsync(replay)).GetProperty("id").GetInt32()
            .Should().Be(firstBody.GetProperty("id").GetInt32());
        var list = await Client.GetAsync($"/api/contracts/{contractId}/appendices");
        (await ReadJsonAsync(list)).EnumerateArray()
            .Count(item => item.GetProperty("title").GetString() == "VO idempotent")
            .Should().Be(1);
    }

    [Fact]
    public async Task UpdateAppendix_ThroughDifferentContract_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var sourceContractId = await CreateContractAsync(customerId);
        var otherContractId = await CreateContractAsync(customerId);
        var create = await Client.PostAsJsonAsync($"/api/contracts/{sourceContractId}/appendices", new
        {
            title = "Source VO",
            reason = "Source contract only",
            valueDelta = 500_000m,
        });
        var voId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync($"/api/contracts/{otherContractId}/appendices/{voId}", new
        {
            title = "Cross contract",
            reason = "Must be rejected",
            valueDelta = 900_000m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var sourceRows = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{sourceContractId}/appendices"));
        sourceRows[0].GetProperty("title").GetString().Should().Be("Source VO");
    }

    [Fact]
    public async Task DeleteAppendix_MissingAppendix_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        (await Client.DeleteAsync($"/api/contracts/{contractId}/appendices/9999999"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAppendix_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.DeleteAsync("/api/contracts/9999999/appendices/9999999"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
            title = "VO",
            reason = "R",
            valueDelta = 1_000m,
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
