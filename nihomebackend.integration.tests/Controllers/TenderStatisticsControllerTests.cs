using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;

namespace NihomeBackend.IntegrationTests.Controllers;

public class TenderStatisticsControllerTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Theory]
    [InlineData("statistics")]
    [InlineData("report-preparers")]
    [InlineData("report-customers")]
    public async Task Report_EnforcesAuthenticationAndPermission(string endpoint)
    {
        (await Client.GetAsync($"/api/tenders/{endpoint}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync($"/api/tenders/{endpoint}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("fromDate=2026-01-01")]
    [InlineData("toDate=2026-01-01")]
    [InlineData("fromDate=2026-02-01&toDate=2026-01-01")]
    [InlineData("fromDate=0001-01-01&toDate=2026-01-01")]
    [InlineData("fromDate=invalid&toDate=2026-01-01")]
    [InlineData("customerId=0")]
    [InlineData("preparerUserId=-1")]
    public async Task Report_RejectsInvalidFilters(string query)
    {
        await LoginAsync();
        (await Client.GetAsync($"/api/tenders/statistics?{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.GetAsync($"/api/tenders?submittedOnly=true&{query}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Report_FirstSubmissionCohort_ReconcilesWithPaginatedDrilldown()
    {
        await LoginAsync();
        var fixture = await SeedAsync();
        var query = $"fromDate=2026-08-01&toDate=2026-08-31&customerId={fixture.CustomerId}&preparerUserId={fixture.OwnerId}";
        var report = await Client.GetFromJsonAsync<TenderStatisticsResponse>($"/api/tenders/statistics?{query}");
        report!.SubmittedCount.Should().Be(4);
        report.WonCount.Should().Be(1);
        report.LostCount.Should().Be(1);
        report.PendingCount.Should().Be(1);
        report.CancelledCount.Should().Be(1);
        report.WinRate.Should().Be(25m);
        report.MissingSubmissionCount.Should().Be(1);
        report.CalculatedAt.Kind.Should().Be(DateTimeKind.Utc);

        var list = await ReadJsonAsync(await Client.GetAsync($"/api/tenders?{query}&submittedOnly=true&pageSize=1"));
        list.GetProperty("total").GetInt32().Should().Be(report.SubmittedCount);
        list.GetProperty("items").GetArrayLength().Should().Be(1);
        var won = await ReadJsonAsync(await Client.GetAsync($"/api/tenders?{query}&submittedOnly=true&status=Won"));
        won.GetProperty("total").GetInt32().Should().Be(report.WonCount);
        // Status/list pagination do not change the report denominator.
        var same = await Client.GetFromJsonAsync<TenderStatisticsResponse>($"/api/tenders/statistics?{query}&status=Won&pageSize=1");
        same!.SubmittedCount.Should().Be(4);
        same.WinRate.Should().Be(25m);

        // Closing after the submission month still contributes to that submission cohort.
        await WithDbAsync(async db =>
        {
            var pending = await db.Tenders.SingleAsync(t => t.Id == fixture.PendingId);
            pending.Status = TenderStatus.Won;
            pending.ClosedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        });
        var updated = await Client.GetFromJsonAsync<TenderStatisticsResponse>($"/api/tenders/statistics?{query}");
        updated!.WinRate.Should().Be(50m);
        updated.SubmittedCount.Should().Be(4);

        var options = await Client.GetFromJsonAsync<List<TenderAssigneeOptionResponse>>("/api/tenders/report-preparers");
        options!.Count(p => p.Id == fixture.OwnerId).Should().Be(1);
        var customers = await Client.GetFromJsonAsync<List<TenderCustomerOptionResponse>>("/api/tenders/report-customers");
        customers!.Count(p => p.Id == fixture.CustomerId).Should().Be(1);
    }

    [Fact]
    public async Task Report_DistinguishesEmptyFromZeroAndSupportsAliases()
    {
        await LoginAsync();
        var fixture = await SeedAsync();
        var empty = await Client.GetFromJsonAsync<TenderStatisticsResponse>(
            $"/api/v1/tenders/statistics?customerId={fixture.CustomerId}&fromDate=2025-01-01&toDate=2025-01-31");
        empty!.SubmittedCount.Should().Be(0);
        empty.WinRate.Should().BeNull();
        var zero = await Client.GetFromJsonAsync<TenderStatisticsResponse>(
            $"/api/tenders/statistics?customerId={fixture.CustomerId}&preparerUserId={fixture.OwnerId}&fromDate=2026-08-15&toDate=2026-08-15");
        zero!.SubmittedCount.Should().Be(1);
        zero.WinRate.Should().Be(0m);
    }

    private Task LoginAsync() => AuthTestHelper.AuthenticateAsync(Client,
        c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

    private Task<(int CustomerId, int OwnerId, int PendingId)> SeedAsync() => WithDbAsync(async db =>
    {
        var owner = await db.Users.OrderBy(u => u.Id).FirstAsync();
        var other = await db.Users.OrderBy(u => u.Id).Skip(1).FirstAsync();
        var customer = new Customer { Name = "Chủ đầu tư nhà máy cơ khí Long Thành", Type = CustomerType.Company, SourceCode = "referral" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject { Code = $"PJ-STAT-{Guid.NewGuid():N}"[..25], Name = "Nhà máy cơ khí Long Thành", CustomerId = customer.Id };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();

        async Task<Tender> Add(string name, TenderStatus status, DateTime? first, int? userId = null, bool second = false)
        {
            var tender = new Tender
            {
                Code = $"TD-STAT-{Guid.NewGuid():N}"[..25],
                Name = name,
                CustomerId = customer.Id,
                OperationalProjectId = project.Id,
                PreparerUserId = userId ?? owner.Id,
                InvitationReceivedByUserId = owner.Id,
                InvitationSource = "Email chủ đầu tư",
                InvitationReceivedAt = new DateTime(2026, 7, 1),
                SubmissionDeadline = new DateTime(2026, 8, 15),
                Status = status,
                SubmittedAt = first,
                ClosedAt = status is TenderStatus.Won or TenderStatus.Lost ? new DateTime(2026, 10, 1) : null,
            };
            db.Tenders.Add(tender);
            await db.SaveChangesAsync();
            if (first.HasValue)
            {
                db.TenderSubmissions.Add(new TenderSubmission
                {
                    TenderId = tender.Id,
                    Sequence = 1,
                    SubmittedAt = first.Value,
                    SubmittedByUserId = owner.Id,
                    SubmissionChannel = "Email",
                    LegacyUnknown = status == TenderStatus.Lost
                });
                if (second) db.TenderSubmissions.Add(new TenderSubmission
                {
                    TenderId = tender.Id,
                    Sequence = 2,
                    SubmittedAt = new DateTime(2026, 8, 20),
                    SubmittedByUserId = owner.Id,
                    SubmissionChannel = "Email"
                });
                await db.SaveChangesAsync();
            }
            return tender;
        }

        // Vietnam August starts at July 31 17:00 UTC and ends at August 31 17:00 UTC.
        await Add("Gói kết cấu nhà xưởng", TenderStatus.Won, new DateTime(2026, 7, 31, 17, 0, 0, DateTimeKind.Utc), second: true);
        await Add("Gói điện động lực", TenderStatus.Lost, new DateTime(2026, 8, 31, 16, 59, 59, DateTimeKind.Utc));
        var pending = await Add("Gói hệ thống thông gió", TenderStatus.Submitted, new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
        await Add("Gói cảnh quan đã nộp rồi hủy", TenderStatus.Cancelled, new DateTime(2026, 8, 10));
        await Add("Gói chốt tháng trước", TenderStatus.Won, new DateTime(2026, 7, 31, 16, 59, 59), second: true);
        await Add("Gói đầu tháng sau", TenderStatus.Won, new DateTime(2026, 8, 31, 17, 0, 0));
        await Add("Gói của nhân viên khác", TenderStatus.Won, new DateTime(2026, 8, 10), other.Id);
        await Add("Gói đang chuẩn bị", TenderStatus.Preparing, null);
        await Add("Gói hủy trước khi nộp", TenderStatus.Cancelled, null);
        await Add("Hồ sơ lịch sử thiếu bằng chứng nộp", TenderStatus.Won, null);
        return (customer.Id, owner.Id, pending.Id);
    });
}
