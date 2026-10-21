using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.IntegrationTests.Infrastructure;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

public sealed class ContractContinuationsTests(NihomeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<Contract> SourceAsync(ContractStatus status = ContractStatus.Signed, bool otherOwner = false)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        return await WithDbAsync(async db =>
        {
            var owner = await db.Users.FirstAsync(x => x.PhoneNumber == (otherOwner ? "0911000005" : "0911000003"));
            var customer = new Customer { Name = "Nhà máy An Phú mở rộng " + Guid.NewGuid().ToString("N"), OwnerUserId = owner.Id };
            var project = new OperationalProject { Code = "FOLLOW-" + Guid.NewGuid().ToString("N"), Name = "Mở rộng xưởng An Phú", Customer = customer, CreatedByUserId = owner.Id };
            var contract = new Contract
            {
                ContractNumber = "HD-" + Guid.NewGuid().ToString("N"),
                Customer = customer,
                OperationalProject = project,
                OwnerUserId = owner.Id,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Design,
                Status = status,
                SignedDate = DateTime.UtcNow.AddMonths(-6),
                StartDate = DateTime.UtcNow.AddMonths(-5),
                EndDate = DateTime.UtcNow.AddMonths(1),
                Value = 500000000
            };
            db.Add(contract); await db.SaveChangesAsync(); return contract;
        });
    }

    private static JsonObject Body(Contract source, string kind = "Renewal") => new()
    {
        ["customerId"] = source.CustomerId,
        ["operationalProjectId"] = source.OperationalProjectId,
        ["sourceContractId"] = source.Id,
        ["sourceContractRowVersion"] = Convert.ToBase64String(source.RowVersion),
        ["continuationKind"] = kind,
        ["continuationReason"] = "Tiếp tục thiết kế khu nhà điều hành An Phú",
        ["direction"] = "Upstream",
        ["type"] = "Design",
        ["status"] = "Draft",
        ["value"] = 150000000,
    };

    private async Task<HttpResponseMessage> PostAsync(string route, JsonObject body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await Client.SendAsync(request);
    }

    [Theory]
    [InlineData("Renewal")]
    [InlineData("Expansion")]
    public async Task Create_PreservesSourceAndStartsSeparateDraft_WithReplayAndDeletionBlock(string kind)
    {
        var source = await SourceAsync();
        var body = Body(source, kind); var key = Guid.NewGuid().ToString();
        var created = await PostAsync("/api/contracts", body, key);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var result = await ReadJsonAsync(created); var id = result.GetProperty("id").GetInt32();
        result.GetProperty("status").GetString().Should().Be("Draft");
        result.GetProperty("sourceContractId").GetInt32().Should().Be(source.Id);
        result.GetProperty("contractNumber").GetString().Should().NotBe(source.ContractNumber);
        result.GetProperty("signedDate").ValueKind.Should().Be(JsonValueKind.Null);
        (await ReadJsonAsync(await PostAsync("/api/contracts", body, key))).GetProperty("id").GetInt32().Should().Be(id);
        var related = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}/continuations"));
        related.GetProperty("contracts").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetInt32() == id);
        var back = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}/continuations"));
        back.GetProperty("contracts")[0].GetProperty("isSource").GetBoolean().Should().BeTrue();
        foreach (var root in new[] { source.Id, id })
        {
            var impact = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{root}/deletion-impact"));
            impact.GetRawText().Should().Contain("contract.continuationContracts").And.Contain("Block");
        }
        await WithDbAsync(async db =>
        {
            var unchanged = await db.Contracts.SingleAsync(x => x.Id == source.Id);
            unchanged.Status.Should().Be(ContractStatus.Signed);
            unchanged.Value.Should().Be(source.Value);
            (await db.ContractPaymentMilestones.CountAsync(x => x.ContractId == id)).Should().Be(0);
            (await db.ContractReminders.CountAsync(x => x.ContractId == id)).Should().Be(0);
        });
    }

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.Cancelled)]
    public async Task InvalidSourceStatus_CreatesNothing(ContractStatus status)
    {
        var source = await SourceAsync(status);
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.SourceContractId == source.Id)).Should().Be(0));
    }

    [Theory]
    [InlineData("missingReason")]
    [InlineData("invalidKind")]
    [InlineData("otherCustomer")]
    [InlineData("downstream")]
    [InlineData("stale")]
    public async Task InvalidContinuation_IsRejectedAtomically(string scenario)
    {
        var source = await SourceAsync(); var body = Body(source);
        switch (scenario)
        {
            case "missingReason": body["continuationReason"] = "   "; break;
            case "invalidKind": body["continuationKind"] = "Amendment"; break;
            case "otherCustomer": body["customerId"] = source.CustomerId + 100000; break;
            case "downstream": body["direction"] = "Downstream"; break;
            case "stale": body["sourceContractRowVersion"] = Convert.ToBase64String(new byte[8]); break;
        }
        var result = await PostAsync("/api/contracts", body);
        result.StatusCode.Should().Be(scenario == "stale" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.SourceContractId == source.Id)).Should().Be(0));
    }

    [Fact]
    public async Task ForeignOwner_AnonymousAndReadOnly_CannotCreateOrReadRelations()
    {
        var source = await SourceAsync(otherOwner: true);
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/contracts/{source.Id}/continuations")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        Client.DefaultRequestHeaders.Authorization = null;
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task OpportunityTracksNegotiation_AndContractInheritsProvenance()
    {
        var source = await SourceAsync(ContractStatus.Completed);
        var opportunity = Body(source, "Expansion");
        opportunity["name"] = "Phát triển nhà điều hành An Phú";
        opportunity["estimatedValue"] = 150000000;
        opportunity["stage"] = "Prospecting";
        var created = await PostAsync("/api/opportunities", opportunity);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var op = await ReadJsonAsync(created); var opId = op.GetProperty("id").GetInt32();
        var body = Body(source); body.Remove("sourceContractId"); body.Remove("continuationKind"); body.Remove("continuationReason");
        body["opportunityId"] = opId;
        var contract = await PostAsync("/api/contracts", body);
        contract.StatusCode.Should().Be(HttpStatusCode.Created, await contract.Content.ReadAsStringAsync());
        var result = await ReadJsonAsync(contract);
        result.GetProperty("continuationKind").GetString().Should().Be("Expansion");
        result.GetProperty("sourceContractId").GetInt32().Should().Be(source.Id);
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}/deletion-impact"));
        impact.GetRawText().Should().Contain("contract.continuationOpportunities");
        var changed = JsonNode.Parse(op.GetRawText())!.AsObject(); changed["customerId"] = source.CustomerId + 1;
        (await Client.PutAsJsonAsync($"/api/opportunities/{opId}", changed)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReplayRechecksSourceScope_AndSourceIdentityCannotChange()
    {
        var source = await SourceAsync(); var body = Body(source); var key = Guid.NewGuid().ToString();
        var created = await PostAsync("/api/contracts", body, key); created.EnsureSuccessStatusCode();
        var current = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}"));
        var edit = JsonNode.Parse(current.GetRawText())!.AsObject(); edit["customerId"] = source.CustomerId + 1;
        (await Client.PutAsJsonAsync($"/api/contracts/{source.Id}", edit)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => { var row = await db.Contracts.SingleAsync(x => x.Id == source.Id); row.OwnerUserId = null; await db.SaveChangesAsync(); });
        (await PostAsync("/api/contracts", body, key)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewRelationInvalidatesDeletionPlan_AndBlockedDeletePreservesBothRoots()
    {
        var source = await SourceAsync();
        var oldPlan = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}/deletion-impact"));
        var created = await ReadJsonAsync(await PostAsync("/api/contracts", Body(source)));
        var id = created.GetProperty("id").GetInt32();
        async Task<HttpResponseMessage> DeleteAsync(JsonElement plan) => await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{source.Id}")
        {
            Content = JsonContent.Create(new
            {
                confirmation = plan.GetProperty("requiredConfirmation").GetString(),
                planToken = plan.GetProperty("planToken").GetString(),
                rowVersion = Convert.ToBase64String(source.RowVersion)
            })
        });
        (await DeleteAsync(oldPlan)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var plan = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}/deletion-impact"));
        plan.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        (await DeleteAsync(plan)).IsSuccessStatusCode.Should().BeFalse();
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.Id == source.Id || x.Id == id)).Should().Be(2));
    }

    [Fact]
    public async Task LostPursuitIsVisibleWithReason_AndCannotBecomeNewContract()
    {
        var source = await SourceAsync();
        var body = Body(source, "Expansion"); body["name"] = "An Phú dời kế hoạch nhà điều hành";
        var created = await ReadJsonAsync(await PostAsync("/api/opportunities", body));
        var id = created.GetProperty("id").GetInt32();
        var lost = await Client.PatchAsJsonAsync($"/api/opportunities/{id}/stage", new
        { targetStage = "Lost", lostReasonCode = "timing", lostNote = "Chủ đầu tư chuyển sang kế hoạch đầu tư năm sau.", rowVersion = created.GetProperty("rowVersion").GetString() });
        lost.EnsureSuccessStatusCode();
        var related = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{source.Id}/continuations"));
        related.GetProperty("opportunities")[0].GetProperty("lostNote").GetString().Should().Contain("năm sau");
        var contract = Body(source); contract["opportunityId"] = id;
        (await PostAsync("/api/contracts", contract)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MissingIdempotencyKeyAndMissingVersion_AreRejected()
    {
        var source = await SourceAsync();
        (await Client.PostAsJsonAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = Body(source); body.Remove("sourceContractRowVersion");
        (await PostAsync("/api/contracts", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.SourceContractId == source.Id)).Should().Be(0));
    }

    [Theory]
    [InlineData(ContractStatus.InProgress)]
    [InlineData(ContractStatus.OnHold)]
    [InlineData(ContractStatus.Completed)]
    public async Task EligibleStatusesAllowMultipleIndependentDrafts(ContractStatus status)
    {
        var source = await SourceAsync(status);
        (await PostAsync("/api/contracts", Body(source))).EnsureSuccessStatusCode();
        (await PostAsync("/api/contracts", Body(source, "Expansion"))).EnsureSuccessStatusCode();
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.SourceContractId == source.Id)).Should().Be(2));
    }

    [Fact]
    public async Task UnsignedSourceAndProvenanceTamperingAreRejected()
    {
        var source = await SourceAsync();
        var created = await ReadJsonAsync(await PostAsync("/api/contracts", Body(source)));
        var id = created.GetProperty("id").GetInt32();
        var edit = JsonNode.Parse(created.GetRawText())!.AsObject(); edit["continuationReason"] = "Thay lịch sử nguồn";
        (await Client.PutAsJsonAsync($"/api/contracts/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => { var root = await db.Contracts.SingleAsync(x => x.Id == source.Id); root.SignedDate = null; await db.SaveChangesAsync(); source.RowVersion = root.RowVersion; });
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpportunityManagerWithoutContractManageCanStartPursuit_ButCannotEraseHistory()
    {
        var source = await SourceAsync();
        var grant = await WithDbAsync(async db =>
        {
            var sale = await db.Roles.SingleAsync(x => x.Code == "SALE");
            var permission = await db.RolePermissions.Include(x => x.Permission).SingleAsync(x => x.RoleId == sale.Id &&
                x.Permission.Module == "crm.contracts" && x.Permission.Action == "manage");
            db.RolePermissions.Remove(permission); await db.SaveChangesAsync(); return permission;
        });
        try
        {
            (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var body = Body(source); body["name"] = "An Phú – trao đổi tái ký thiết kế";
            var response = await PostAsync("/api/opportunities", body);
            response.EnsureSuccessStatusCode();
            var id = (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
            var impact = await ReadJsonAsync(await Client.GetAsync($"/api/opportunities/{id}/deletion-impact"));
            impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
            impact.GetRawText().Should().Contain("opportunity.continuationSource");
            var deletion = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/opportunities/{id}")
            {
                Content = JsonContent.Create(new
                {
                    confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                    planToken = impact.GetProperty("planToken").GetString(),
                    rowVersion = (await ReadJsonAsync(await Client.GetAsync($"/api/opportunities/{id}"))).GetProperty("rowVersion").GetString()
                })
            });
            deletion.IsSuccessStatusCode.Should().BeFalse();
            (await Client.GetAsync($"/api/opportunities/{id}")).EnsureSuccessStatusCode();
        }
        finally
        {
            await WithDbAsync(async db =>
            {
                if (!await db.RolePermissions.AnyAsync(x => x.RoleId == grant.RoleId && x.PermissionId == grant.PermissionId))
                    db.RolePermissions.Add(new NihomeBackend.Models.Rbac.RolePermission { RoleId = grant.RoleId, PermissionId = grant.PermissionId });
                await db.SaveChangesAsync();
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostOrRestrictedPursuitCannotBeAttachedThroughEditOrQuote(bool restricted)
    {
        var source = await SourceAsync();
        var body = Body(source, "Expansion"); body["name"] = "An Phú – phạm vi nhà điều hành";
        var pursuit = await ReadJsonAsync(await PostAsync("/api/opportunities", body));
        var opId = pursuit.GetProperty("id").GetInt32();
        var quoteId = await WithDbAsync(async db =>
        {
            var opportunity = await db.Opportunities.SingleAsync(x => x.Id == opId);
            if (restricted) opportunity.OwnerUserId = null;
            else { opportunity.Stage = OpportunityStage.Lost; opportunity.LostReasonCode = "timing"; opportunity.LostNote = "Chủ đầu tư dừng mở rộng."; }
            var quote = new Quote
            {
                Code = "QT-" + Guid.NewGuid().ToString("N"),
                OpportunityId = opId,
                OperationalProjectId = source.OperationalProjectId,
                Status = QuoteStatus.Approved,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                OwnerUserId = source.OwnerUserId
            };
            db.Quotes.Add(quote); await db.SaveChangesAsync(); return quote.Id;
        });
        var direct = await ReadJsonAsync(await PostAsync("/api/contracts", Body(source, "Expansion")));
        var id = direct.GetProperty("id").GetInt32();
        var edit = JsonNode.Parse(direct.GetRawText())!.AsObject(); edit["opportunityId"] = opId;
        (await Client.PutAsJsonAsync($"/api/contracts/{id}", edit)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync($"/api/contracts/{id}/link-quote", new
        {
            quoteId,
            rowVersion = direct.GetProperty("rowVersion").GetString()
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var after = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{id}"));
        after.GetProperty("opportunityId").ValueKind.Should().Be(JsonValueKind.Null);
        after.GetProperty("quoteId").ValueKind.Should().Be(JsonValueKind.Null);
        after.GetProperty("rowVersion").GetString().Should().Be(direct.GetProperty("rowVersion").GetString());
    }

    [Fact]
    public async Task QuoteOnlyCreateInheritsContinuationAndKeepsApprovalAndExclusivityGates()
    {
        var source = await SourceAsync(); var body = Body(source, "Expansion"); body["name"] = "An Phú báo giá thiết kế bổ sung";
        var pursuit = await ReadJsonAsync(await PostAsync("/api/opportunities", body));
        var quoteId = await WithDbAsync(async db =>
        {
            var quote = new Quote
            {
                Code = "QT-" + Guid.NewGuid().ToString("N"),
                OpportunityId = pursuit.GetProperty("id").GetInt32(),
                OperationalProjectId = source.OperationalProjectId,
                Status = QuoteStatus.Draft,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                OwnerUserId = source.OwnerUserId
            };
            db.Quotes.Add(quote); await db.SaveChangesAsync(); return quote.Id;
        });
        var contract = Body(source); contract.Remove("sourceContractId"); contract.Remove("continuationKind"); contract.Remove("continuationReason"); contract["quoteId"] = quoteId;
        (await PostAsync("/api/contracts", contract)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => { var quote = await db.Quotes.SingleAsync(x => x.Id == quoteId); quote.Status = QuoteStatus.Approved; await db.SaveChangesAsync(); });
        var response = await PostAsync("/api/contracts", contract);
        response.EnsureSuccessStatusCode();
        (await ReadJsonAsync(response)).GetProperty("continuationKind").GetString().Should().Be("Expansion");
        (await PostAsync("/api/contracts", contract)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InaccessibleProjectIsRejectedOnContractCreateEditAndPursuit()
    {
        var source = await SourceAsync();
        var projectId = await WithDbAsync(async db =>
        {
            var project = new OperationalProject
            {
                Code = "PRIVATE-" + Guid.NewGuid().ToString("N"),
                Name = "An Phú – khu nhà điều hành hạn chế truy cập",
                CustomerId = source.CustomerId,
                CreatedByUserId = (await db.Users.FirstAsync(x => x.PhoneNumber == "0911000005")).Id
            };
            db.OperationalProjects.Add(project); await db.SaveChangesAsync(); return project.Id;
        });
        var body = Body(source); body["operationalProjectId"] = projectId;
        (await PostAsync("/api/contracts", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body["name"] = "Cơ hội dự án ngoài phạm vi";
        (await PostAsync("/api/opportunities", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var created = await ReadJsonAsync(await PostAsync("/api/contracts", Body(source)));
        var edit = JsonNode.Parse(created.GetRawText())!.AsObject(); edit["operationalProjectId"] = projectId;
        (await Client.PutAsJsonAsync($"/api/contracts/{created.GetProperty("id").GetInt32()}", edit)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.Contracts.SingleAsync(x => x.Id == created.GetProperty("id").GetInt32())).OperationalProjectId.Should().Be(source.OperationalProjectId));
    }

    [Fact]
    public async Task DownstreamSourceAndOversizedReasonCannotCreateSuccessors()
    {
        var source = await SourceAsync(); var body = Body(source); body["continuationReason"] = new string('x', 2001);
        (await PostAsync("/api/contracts", body)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => { var row = await db.Contracts.SingleAsync(x => x.Id == source.Id); row.Direction = ContractDirection.Downstream; row.Type = ContractType.Supply; await db.SaveChangesAsync(); source.RowVersion = row.RowVersion; });
        (await PostAsync("/api/contracts", Body(source))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db => (await db.Contracts.CountAsync(x => x.SourceContractId == source.Id)).Should().Be(0));
    }
}
