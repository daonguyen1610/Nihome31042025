using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public class TenderEstimatesControllerTests : IntegrationTestBase
{
    public TenderEstimatesControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task EstimateFlow_ImportsSubmitsEnforcesApprovalPermissionAndApproves()
    {
        await AuthenticateAsAsync("SALES_MANAGER");
        var (tenderId, _) = await CreateTenderAsync();

        var template = await Client.GetAsync($"/api/tenders/{tenderId}/estimates/template");
        template.StatusCode.Should().Be(HttpStatusCode.OK);
        var templateBytes = await template.Content.ReadAsByteArrayAsync();
        templateBytes.Should().StartWith(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(templateBytes[Encoding.UTF8.GetPreamble().Length..])
            .Should().StartWith("ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note");

        var import = await ImportAsync(tenderId, ValidCsv("HM-01"));
        import.StatusCode.Should().Be(HttpStatusCode.Created);
        var imported = await ReadJsonAsync(import);
        var revision = imported.GetProperty("revision");
        var revisionId = revision.GetProperty("id").GetInt32();
        revision.GetProperty("versionNumber").GetInt32().Should().Be(1);
        revision.GetProperty("grandBidTotal").GetDecimal().Should().Be(330m);
        revision.GetProperty("sourceSha256").GetString().Should().HaveLength(64);

        var submit = await Client.PostAsync($"/api/tenders/{tenderId}/estimates/{revisionId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(submit)).GetProperty("status").GetString().Should().Be("Submitted");

        await AuthenticateAsAsync("SALE");
        var forbidden = await Client.PostAsJsonAsync(
            $"/api/tenders/{tenderId}/estimates/{revisionId}/approve", new { note = "Không được phép" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WithDbAsync(db => db.TenderEstimateRevisions.AsNoTracking()
            .SingleAsync(item => item.Id == revisionId))).Status.Should().Be(TenderEstimateRevisionStatus.Submitted);

        await AuthenticateAsAsync("SALES_MANAGER");
        var approve = await Client.PostAsJsonAsync(
            $"/api/tenders/{tenderId}/estimates/{revisionId}/approve", new { note = "Đồng ý" });
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await ReadJsonAsync(approve);
        approved.GetProperty("status").GetString().Should().Be("Approved");
        approved.GetProperty("approvedAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

        var list = await Client.GetAsync($"/api/tenders/{tenderId}/estimates");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(list)).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task InvalidImport_IsAtomicAndDoesNotConsumeVersion()
    {
        await AuthenticateAsAsync("SALES_MANAGER");
        var (tenderId, _) = await CreateTenderAsync();
        const string invalidCsv = "ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\nA,Hạng mục,m2,1,10,20,10,\nA,Hạng mục,m2,0,10,20,8,";

        var invalid = await ImportAsync(tenderId, invalidCsv);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.TenderEstimateRevisions.CountAsync(item => item.TenderId == tenderId))).Should().Be(0);

        var valid = await ImportAsync(tenderId, ValidCsv("A"));
        valid.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(valid)).GetProperty("revision").GetProperty("versionNumber").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TenderSubmission_RequiresCompletedChecklistAndApprovedEstimate()
    {
        await AuthenticateAsAsync("SALES_MANAGER");
        var (tenderId, _) = await CreateTenderAsync();

        var rejected = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/transition", new { status = "Submitted" });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Tenders.AsNoTracking().SingleAsync(item => item.Id == tenderId)))
            .Status.Should().Be(TenderStatus.Preparing);

        var import = await ImportAsync(tenderId, ValidCsv("A"));
        var revisionId = (await ReadJsonAsync(import)).GetProperty("revision").GetProperty("id").GetInt32();
        await Client.PostAsync($"/api/tenders/{tenderId}/estimates/{revisionId}/submit", null);
        await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/estimates/{revisionId}/approve", new { note = "Đạt" });
        await WithDbAsync(async db =>
        {
            var items = await db.TenderChecklistItems.Where(item => item.TenderId == tenderId).ToListAsync();
            items.ForEach(item => item.Status = TenderChecklistItemStatus.Done);
            await db.SaveChangesAsync();
        });

        var submitted = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/transition", new { status = "Submitted" });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(submitted)).GetProperty("status").GetString().Should().Be("Submitted");

        var terminalUpdate = await Client.PutAsJsonAsync($"/api/tenders/{tenderId}", new
        {
            name = "Không đổi",
            submissionDeadline = DateTime.UtcNow.AddDays(20),
            note = "Submitted remains non-terminal",
        });
        terminalUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WonTransition_RequiresSubmittedTenderAndSameCustomerOpportunity()
    {
        await AuthenticateAsAsync("SALES_MANAGER");
        var (tenderId, _) = await CreateTenderAsync();
        var otherCustomerId = await CreateCustomerAsync();
        var opportunity = await Client.PostAsJsonAsync("/api/opportunities", new
        {
            name = "Cơ hội khách hàng khác",
            customerId = otherCustomerId,
            estimatedValue = 1_000_000m,
            winProbability = 30,
        });
        opportunity.EnsureSuccessStatusCode();
        var opportunityId = (await ReadJsonAsync(opportunity)).GetProperty("id").GetInt32();
        await WithDbAsync(async db =>
        {
            var tender = await db.Tenders.SingleAsync(item => item.Id == tenderId);
            tender.Status = TenderStatus.Submitted;
            await db.SaveChangesAsync();
        });

        var response = await Client.PostAsJsonAsync($"/api/tenders/{tenderId}/transition", new
        {
            status = "Won",
            opportunityId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await WithDbAsync(db => db.Tenders.AsNoTracking().SingleAsync(item => item.Id == tenderId));
        unchanged.Status.Should().Be(TenderStatus.Submitted);
        unchanged.WonOpportunityId.Should().BeNull();
    }

    private Task AuthenticateAsAsync(string role) =>
        AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private async Task<(int TenderId, int CustomerId)> CreateTenderAsync()
    {
        var customerId = await CreateCustomerAsync();
        var response = await Client.PostAsJsonAsync("/api/tenders", new
        {
            name = "Tender estimate " + Guid.NewGuid().ToString("N")[..6],
            customerId,
            submissionDeadline = DateTime.UtcNow.AddDays(14),
        });
        response.EnsureSuccessStatusCode();
        return ((await ReadJsonAsync(response)).GetProperty("id").GetInt32(), customerId);
    }

    private async Task<int> CreateCustomerAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Estimate customer " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Estimate contact",
                phone = "0933" + Random.Shared.Next(100000, 999999),
                isPrimary = true,
            },
        });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }

    private async Task<HttpResponseMessage> ImportAsync(int tenderId, string csv)
    {
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", "estimate.csv");
        return await Client.PostAsync($"/api/tenders/{tenderId}/estimates/import", multipart);
    }

    private static string ValidCsv(string itemCode) =>
        $"ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note\r\n{itemCode},Hạng mục,m2,2,100,150,10,Ghi chú\r\n";
}
