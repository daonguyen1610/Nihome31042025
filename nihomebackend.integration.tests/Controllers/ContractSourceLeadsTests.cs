using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Responses;
using NihomeBackend.Models.Rbac;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class ContractSourceLeadsTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task SourceLeads_RequiresBothPermissions()
    {
        (await Client.GetAsync("/api/contracts/1/source-leads")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var grant in new[] { "crm.contracts.view", "crm.leads.view" })
        {
            var user = await CreateUserAsync([grant]);
            await LoginAsync(user.Phone);
            (await Client.GetAsync("/api/contracts/1/source-leads")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task SourceLeads_EnforcesIndependentOwnerScopesAndExactConversion()
    {
        var owner = await CreateUserAsync(["crm.contracts.view", "crm.leads.view"]);
        var other = await CreateUserAsync(["crm.contracts.view", "crm.leads.view"]);
        var fixture = await SeedAsync(owner.Id, other.Id);
        await LoginAsync(owner.Phone);
        var own = await GetAsync(fixture.ContractId);
        own.Select(item => item.Name).Should().Equal("Đầu mối nhà máy Long Thành");
        // Contract portfolio access does not grant access to somebody else's Lead.
        var portfolio = await CreateUserAsync(["crm.contracts.view", "crm.contracts.view.all", "crm.leads.view"]);
        await LoginAsync(portfolio.Phone);
        (await GetAsync(fixture.ContractId)).Should().BeEmpty();
        await LoginAsync(other.Phone);
        (await Client.GetAsync($"/api/contracts/{fixture.ContractId}/source-leads")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync("/api/contracts/2147483647/source-leads")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var leadPortfolio = await CreateUserAsync(["crm.contracts.view", "crm.leads.view", "crm.leads.view.all"]);
        await LoginAsync(leadPortfolio.Phone);
        (await Client.GetAsync($"/api/contracts/{fixture.ContractId}/source-leads")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var all = await Client.GetFromJsonAsync<List<ContractSourceLeadResponse>>($"/api/v1/contracts/{fixture.ContractId}/source-leads");
        all!.Select(item => item.Name).Should().Equal("Đầu mối nhà máy Long Thành", "Đầu mối kỹ thuật Long Thành");
        // No phone, email, notes, or permission-hidden identifiers leak through this DTO.
        var json = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{fixture.ContractId}/source-leads"));
        json[0].EnumerateObject().Select(item => item.Name).Should().BeEquivalentTo("id", "name", "convertedAt");
        (await GetAsync(fixture.DirectId)).Should().BeEmpty();
        (await GetAsync(fixture.DownstreamId)).Should().BeEmpty();
    }

    [Fact]
    public async Task SourceLeads_TracksActualConversionAndStopsAfterUnlink()
    {
        await AuthTestHelper.AuthenticateAsync(Client, AuthTestHelper.LoginAsSuperAdminAsync);
        var leadResponse = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Khảo sát mở rộng nhà xưởng Long Thành",
            email = $"lead-{Guid.NewGuid():N}@example.test",
            sourceCode = "referral",
            segmentCode = "unclassified",
        });
        leadResponse.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(leadResponse);
        var leadId = created.GetProperty("id").GetInt32();
        var conversion = await Client.PostAsJsonAsync($"/api/leads/{leadId}/convert", new
        {
            rowVersion = created.GetProperty("rowVersion").GetString(),
        });
        conversion.EnsureSuccessStatusCode();
        var converted = await ReadJsonAsync(conversion);
        var opportunityId = converted.GetProperty("convertedOpportunityId").GetInt32();
        var ownerId = converted.GetProperty("ownerUserId").GetInt32();
        var projectId = await WithDbAsync(db => db.Opportunities.Where(item => item.Id == opportunityId)
            .Select(item => item.OperationalProjectId).SingleAsync());
        var contractResponse = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId = converted.GetProperty("convertedCustomerId").GetInt32(),
            opportunityId, operationalProjectId = projectId, ownerUserId = ownerId,
            direction = "Upstream", type = "DesignAndBuild", value = 1500000000m,
        });
        contractResponse.EnsureSuccessStatusCode();
        var contractId = (await ReadJsonAsync(contractResponse)).GetProperty("id").GetInt32();
        (await GetAsync(contractId)).Should().ContainSingle(item => item.Id == leadId);
        await WithDbAsync(async db =>
        {
            var lead = await db.Leads.SingleAsync(item => item.Id == leadId);
            lead.Status = LeadStatus.Interested;
            lead.ConvertedOpportunityId = null;
            lead.ConvertedCustomerId = null;
            lead.ConvertedAt = null;
            await db.SaveChangesAsync();
        });
        (await GetAsync(contractId)).Should().BeEmpty();
    }

    private async Task<List<ContractSourceLeadResponse>> GetAsync(int id)
        => (await Client.GetFromJsonAsync<List<ContractSourceLeadResponse>>($"/api/contracts/{id}/source-leads"))!;

    private Task LoginAsync(string phone) => AuthTestHelper.AuthenticateAsync(Client,
        client => AuthTestHelper.LoginAsync(client, phone, TestDataSeeder.DefaultPassword));

    private Task<(int Id, string Phone)> CreateUserAsync(string[] grants) => WithDbAsync(async db =>
    {
        var role = new Role { Code = $"SOURCE_{Guid.NewGuid():N}"[..30], Name = "Contract source test", IsActive = true, InitialPermissionsSeeded = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var permissionIds = await db.Permissions.Where(item => grants.Contains(item.Module + "." + item.Action)).Select(item => item.Id).ToListAsync();
        permissionIds.Count.Should().Be(grants.Length);
        db.RolePermissions.AddRange(permissionIds.Select(id => new RolePermission { RoleId = role.Id, PermissionId = id }));
        var user = new ApplicationUser { PhoneNumber = "09" + Random.Shared.Next(10000000, 99999999),
            FullName = "Kinh doanh dự án Long Thành", Role = UserRole.USER, RoleEntityId = role.Id, IsActive = true };
        user.PasswordHash = new PasswordService().Hash(user, TestDataSeeder.DefaultPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id, user.PhoneNumber!);
    });

    private Task<(int ContractId, int DirectId, int DownstreamId)> SeedAsync(int owner, int other) => WithDbAsync(async db =>
    {
        var customer = new Customer { Name = "Chủ đầu tư Long Thành", SourceCode = "referral", Type = CustomerType.Company };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var project = new OperationalProject { Code = $"PJ-{Guid.NewGuid():N}"[..25], Name = "Mở rộng nhà máy Long Thành", CustomerId = customer.Id };
        db.OperationalProjects.Add(project);
        await db.SaveChangesAsync();
        var opportunity = new Opportunity { Name = "Gói mở rộng nhà máy", CustomerId = customer.Id, OperationalProjectId = project.Id, OwnerUserId = owner };
        var unrelated = new Opportunity { Name = "Gói độc lập cùng khách hàng", CustomerId = customer.Id, OperationalProjectId = project.Id };
        db.Opportunities.AddRange(opportunity, unrelated);
        await db.SaveChangesAsync();
        var time = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Lead Lead(string name, int leadOwner, int? opportunityId, int? customerId, LeadStatus status = LeadStatus.Converted, DateTime? convertedAt = null)
            => new() { Name = name, OwnerUserId = leadOwner, SourceCode = "referral", Email = $"{Guid.NewGuid():N}@example.test",
                Status = status, ConvertedAt = convertedAt ?? time, ConvertedOpportunityId = opportunityId, ConvertedCustomerId = customerId };
        db.Leads.AddRange(
            Lead("Đầu mối nhà máy Long Thành", owner, opportunity.Id, customer.Id),
            Lead("Đầu mối kỹ thuật Long Thành", other, opportunity.Id, customer.Id, convertedAt: time.AddDays(1)),
            Lead("Lead của cơ hội khác", owner, unrelated.Id, customer.Id),
            Lead("Lead chưa chuyển đổi", owner, opportunity.Id, customer.Id, LeadStatus.Interested),
            Lead("Lead sai khách hàng lịch sử", owner, opportunity.Id, null));
        var missingTimestamp = Lead("Thiếu thời điểm chuyển đổi", owner, opportunity.Id, customer.Id);
        missingTimestamp.ConvertedAt = null;
        db.Leads.Add(missingTimestamp);
        Contract Contract(ContractDirection direction, int? source) => new() { ContractNumber = $"HD-{Guid.NewGuid():N}"[..28], CustomerId = customer.Id,
            OperationalProjectId = project.Id, OpportunityId = source, OwnerUserId = owner, Direction = direction,
            Type = direction == ContractDirection.Upstream ? ContractType.DesignAndBuild : ContractType.Subcontract, Value = 1500000000m };
        var contract = Contract(ContractDirection.Upstream, opportunity.Id);
        var direct = Contract(ContractDirection.Upstream, null);
        var downstream = Contract(ContractDirection.Downstream, opportunity.Id);
        db.Contracts.AddRange(contract, direct, downstream);
        await db.SaveChangesAsync();
        return (contract.Id, direct.Id, downstream.Id);
    });
}
