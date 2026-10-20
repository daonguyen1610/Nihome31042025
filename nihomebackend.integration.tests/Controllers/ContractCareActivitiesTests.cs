using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ContractCareActivitiesTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<int> SeedContractAsync(string ownerRole = "SALE", ContractDirection direction = ContractDirection.Upstream,
        int? customerId = null)
    {
        return await WithDbAsync(async db =>
        {
            var phone = ownerRole == "SUPER_ADMIN" ? TestDataSeeder.SuperAdminPhone : TestDataSeeder.BusinessRolePhonesByCode[ownerRole];
            var ownerId = await db.Users.Where(user => user.PhoneNumber == phone).Select(user => user.Id).SingleAsync();
            var customer = customerId.HasValue ? await db.Customers.SingleAsync(item => item.Id == customerId) : new Customer
            {
                Name = "An Phú factory customer-care test",
                Type = CustomerType.Company,
                SourceCode = "marketing",
                OwnerUserId = ownerId,
            };
            var contract = new Contract
            {
                ContractNumber = $"CARE-{Guid.NewGuid():N}",
                Customer = customer,
                Direction = direction,
                Type = direction == ContractDirection.Upstream ? ContractType.DesignAndBuild : ContractType.Supply,
                OwnerUserId = ownerId,
                Value = 1_500_000_000m,
                Status = ContractStatus.Draft,
                CreatedByUserId = ownerId,
                UpdatedByUserId = ownerId,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            return contract.Id;
        });
    }

    private async Task LoginAsync(string role = "SALE") =>
        await AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private Task<HttpResponseMessage> AddAsync(int id, object payload, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/contracts/{id}/care-activities")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        return Client.SendAsync(request);
    }

    [Fact]
    public async Task Owner_RecordsInteractionsWithUtcAuditAndContractIsolation()
    {
        var contractId = await SeedContractAsync();
        var customerId = await WithDbAsync(db => db.Contracts.Where(item => item.Id == contractId).Select(item => item.CustomerId).SingleAsync());
        var otherId = await SeedContractAsync(customerId: customerId);
        await LoginAsync();
        var empty = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{contractId}/care-activities"));
        empty.GetProperty("total").GetInt32().Should().Be(0);
        foreach (var type in new[] { "Call", "Email", "Meeting", "Note" })
        {
            var response = await AddAsync(contractId, new
            {
                type,
                content = $"  An Phú owner: {type} about the progress meeting.  ",
                occurredAt = "2026-10-18T10:00:00+07:00",
                createdByUserId = -1,
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var activity = await ReadJsonAsync(response);
            activity.GetProperty("content").GetString().Should().Be($"An Phú owner: {type} about the progress meeting.");
            activity.GetProperty("occurredAt").GetDateTime().Should().Be(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc));
            activity.GetProperty("createdByUserId").GetInt32().Should().BePositive();
            activity.GetProperty("createdByName").GetString().Should().NotBeNullOrEmpty();
        }
        var list = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{contractId}/care-activities?page=2&pageSize=2"));
        list.GetProperty("total").GetInt32().Should().Be(4);
        list.GetProperty("items").GetArrayLength().Should().Be(2);
        list.GetProperty("items")[0].GetProperty("type").GetString().Should().Be("Email");
        list.GetProperty("items")[1].GetProperty("type").GetString().Should().Be("Call");
        var other = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{otherId}/care-activities"));
        other.GetProperty("total").GetInt32().Should().Be(0);
        var persisted = await WithDbAsync(db => db.ContractCareActivities.Where(item => item.ContractId == contractId).CountAsync());
        persisted.Should().Be(4);
    }

    [Fact]
    public async Task Retry_ReplaysOnceAndRechecksOwnerScope()
    {
        var id = await SeedContractAsync();
        await LoginAsync();
        var payload = new { type = "Call", content = "Confirm factory inspection meeting." };
        var key = Guid.NewGuid().ToString("N");
        var first = await AddAsync(id, payload, key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var replay = await AddAsync(id, payload, key);
        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replay.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(1);
        (await AddAsync(id, new { type = "Note", content = "Changed payload" }, key)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == id);
            contract.OwnerUserId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.SuperAdminPhone).Select(user => user.Id).SingleAsync();
            await db.SaveChangesAsync();
        });
        (await AddAsync(id, payload, key)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(1);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"content\":\"Missing type\"}")]
    [InlineData("{\"type\":\"Call\",\"content\":\"   \"}")]
    [InlineData("{\"type\":\"LegalRepresentativeAssigned\",\"content\":\"Wrong kind\"}")]
    [InlineData("{\"type\":99,\"content\":\"Wrong kind\"}")]
    [InlineData("{\"type\":\"Note\",\"content\":\"Date\",\"occurredAt\":\"yesterday\"}")]
    public async Task InvalidEntry_IsRejectedWithoutPersistence(string json)
    {
        var id = await SeedContractAsync();
        await LoginAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(json);
        (await AddAsync(id, payload)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ContractCareActivities.AnyAsync(item => item.ContractId == id))).Should().BeFalse();
    }

    [Fact]
    public async Task ContentLengthBoundaryAndMissingKey_AreEnforced()
    {
        var id = await SeedContractAsync();
        await LoginAsync();
        (await AddAsync(id, new { type = "Note", content = new string('a', 4000) })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddAsync(id, new { type = "Note", content = new string('a', 4001) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/care-activities", new { type = "Note", content = "Missing retry key" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(1);
    }

    [Fact]
    public async Task UnauthorizedNonOwnerAndDownstream_DoNotExposeOrMutateHistory()
    {
        var id = await SeedContractAsync("SUPER_ADMIN");
        var downstreamId = await SeedContractAsync(direction: ContractDirection.Downstream);
        (await Client.GetAsync($"/api/contracts/{id}/care-activities")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AddAsync(id, new { type = "Note", content = "Denied" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await LoginAsync("USER");
        (await Client.GetAsync($"/api/contracts/{id}/care-activities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AddAsync(id, new { type = "Note", content = "Denied" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await LoginAsync();
        (await Client.GetAsync($"/api/contracts/{id}/care-activities")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AddAsync(id, new { type = "Note", content = "Denied" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/contracts/{downstreamId}/care-activities")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AddAsync(downstreamId, new { type = "Note", content = "Denied" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await LoginAsync("SALES_MANAGER");
        (await AddAsync(id, new { type = "Meeting", content = "CM coordination with the owner." })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(1);
        (await WithDbAsync(db => db.ContractCareActivities.AnyAsync(item => item.ContractId == downstreamId))).Should().BeFalse();
    }

    [Fact]
    public async Task DeletionImpact_IncludesHistoryAndRejectsStalePlan()
    {
        var id = await SeedContractAsync();
        await LoginAsync("SUPER_ADMIN");
        await AddAsync(id, new { type = "Note", content = "First owner follow-up." });
        var plan = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/deletion-impact"));
        var group = plan.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("key").GetString() == "contract.careActivities");
        group.GetProperty("count").GetInt32().Should().Be(1);
        group.GetProperty("action").GetString().Should().Be("Delete");
        await AddAsync(id, new { type = "Call", content = "New follow-up after deletion preview." });
        var contract = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{id}")
        {
            Content = JsonContent.Create(new
            {
                confirmation = plan.GetProperty("requiredConfirmation").GetString(),
                planToken = plan.GetProperty("planToken").GetString(),
                rowVersion = contract.GetProperty("rowVersion").GetString(),
            }),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(2);
        (await WithDbAsync(db => db.Contracts.AnyAsync(item => item.Id == id))).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DirectionChange_IsBlockedOnlyWhenCareHistoryExists(bool hasHistory)
    {
        var id = await SeedContractAsync();
        await LoginAsync();
        var vendorId = await WithDbAsync(async db =>
        {
            var contract = await db.Contracts.SingleAsync(item => item.Id == id);
            var project = new OperationalProject
            {
                Code = $"CARE-LOCK-{Guid.NewGuid():N}"[..40],
                Name = "An Phú contract direction validation",
                CustomerId = contract.CustomerId,
                CreatedByUserId = contract.OwnerUserId!.Value,
            };
            var vendor = new Vendor
            {
                VendorCode = $"CARE-NCC-{Guid.NewGuid():N}"[..30],
                CompanyName = "An Phú electrical equipment supply",
                VendorType = VendorType.Supplier,
                Email = "equipment@example.test",
                CreatedByUserId = contract.OwnerUserId.Value,
            };
            db.AddRange(project, vendor);
            await db.SaveChangesAsync();
            contract.OperationalProjectId = project.Id;
            await db.SaveChangesAsync();
            return vendor.Id;
        });
        if (hasHistory)
            (await AddAsync(id, new { type = "Call", content = "Confirm the factory owner's progress meeting." }))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        var before = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
        var payload = JsonNode.Parse(before.GetRawText())!.AsObject();
        payload["direction"] = "Downstream";
        payload["type"] = "Supply";
        payload["vendorId"] = vendorId;
        var response = await Client.PutAsJsonAsync($"/api/contracts/{id}", payload);
        response.StatusCode.Should().Be(hasHistory ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        if (hasHistory)
        {
            (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("CONTRACT_CARE_DIRECTION_LOCKED");
            var after = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
            foreach (var field in new[] { "direction", "type", "vendorId", "operationalProjectId", "customerId", "value", "rowVersion" })
                after.GetProperty(field).GetRawText().Should().Be(before.GetProperty(field).GetRawText());
            var history = await Client.GetAsync($"/api/contracts/{id}/care-activities");
            history.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ReadJsonAsync(history)).GetProperty("total").GetInt32().Should().Be(1);
        }
        else
        {
            (await ReadJsonAsync(response)).GetProperty("direction").GetString().Should().Be("Downstream");
        }
    }

    [Theory]
    [InlineData("ACCOUNTANT", false, HttpStatusCode.Forbidden)]
    [InlineData("PROCUREMENT", true, HttpStatusCode.Forbidden)]
    [InlineData("SALE", true, HttpStatusCode.NotFound)]
    public async Task ProjectAndFinanceReaders_DoNotGainCareWriteAuthority(string role, bool isMember, HttpStatusCode writeStatus)
    {
        var id = await SeedContractAsync("SUPER_ADMIN");
        await LoginAsync("SUPER_ADMIN");
        (await AddAsync(id, new { type = "Note", content = "Owner meeting for factory contract." })).StatusCode.Should().Be(HttpStatusCode.Created);
        if (isMember)
        {
            await WithDbAsync(async db =>
            {
                var contract = await db.Contracts.SingleAsync(item => item.Id == id);
                var memberId = await db.Users.Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode[role])
                    .Select(user => user.Id).SingleAsync();
                var project = new OperationalProject
                {
                    Code = $"CARE-PJ-{Guid.NewGuid():N}",
                    Name = "An Phú project-scoped contract care",
                    CustomerId = contract.CustomerId,
                    CreatedByUserId = contract.OwnerUserId!.Value,
                };
                db.OperationalProjects.Add(project);
                await db.SaveChangesAsync();
                contract.OperationalProjectId = project.Id;
                db.OperationalProjectMembers.Add(new OperationalProjectMember
                {
                    OperationalProjectId = project.Id,
                    UserId = memberId,
                    Position = "Contract coordination",
                    StartedAt = DateTime.UtcNow,
                    CreatedByUserId = contract.OwnerUserId.Value,
                    UpdatedByUserId = contract.OwnerUserId.Value,
                });
                await db.SaveChangesAsync();
            });
        }
        await LoginAsync(role);
        var response = await Client.GetAsync($"/api/contracts/{id}/care-activities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("total").GetInt32().Should().Be(1);
        (await AddAsync(id, new { type = "Call", content = "Read permission must not allow writes." })).StatusCode.Should().Be(writeStatus);
        (await WithDbAsync(db => db.ContractCareActivities.CountAsync(item => item.ContractId == id))).Should().Be(1);
    }
}
