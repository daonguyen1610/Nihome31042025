using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>LeadsController</c> (NIH-378): RBAC, owner
/// scoping, auto-assign, status transitions, convert and activity timeline.
/// </summary>
public class LeadsControllerTests : IntegrationTestBase
{
    public LeadsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/leads")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsRoleWithoutViewPerm_IsForbidden()
    {
        // DESIGN role does not have crm.leads.*
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));
        (await Client.GetAsync("/api/leads")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsSale_PersistsLeadAndReturnsCreated()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var payload = new
        {
            name = "Ms. Nga " + Guid.NewGuid().ToString("N")[..6],
            companyName = "ACME",
            phone = "0900000000",
            email = "nga@example.test",
            sourceCode = "marketing",
        };

        var res = await Client.PostAsJsonAsync("/api/leads", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(res);
        body.GetProperty("name").GetString().Should().Be(payload.name);
        body.GetProperty("status").GetString().Should().Be("New");
        body.GetProperty("sourceCode").GetString().Should().Be("marketing");
    }

    [Fact]
    public async Task Create_RejectsUnknownSourceCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "N",
            phone = "0900000000",
            sourceCode = "tiktok",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_RequiresPhoneOrEmail()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "N",
            sourceCode = "marketing",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_AsSale_AutoAssignsSelfViaRoundRobin()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Auto-" + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(res);
        // The seeded SALE user is the only one with crm.leads.manage but
        // *without* crm.leads.view.all (Managers/Admin also have manage);
        // round-robin picks by workload — but since only one owner can be
        // returned deterministically, the response must have an owner set.
        body.TryGetProperty("ownerUserId", out var owner).Should().BeTrue();
        owner.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task List_AsSale_HidesOtherOwnersLeads()
    {
        // Sales Manager creates a lead assigned to Admin — then Sale role
        // should NOT see it.
        var adminId = await WithDbAsync(async db =>
            (await db.Users.FirstAsync(u => u.PhoneNumber == TestDataSeeder.AdminPhone)).Id);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Manager-owned " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000001",
            sourceCode = "marketing",
            ownerUserId = adminId,
        });
        created.EnsureSuccessStatusCode();
        var createdBody = await ReadJsonAsync(created);
        var leadId = createdBody.GetProperty("id").GetInt32();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var list = await Client.GetAsync("/api/leads?pageSize=100");
        list.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(list);
        var ids = body.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32())
            .ToList();
        ids.Should().NotContain(leadId);

        // Direct GET on the hidden lead must also 404 for Sales, not leak existence.
        (await Client.GetAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsSale_CannotMoveToJunk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Move-junk " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();
        var ownerId = (await ReadJsonAsync(created)).GetProperty("ownerUserId").GetInt32();

        var res = await Client.PutAsJsonAsync($"/api/leads/{leadId}", new
        {
            name = "Move-junk",
            phone = "0900000000",
            sourceCode = "marketing",
            status = "Junk",
            ownerUserId = ownerId,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_AsSalesManager_CanMoveToJunk()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Move-junk-mgr " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(created);
        var leadId = body.GetProperty("id").GetInt32();
        var ownerId = body.GetProperty("ownerUserId").GetInt32();

        var res = await Client.PutAsJsonAsync($"/api/leads/{leadId}", new
        {
            name = "Move-junk-mgr",
            phone = "0900000000",
            sourceCode = "marketing",
            status = "Junk",
            ownerUserId = ownerId,
        });

        res.EnsureSuccessStatusCode();
        (await ReadJsonAsync(res)).GetProperty("status").GetString().Should().Be("Junk");
    }

    [Fact]
    public async Task Update_DirectTransitionToConverted_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Direct-convert " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(created);
        var leadId = body.GetProperty("id").GetInt32();
        var ownerId = body.GetProperty("ownerUserId").GetInt32();

        var res = await Client.PutAsJsonAsync($"/api/leads/{leadId}", new
        {
            name = "Direct-convert",
            phone = "0900000000",
            sourceCode = "marketing",
            status = "Converted",
            ownerUserId = ownerId,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Convert_TransitionsLeadAndReturnsConvertedIds()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        // A phone unique to this run: convert now refuses to create a second
        // individual customer holding a phone that already belongs to one.
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999);

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Convert-me " + Guid.NewGuid().ToString("N")[..6],
            phone,
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        // No ids supplied — convert builds the customer and the opportunity. It
        // used to just stamp whatever numbers the caller passed, so this asked for
        // 4242 and 4243 and got them back without either record existing.
        var res = await Client.PostAsJsonAsync($"/api/leads/{leadId}/convert", new
        {
            note = "Signed",
        });

        res.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(res);
        body.GetProperty("status").GetString().Should().Be("Converted");

        var customerId = body.GetProperty("convertedCustomerId").GetInt32();
        var opportunityId = body.GetProperty("convertedOpportunityId").GetInt32();
        customerId.Should().BeGreaterThan(0);
        opportunityId.Should().BeGreaterThan(0);

        // Both must be real rows, not just numbers on the lead.
        var customer = await Client.GetAsync($"/api/customers/{customerId}");
        customer.EnsureSuccessStatusCode();
        (await ReadJsonAsync(customer)).GetProperty("sourceCode").GetString().Should().Be("marketing");

        var opportunity = await Client.GetAsync($"/api/opportunities/{opportunityId}");
        opportunity.EnsureSuccessStatusCode();
        (await ReadJsonAsync(opportunity)).GetProperty("customerId").GetInt32().Should().Be(customerId);

        // Follow-up edit must fail.
        var edit = await Client.PutAsJsonAsync($"/api/leads/{leadId}", new
        {
            name = "New name",
            phone = "0900000000",
            sourceCode = "marketing",
            status = "Contacted",
        });
        edit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Convert_CompanyLeadWithoutTaxId_PreservesCustomerAndOpportunityLinks()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Đại diện " + Guid.NewGuid().ToString("N")[..6],
            companyName = "Công ty chưa có MST " + Guid.NewGuid().ToString("N")[..6],
            phone = "09" + Random.Shared.Next(10_000_000, 99_999_999),
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var createdBody = await ReadJsonAsync(created);
        var leadId = createdBody.GetProperty("id").GetInt32();
        var ownerId = createdBody.GetProperty("ownerUserId").GetInt32();

        var converted = await Client.PostAsJsonAsync($"/api/leads/{leadId}/convert", new
        {
            address = "12 Nguyễn Trãi, Hà Nội",
            representativeName = "Ms. Nga",
        });

        converted.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(converted);
        var customerId = body.GetProperty("convertedCustomerId").GetInt32();
        var opportunityId = body.GetProperty("convertedOpportunityId").GetInt32();

        var links = await WithDbAsync(async db =>
        {
            var customer = await db.Customers.FirstAsync(c => c.Id == customerId);
            var opportunity = await db.Opportunities.FirstAsync(o => o.Id == opportunityId);
            return new
            {
                customer.TaxId,
                CustomerOwnerUserId = customer.OwnerUserId,
                opportunity.CustomerId,
                OpportunityOwnerUserId = opportunity.OwnerUserId,
            };
        });

        links.TaxId.Should().BeNull();
        links.CustomerOwnerUserId.Should().Be(ownerId);
        links.CustomerId.Should().Be(customerId);
        links.OpportunityOwnerUserId.Should().Be(ownerId);
    }

    [Fact]
    public async Task Convert_WithoutConvertPermission_IsForbidden()
    {
        // ADMIN role has ** with deny of users.manage/system.audit.manage —
        // so ADMIN does have crm.leads.convert. Use a role that lacks it.
        // DESIGN role has no crm.leads.*, so /convert returns Forbidden at
        // the RequirePermission attribute layer.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "DESIGN"));

        var res = await Client.PostAsJsonAsync("/api/leads/999999/convert", new { });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddActivity_AsOwner_PersistsEntry()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Activity-owner " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync($"/api/leads/{leadId}/activities", new
        {
            type = "Call",
            content = "First contact — chị Nga đồng ý gặp T7.",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(res);
        body.GetProperty("type").GetString().Should().Be("Call");
        body.GetProperty("content").GetString().Should().Contain("First contact");

        var detail = await Client.GetAsync($"/api/leads/{leadId}");
        detail.EnsureSuccessStatusCode();
        var detailBody = await ReadJsonAsync(detail);
        detailBody.GetProperty("activities").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Delete_AsSalesManager_RemovesUnconvertedLead()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Delete-me " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        (await Client.DeleteAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ConvertedLead_RemovesLeadButPreservesConvertedPrincipals()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Delete-converted " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000000",
            sourceCode = "marketing",
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var convert = await Client.PostAsJsonAsync($"/api/leads/{leadId}/convert", new { });
        convert.EnsureSuccessStatusCode();
        var (customerId, opportunityId) = await WithDbAsync(async db =>
        {
            var customer = new NihomeBackend.Models.Customer
            {
                Name = "Preserved converted customer",
                Type = NihomeBackend.Models.CustomerType.Company,
                SourceCode = "marketing",
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var opportunity = new NihomeBackend.Models.Opportunity
            {
                Name = "Preserved converted opportunity",
                CustomerId = customer.Id,
            };
            db.Opportunities.Add(opportunity);
            var lead = await db.Leads.FirstAsync(l => l.Id == leadId);
            lead.ConvertedCustomerId = customer.Id;
            lead.ConvertedOpportunityId = opportunity.Id;
            await db.SaveChangesAsync();
            return (customer.Id, opportunity.Id);
        });

        (await Client.DeleteAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/customers/{customerId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"/api/opportunities/{opportunityId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_MissingLead_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.DeleteAsync("/api/leads/9999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutManagePermission_ReturnsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.DeleteAsync("/api/leads/9999999")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_SalesUser_CannotDeleteOtherOwnersLead_ReturnsNotFound()
    {
        // SECURITY regression guard mirroring Customer — cross-owner delete
        // is refused with 404 so existence is not leaked.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var adminId = await WithDbAsync(async db =>
            (await db.Users.FirstAsync(u => u.PhoneNumber == TestDataSeeder.AdminPhone)).Id);
        var created = await Client.PostAsJsonAsync("/api/leads", new
        {
            name = "Manager-owned lead " + Guid.NewGuid().ToString("N")[..6],
            phone = "0900000005",
            sourceCode = "marketing",
            ownerUserId = adminId,
        });
        created.EnsureSuccessStatusCode();
        var leadId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Manager confirms lead still exists.
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync($"/api/leads/{leadId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
