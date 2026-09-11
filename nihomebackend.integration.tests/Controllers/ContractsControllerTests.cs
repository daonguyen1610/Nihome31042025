using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>ContractsController</c> — CRUD, RBAC scoping
/// (Sales sees only own, Manager sees all), duplicate handling, validation.
/// NIH-102 scope: list + minimal CRUD. Payment milestones / VOs are follow-up.
/// </summary>
public class ContractsControllerTests : IntegrationTestBase
{
    public ContractsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CreateCustomerAsync(bool createOperationalProject = true)
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
        res.StatusCode.Should().Be(HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        var customerId = (await ReadJsonAsync(res)).GetProperty("id").GetInt32();
        if (createOperationalProject)
        {
            await WithDbAsync(async db =>
            {
                db.OperationalProjects.Add(new OperationalProject
                {
                    Code = $"PJ-CT-{Guid.NewGuid():N}"[..20],
                    Name = $"Contract project {Guid.NewGuid():N}",
                    CustomerId = customerId,
                });
                await db.SaveChangesAsync();
            });
        }
        return customerId;
    }

    private static object ContractBody(int customerId, string status = "Draft", decimal value = 100_000_000)
        => new
        {
            customerId,
            direction = "Upstream",
            type = "DesignAndBuild",
            status,
            value,
            signedDate = "2026-06-01T00:00:00Z",
        };

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/contracts")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/contracts/export-data?direction=Upstream")).StatusCode
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
    public async Task List_AsAccountant_ReturnsContractsAcrossSalesOwners()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var projectId = await WithDbAsync(db => db.OperationalProjects
            .Where(project => project.CustomerId == customerId)
            .Select(project => project.Id)
            .SingleAsync());
        var create = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            operationalProjectId = projectId,
            direction = "Upstream",
            type = "DesignAndBuild",
            status = "Draft",
            value = 250_000_000,
            endDate = "2026-12-31T18:00:00Z",
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var contractId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "ACCOUNTANT"));
        var response = await Client.GetAsync(
            $"/api/contracts?direction=Upstream&operationalProjectId={projectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == contractId);

        var endDateFiltered = await Client.GetAsync(
            $"/api/contracts?direction=Upstream&endFrom=2026-12-31&endTo=2026-12-31&operationalProjectId={projectId}");
        endDateFiltered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(endDateFiltered)).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == contractId);

        var options = await Client.GetAsync("/api/contracts/filter-options?direction=Upstream");
        options.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(options)).GetProperty("projects").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == projectId);

        var export = await Client.GetAsync(
            $"/api/contracts/export-data?direction=Upstream&operationalProjectId={projectId}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(export)).GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == contractId);

        var detail = await Client.GetAsync($"/api/contracts/{contractId}?direction=Upstream");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        var unscoped = await Client.GetAsync("/api/contracts");
        unscoped.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(unscoped)).GetProperty("items").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetInt32() == contractId);
    }

    [Fact]
    public async Task Get_WithMismatchedDirectionScope_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SUPER_ADMIN"));
        var customerId = await CreateCustomerAsync();
        var create = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));
        create.StatusCode.Should().Be(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var contractId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var response = await Client.GetAsync($"/api/contracts/{contractId}?direction=Downstream");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ClassificationOptions_ExcludeLegacyValue_AndDefineAllowedTypes()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));

        var response = await Client.GetAsync("/api/contracts/classification-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("directions").EnumerateArray().Select(item => item.GetString())
            .Should().BeEquivalentTo("Upstream", "Downstream");
        body.GetProperty("types").EnumerateArray().Select(item => item.GetString())
            .Should().NotContain("Unclassified");
        body.GetProperty("allowedTypes").GetProperty("Downstream")
            .EnumerateArray().Select(item => item.GetString())
            .Should().BeEquivalentTo("Supply", "Subcontract");
    }

    [Fact]
    public async Task List_ByOperationalProject_ReturnsMultipleContractTypesForOnlyThatProject()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        async Task<int> CreateProjectAsync(string name)
        {
            var response = await Client.PostAsJsonAsync("/api/operational-projects", new
            {
                name,
                customerId,
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
        }

        async Task<(int Id, string Type)> CreateContractAsync(int operationalProjectId, string type)
        {
            var response = await Client.PostAsJsonAsync("/api/contracts", new
            {
                customerId,
                operationalProjectId,
                direction = "Upstream",
                type,
                status = "Draft",
                value = 100_000_000,
            });
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            var body = await ReadJsonAsync(response);
            return (body.GetProperty("id").GetInt32(), body.GetProperty("type").GetString()!);
        }

        var selectedProjectId = await CreateProjectAsync($"Multi-contract {Guid.NewGuid():N}");
        var otherProjectId = await CreateProjectAsync($"Other project {Guid.NewGuid():N}");
        var design = await CreateContractAsync(selectedProjectId, "Design");
        var construction = await CreateContractAsync(selectedProjectId, "Construction");
        await CreateContractAsync(otherProjectId, "DesignAndBuild");

        var response = await Client.GetAsync($"/api/contracts?operationalProjectId={selectedProjectId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("total").GetInt32().Should().Be(2);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetInt32())
            .Should().BeEquivalentTo([design.Id, construction.Id]);
        items.Select(item => item.GetProperty("type").GetString())
            .Should().BeEquivalentTo("Design", "Construction");
        items.Should().OnlyContain(item =>
            item.GetProperty("operationalProjectId").GetInt32() == selectedProjectId);
        items.Should().OnlyContain(item =>
            !string.IsNullOrWhiteSpace(item.GetProperty("operationalProjectCode").GetString()) &&
            !string.IsNullOrWhiteSpace(item.GetProperty("operationalProjectName").GetString()));
    }

    [Fact]
    public async Task Create_WithoutOperationalProject_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync(createOperationalProject: false);

        var response = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Dự án vận hành");
    }

    [Fact]
    public async Task Create_WithoutOperationalProject_WhenCustomerHasMultipleProjects_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var secondProject = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Second project {Guid.NewGuid():N}",
            customerId,
        });
        secondProject.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Dự án vận hành");
    }

    [Fact]
    public async Task Create_WithProjectOwnedByDifferentCustomer_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var contractCustomerId = await CreateCustomerAsync();
        var projectCustomerId = await CreateCustomerAsync();
        var projectResponse = await Client.PostAsJsonAsync("/api/operational-projects", new
        {
            name = $"Cross-customer {Guid.NewGuid():N}",
            customerId = projectCustomerId,
        });
        projectResponse.EnsureSuccessStatusCode();
        var projectId = (await ReadJsonAsync(projectResponse)).GetProperty("id").GetInt32();

        var response = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId = contractCustomerId,
            operationalProjectId = projectId,
            direction = "Upstream",
            type = "Design",
            status = "Draft",
            value = 100,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("cùng một Khách hàng");
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
    public async Task FullRoundTrip_AsSalesManager_Create_Update_PreviewDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var created = await Client.PostAsJsonAsync("/api/contracts", ContractBody(customerId));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        body.GetProperty("contractNumber").GetString().Should().StartWith("HD-");
        body.GetProperty("customerId").GetInt32().Should().Be(customerId);
        body.GetProperty("direction").GetString().Should().Be("Upstream");
        body.GetProperty("type").GetString().Should().Be("DesignAndBuild");
        body.GetProperty("status").GetString().Should().Be("Draft");

        var update = await Client.PutAsJsonAsync($"/api/contracts/{id}", new
        {
            customerId,
            direction = "Upstream",
            type = "DesignAndBuild",
            status = "Signed",
            value = 500_000_000,
            signedDate = "2026-06-15T00:00:00Z",
            startDate = "2026-07-01T00:00:00Z",
            endDate = "2026-12-31T00:00:00Z",
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("status").GetString().Should().Be("Signed");

        var impact = await GetDeletionImpactAsync(id);
        impact.GetProperty("resourceType").GetString().Should().Be("Contract");
        impact.GetProperty("requiredConfirmation").GetString()
            .Should().Be(body.GetProperty("contractNumber").GetString());
        (await ConfirmDeleteAsync(id, impact)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DeleteContractAsync(id, new string('a', 64), impact.GetProperty("requiredConfirmation").GetString()))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletionImpact_EnforcesAuthorizationPermissionAndOwnerScope()
    {
        (await Client.GetAsync("/api/contracts/1/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/contracts/1/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.GetAsync($"/api/contracts/{contractId}/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DeleteContractAsync(contractId, new string('a', 64), "HIDDEN"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.Contracts.AnyAsync(item => item.Id == contractId))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_RejectsMissingOrInvalidConfirmationAndStalePlanWithoutMutation()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);
        var impact = await GetDeletionImpactAsync(contractId);
        var token = impact.GetProperty("planToken").GetString()!;
        var confirmation = impact.GetProperty("requiredConfirmation").GetString()!;
        var rowVersion = await GetRowVersionAsync(contractId);

        (await DeleteContractAsync(contractId, token, null, rowVersion)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeleteContractAsync(contractId, token, $"{confirmation} ", rowVersion)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DeleteContractAsync(contractId, new string('a', 64), confirmation, rowVersion)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await WithDbAsync(db => db.Contracts.AnyAsync(item => item.Id == contractId))).Should().BeTrue();
        (await WithDbAsync(db => db.HardDeleteOperations.AnyAsync(item =>
            item.ResourceType == "Contract" && item.ResourceId == contractId.ToString()))).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_RequiresWellFormedCurrentRowVersionAndEmitsOneCompletionAudit()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId);
        var impact = await GetDeletionImpactAsync(contractId);
        var planToken = impact.GetProperty("planToken").GetString();
        var confirmation = impact.GetProperty("requiredConfirmation").GetString();
        var staleBytes = Convert.FromBase64String(await GetRowVersionAsync(contractId));
        staleBytes[0] ^= 0xff;

        (await DeleteContractAsync(contractId, planToken, confirmation, null)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await DeleteContractAsync(contractId, planToken, confirmation, "malformed")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await DeleteContractAsync(contractId, planToken, confirmation, Convert.ToBase64String(staleBytes)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.AuditLogs.CountAsync(item =>
            item.ResourceType == EntityTypes.Contract && item.ResourceId == contractId.ToString() &&
            item.Action == "contract.delete"))).Should().Be(0);

        (await ConfirmDeleteAsync(contractId, impact)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await WithDbAsync(db => db.AuditLogs.CountAsync(item =>
            item.ResourceType == EntityTypes.Contract && item.ResourceId == contractId.ToString() &&
            item.Action == "contract.delete"))).Should().Be(1);
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
            direction = "Upstream",
            type = "DesignAndBuild",
            status = "Draft",
            value = 100,
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        (await ReadJsonAsync(first)).GetProperty("contractNumber").GetString().Should().Be(number);

        var dup = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            contractNumber = number,
            direction = "Upstream",
            type = "DesignAndBuild",
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
    public async Task Create_WithoutClassification_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();

        var response = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            status = "Draft",
            value = 100,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DownstreamContract_RequiresCompatibleActiveVendor()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var vendor = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            var entity = new Vendor
            {
                VendorCode = $"SUB-{Guid.NewGuid():N}"[..20],
                CompanyName = "Approved subcontractor",
                VendorType = VendorType.SubContractor,
                CreatedByUserId = userId,
            };
            db.Vendors.Add(entity);
            await db.SaveChangesAsync();
            return entity;
        });

        var missingVendor = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Downstream",
            type = "Subcontract",
            status = "Draft",
            value = 100,
        });
        missingVendor.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var upstreamWithVendor = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Upstream",
            type = "DesignAndBuild",
            vendorId = vendor.Id,
            status = "Draft",
            value = 100,
        });
        upstreamWithVendor.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var incompatibleType = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Downstream",
            type = "Supply",
            vendorId = vendor.Id,
            status = "Draft",
            value = 100,
        });
        incompatibleType.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await WithDbAsync(async db =>
        {
            var persisted = await db.Vendors.SingleAsync(item => item.Id == vendor.Id);
            persisted.IsActive = false;
            await db.SaveChangesAsync();
        });
        var inactiveVendor = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Downstream",
            type = "Subcontract",
            vendorId = vendor.Id,
            status = "Draft",
            value = 100,
        });
        inactiveVendor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await WithDbAsync(async db =>
        {
            var persisted = await db.Vendors.SingleAsync(item => item.Id == vendor.Id);
            persisted.IsActive = true;
            await db.SaveChangesAsync();
        });

        var created = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Downstream",
            type = "Subcontract",
            vendorId = vendor.Id,
            status = "Draft",
            value = 100,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(created);
        body.GetProperty("direction").GetString().Should().Be("Downstream");
        body.GetProperty("type").GetString().Should().Be("Subcontract");
        body.GetProperty("vendorId").GetInt32().Should().Be(vendor.Id);
        body.GetProperty("vendorName").GetString().Should().Be(vendor.CompanyName);

        var filtered = await ReadJsonAsync(await Client.GetAsync(
            $"/api/contracts?direction=Downstream&type=Subcontract&vendorId={vendor.Id}"));
        filtered.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == body.GetProperty("id").GetInt32());
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
            direction = "Upstream",
            type = "DesignAndBuild",
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
            direction = "Upstream",
            type = "DesignAndBuild",
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
            direction = "Upstream",
            type = "DesignAndBuild",
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
    public async Task Transition_CompanyContract_RequiresTaxIdAndLegalRepresentative()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                Type = CustomerType.Company,
                Name = "Signing gate company",
                Address = "1 Nguyen Trai",
                RepresentativeName = "Legal Representative",
                SourceCode = "marketing",
                Contacts =
                [
                    new CustomerContact
                    {
                        FullName = "Legal Representative",
                        IsPrimary = true,
                        IsLegalRepresentative = true,
                        LegalRepresentativeSince = DateTime.UtcNow,
                    },
                ],
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            db.OperationalProjects.Add(new OperationalProject
            {
                Code = $"PJ-SIGN-{Guid.NewGuid():N}"[..20],
                Name = "Signing gate project",
                CustomerId = customer.Id,
            });
            await db.SaveChangesAsync();
            return customer.Id;
        });
        var contractId = await CreateContractAsync(customerId);

        var missingTax = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/transition",
            new { newStatus = "Signed" });
        missingTax.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var afterMissingTax = await WithDbAsync(db => db.Contracts.AsNoTracking()
            .SingleAsync(item => item.Id == contractId));
        afterMissingTax.Status.Should().Be(ContractStatus.Draft);
        var unchangedRowVersion = afterMissingTax.RowVersion.ToArray();
        var unchangedUpdatedAt = afterMissingTax.UpdatedAt;

        await WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(item => item.Id == customerId);
            customer.TaxId = "TAX-SIGNING";
            var representative = await db.CustomerContacts.SingleAsync(item => item.CustomerId == customerId);
            representative.IsLegalRepresentative = false;
            representative.LegalRepresentativeSince = null;
            await db.SaveChangesAsync();
        });
        var missingRepresentative = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/transition",
            new { newStatus = "Signed" });
        missingRepresentative.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var afterMissingRepresentative = await WithDbAsync(db => db.Contracts.AsNoTracking()
            .SingleAsync(item => item.Id == contractId));
        afterMissingRepresentative.Status.Should().Be(ContractStatus.Draft);
        afterMissingRepresentative.RowVersion.Should().Equal(unchangedRowVersion);
        afterMissingRepresentative.UpdatedAt.Should().Be(unchangedUpdatedAt);

        await WithDbAsync(async db =>
        {
            var representative = await db.CustomerContacts.SingleAsync(item => item.CustomerId == customerId);
            representative.IsLegalRepresentative = true;
            representative.LegalRepresentativeSince = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var signed = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/transition",
            new { newStatus = "Signed" });
        signed.StatusCode.Should().Be(HttpStatusCode.OK);
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
    public async Task Transition_LegacyUnclassifiedContract_ReturnsBadRequestWithoutMutation()
    {
        await AuthTestHelper.AuthenticateAsync(
            Client,
            client => AuthTestHelper.LoginAsRoleAsync(client, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var contractId = await WithDbAsync(async db =>
        {
            var entity = new Contract
            {
                ContractNumber = $"HD-LEGACY-{Guid.NewGuid():N}"[..30],
                CustomerId = customerId,
                Direction = ContractDirection.Upstream,
                Type = ContractType.Unclassified,
                Status = ContractStatus.Draft,
            };
            db.Contracts.Add(entity);
            await db.SaveChangesAsync();
            return entity.Id;
        });

        var response = await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/transition",
            new { newStatus = "Signed" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Contracts.SingleAsync(item => item.Id == contractId)))
            .Status.Should().Be(ContractStatus.Draft);
    }

    [Fact]
    public async Task Milestone_StatusUpdate_RequiresPersistsAndClearsActualPaymentDate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var accountantUserId = await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == TestDataSeeder.BusinessRolePhonesByCode["ACCOUNTANT"])
            .Select(user => user.Id)
            .SingleAsync());

        var body = new
        {
            customerId,
            direction = "Upstream",
            type = "DesignAndBuild",
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

        var missingDate = await Client.PatchAsJsonAsync(
            $"/api/contracts/{contractId}/milestones/{milestoneId}/status",
            new { status = "Paid" });
        missingDate.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var paid = await Client.PatchAsJsonAsync(
            $"/api/contracts/{contractId}/milestones/{milestoneId}/status",
            new
            {
                status = "Paid",
                actualPaymentDate = "2026-08-30T15:45:00Z",
                responsibleAccountantUserId = accountantUserId,
                note = "Payment confirmed",
            });
        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        var paidMilestone = (await ReadJsonAsync(paid)).GetProperty("paymentMilestones")[0];
        paidMilestone.GetProperty("status").GetString().Should().Be("Paid");
        paidMilestone.GetProperty("actualPaymentDate").GetDateTime()
            .Should().Be(new DateTime(2026, 8, 30));
        paidMilestone.GetProperty("responsibleAccountantUserId").GetInt32().Should().Be(accountantUserId);
        var paymentEvent = await WithDbAsync(db => db.ContractPaymentMilestoneEvents.AsNoTracking()
            .SingleAsync(item => item.ContractPaymentMilestoneId == milestoneId));
        paymentEvent.FromStatus.Should().Be(PaymentMilestoneStatus.Pending);
        paymentEvent.ToStatus.Should().Be(PaymentMilestoneStatus.Paid);
        paymentEvent.Note.Should().Be("Payment confirmed");

        var pending = await Client.PatchAsJsonAsync(
            $"/api/contracts/{contractId}/milestones/{milestoneId}/status",
            new { status = "Pending", actualPaymentDate = "2026-09-01T00:00:00Z" });
        pending.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingMilestone = (await ReadJsonAsync(pending)).GetProperty("paymentMilestones")[0];
        pendingMilestone.GetProperty("status").GetString().Should().Be("Pending");
        pendingMilestone.GetProperty("actualPaymentDate").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Milestone_StatusUpdate_WithoutManagePermission_IsForbiddenAndPreservesState()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateCustomerAsync();
        var created = await Client.PostAsJsonAsync("/api/contracts", new
        {
            customerId,
            direction = "Upstream",
            type = "DesignAndBuild",
            status = "Signed",
            value = 100_000_000,
            paymentMilestones = new object[]
            {
                new { order = 1, name = "M1", percentValue = 100m, status = "Pending" },
            },
        });
        created.EnsureSuccessStatusCode();
        var createdJson = await ReadJsonAsync(created);
        var contractId = createdJson.GetProperty("id").GetInt32();
        var milestoneId = createdJson.GetProperty("paymentMilestones")[0].GetProperty("id").GetInt32();

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "PM"));
        var response = await Client.PatchAsJsonAsync(
            $"/api/contracts/{contractId}/milestones/{milestoneId}/status",
            new { status = "Paid", actualPaymentDate = "2026-08-30T00:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var persisted = await WithDbAsync(db => db.ContractPaymentMilestones
            .AsNoTracking()
            .SingleAsync(milestone => milestone.Id == milestoneId));
        persisted.Status.Should().Be(NihomeBackend.Models.PaymentMilestoneStatus.Pending);
        persisted.ActualPaymentDate.Should().BeNull();
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
    public async Task DeleteApprovedAppendix_AsOwningSale_SucceedsAndRestoresCurrentValue()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateCustomerAsync();
        var contractId = await CreateContractAsync(customerId, status: "Signed", value: 500_000_000m);
        var create = await Client.PostAsJsonAsync($"/api/contracts/{contractId}/appendices", new
        {
            title = "Owned approved VO",
            reason = "Delete permission regression",
            valueDelta = 50_000_000m,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var voId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();
        (await Client.PostAsync($"/api/contracts/{contractId}/appendices/{voId}/submit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.PostAsJsonAsync(
            $"/api/contracts/{contractId}/appendices/{voId}/approve",
            new { note = "Approved" })).StatusCode.Should().Be(HttpStatusCode.OK);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        (await Client.DeleteAsync($"/api/contracts/{contractId}/appendices/{voId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refreshed = await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{contractId}"));
        refreshed.GetProperty("approvedVoTotal").GetDecimal().Should().Be(0);
        refreshed.GetProperty("currentValue").GetDecimal().Should().Be(500_000_000m);
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

    [Fact]
    public async Task WonOpportunity_LastQualifyingContract_CannotBeCancelledOrDeleted()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var ids = await WithDbAsync(async db =>
        {
            var customer = new Customer { Name = "Won customer", Type = CustomerType.Company, SourceCode = "marketing" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var opportunity = new Opportunity
            {
                Name = "Won deal",
                CustomerId = customer.Id,
                Stage = OpportunityStage.Won,
            };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync();
            var contract = new Contract
            {
                ContractNumber = "HD-WON-" + Guid.NewGuid().ToString("N")[..8],
                CustomerId = customer.Id,
                OpportunityId = opportunity.Id,
                Status = ContractStatus.Signed,
                SignedDate = DateTime.UtcNow,
                Value = 1_000_000m,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync();
            return (ContractId: contract.Id, OpportunityId: opportunity.Id);
        });

        (await Client.PostAsJsonAsync($"/api/contracts/{ids.ContractId}/transition", new { newStatus = "Cancelled" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var impact = await GetDeletionImpactAsync(ids.ContractId);
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "contract.wonOpportunity" &&
            item.GetProperty("action").GetString() == "Block");
        (await ConfirmDeleteAsync(ids.ContractId, impact)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var status = await WithDbAsync(db => db.Contracts
            .Where(item => item.Id == ids.ContractId)
            .Select(item => item.Status)
            .SingleAsync());
        status.Should().Be(ContractStatus.Signed);
        (await WithDbAsync(db => db.Opportunities.AnyAsync(item => item.Id == ids.OpportunityId))).Should().BeTrue();
    }

    private async Task<JsonElement> GetDeletionImpactAsync(int contractId)
    {
        var response = await Client.GetAsync($"/api/contracts/{contractId}/deletion-impact");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> ConfirmDeleteAsync(int contractId, JsonElement impact) =>
        await DeleteContractAsync(
            contractId,
            impact.GetProperty("planToken").GetString(),
            impact.GetProperty("requiredConfirmation").GetString(),
            await GetRowVersionAsync(contractId));

    private async Task<string> GetRowVersionAsync(int contractId) =>
        (await ReadJsonAsync(await Client.GetAsync($"/api/contracts/{contractId}")))
            .GetProperty("rowVersion").GetString()!;

    private async Task<HttpResponseMessage> DeleteContractAsync(
        int contractId,
        string? planToken,
        string? confirmation,
        string? rowVersion = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/contracts/{contractId}")
        {
            Content = JsonContent.Create(new { planToken, confirmation, rowVersion }),
        };
        return await Client.SendAsync(request);
    }
}
