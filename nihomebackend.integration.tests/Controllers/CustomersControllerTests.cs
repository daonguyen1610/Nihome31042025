using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Models;

namespace NihomeBackend.IntegrationTests.Controllers;

/// <summary>
/// End-to-end coverage for <c>CustomersController</c> (NIH-78): RBAC scoping,
/// duplicate detection with override, contact primary-flag invariant,
/// activity timeline, and delete guard.
/// </summary>
public class CustomersControllerTests : IntegrationTestBase
{
    public CustomersControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsRoleWithoutPerm_IsForbidden()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        (await Client.GetAsync("/api/customers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_Individual_AsSale_PersistsWithPrimaryContact()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var payload = new
        {
            type = "Individual",
            name = "Ms. Nga " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Ms. Nga",
                phone = "0911" + Random.Shared.Next(100000, 999999),
                isPrimary = true,
            },
        };

        var res = await Client.PostAsJsonAsync("/api/customers", payload);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await ReadJsonAsync(res);
        body.GetProperty("type").GetString().Should().Be("Individual");
        body.GetProperty("relationshipStatus").GetString().Should().Be("Prospect");
        body.GetProperty("contacts").GetArrayLength().Should().Be(1);
        body.GetProperty("contacts")[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Create_SameIdempotencyKeyAndPayload_ReplaysOriginalResponse()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var key = $"customer-{Guid.NewGuid():N}";
        var payload = new
        {
            type = "Individual",
            name = "Idempotent " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Contact", phone = "0911" + Random.Shared.Next(100000, 999999) },
        };

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(payload),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        using var first = await Client.SendAsync(firstRequest);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.ETag.Should().NotBeNull();
        var firstId = (await ReadJsonAsync(first)).GetProperty("id").GetInt32();

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(payload),
        };
        replayRequest.Headers.Add("Idempotency-Key", key);
        using var replay = await Client.SendAsync(replayRequest);

        replay.StatusCode.Should().Be(HttpStatusCode.Created);
        replay.Headers.GetValues("Idempotency-Replayed").Should().ContainSingle("true");
        replay.Headers.ETag.Should().Be(first.Headers.ETag);
        (await ReadJsonAsync(replay)).GetProperty("id").GetInt32().Should().Be(firstId);
    }

    [Fact]
    public async Task Create_SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var key = $"customer-{Guid.NewGuid():N}";

        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new
            {
                type = "Individual",
                name = "First payload",
                sourceCode = "marketing",
                primaryContact = new { fullName = "Contact", phone = "0911" + Random.Shared.Next(100000, 999999) },
            }),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        (await Client.SendAsync(firstRequest)).EnsureSuccessStatusCode();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(new
            {
                type = "Individual",
                name = "Different payload",
                sourceCode = "marketing",
                primaryContact = new { fullName = "Contact", phone = "0900" + Random.Shared.Next(100000, 999999) },
            }),
        };
        secondRequest.Headers.Add("Idempotency-Key", key);
        using var second = await Client.SendAsync(secondRequest);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_SameIdempotencyKeyAndPayloadFromDifferentActor_ReturnsConflict()
    {
        var key = $"customer-{Guid.NewGuid():N}";
        var payload = new
        {
            type = "Individual",
            name = "Actor bound " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Contact", phone = "0911" + Random.Shared.Next(100000, 999999) },
        };

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(payload),
        };
        firstRequest.Headers.Add("Idempotency-Key", key);
        (await Client.SendAsync(firstRequest)).EnsureSuccessStatusCode();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        using var secondRequest = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
        {
            Content = JsonContent.Create(payload),
        };
        secondRequest.Headers.Add("Idempotency-Key", key);
        using var second = await Client.SendAsync(secondRequest);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_CompanyWithoutTaxId_IsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var res = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Company",
            name = "ACME " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "CEO", phone = "0900" + Random.Shared.Next(100000, 999999) },
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateTaxId_Returns409WithoutReason()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var taxId = "TAX-" + Guid.NewGuid().ToString("N")[..6];
        var basePayload = new
        {
            type = "Company",
            name = "ACME",
            taxId,
            address = "1 Nguyễn Trãi",
            representativeName = "CEO",
            sourceCode = "marketing",
            primaryContact = new { fullName = "CEO", phone = "0911" + Random.Shared.Next(100000, 999999) },
        };

        var first = await Client.PostAsJsonAsync("/api/customers", basePayload);
        first.EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/customers", basePayload with { name = "ACME clone" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await ReadJsonAsync(second);
        body.GetProperty("field").GetString().Should().Be("TaxId");
        body.GetProperty("value").GetString().Should().Be(taxId);
    }

    [Fact]
    public async Task Create_DuplicateTaxId_AllowedWithOverrideReason()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var taxId = "TAX-" + Guid.NewGuid().ToString("N")[..6];
        var basePayload = new
        {
            type = "Company",
            name = "ACME",
            taxId,
            address = "1 Nguyễn Trãi",
            representativeName = "CEO",
            sourceCode = "marketing",
            primaryContact = new { fullName = "CEO", phone = "0911" + Random.Shared.Next(100000, 999999) },
        };

        (await Client.PostAsJsonAsync("/api/customers", basePayload)).EnsureSuccessStatusCode();

        var second = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Company",
            name = "ACME sister",
            taxId,
            address = "1 Nguyễn Trãi",
            representativeName = "CEO",
            sourceCode = "marketing",
            primaryContact = new { fullName = "CEO 2", phone = "0911" + Random.Shared.Next(100000, 999999) },
            duplicateOverrideReason = "Cùng tập đoàn, khác pháp nhân",
        });

        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task List_AsSale_HidesOtherOwnersCustomer()
    {
        // Sales Manager creates a customer, Sale role should not see it.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Manager owned " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "N", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var list = await Client.GetAsync("/api/customers?pageSize=100");
        list.EnsureSuccessStatusCode();
        var ids = (await ReadJsonAsync(list)).GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32()).ToList();
        ids.Should().NotContain(id);

        (await Client.GetAsync($"/api/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_AsSale_CannotSuspend()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Suspend me " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "N", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        var ownerId = body.GetProperty("ownerUserId").GetInt32();

        var res = await Client.PutAsJsonAsync($"/api/customers/{id}", new
        {
            type = "Individual",
            name = "Try suspend",
            sourceCode = "marketing",
            relationshipStatus = "Suspended",
            ownerUserId = ownerId,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_AsManager_CanSuspend()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));

        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Suspend me " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "N", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var res = await Client.PutAsJsonAsync($"/api/customers/{id}", new
        {
            type = "Individual",
            name = "Suspended!",
            sourceCode = "marketing",
            relationshipStatus = "Suspended",
        });

        res.EnsureSuccessStatusCode();
        (await ReadJsonAsync(res)).GetProperty("relationshipStatus").GetString().Should().Be("Suspended");
    }

    [Fact]
    public async Task Update_WithStaleRowVersion_ReturnsConflictWithoutOverwritingLatestData()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Concurrency " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Contact", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var original = await ReadJsonAsync(created);
        var id = original.GetProperty("id").GetInt32();
        var staleRowVersion = original.GetProperty("rowVersion").GetString();
        staleRowVersion.Should().NotBeNullOrWhiteSpace();
        created.Headers.ETag?.Tag.Should().Be($"\"{staleRowVersion}\"");

        var latest = await Client.PutAsJsonAsync($"/api/customers/{id}", new
        {
            rowVersion = staleRowVersion,
            type = "Individual",
            name = "Latest value",
            sourceCode = "marketing",
            relationshipStatus = "InProgress",
        });
        latest.EnsureSuccessStatusCode();

        var stale = await Client.PutAsJsonAsync($"/api/customers/{id}", new
        {
            rowVersion = staleRowVersion,
            type = "Individual",
            name = "Stale overwrite",
            sourceCode = "marketing",
            relationshipStatus = "InProgress",
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadJsonAsync(stale)).GetProperty("code").GetString()
            .Should().Be("crm_concurrency_conflict");

        var current = await Client.GetAsync($"/api/customers/{id}");
        current.EnsureSuccessStatusCode();
        (await ReadJsonAsync(current)).GetProperty("name").GetString().Should().Be("Latest value");
    }

    [Fact]
    public async Task Update_WithMalformedOrConflictingToken_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Token validation " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Contact", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        var rowVersion = body.GetProperty("rowVersion").GetString();

        var malformed = await Client.PutAsJsonAsync($"/api/customers/{id}", new
        {
            rowVersion = "not-base64",
            type = "Individual",
            name = "Malformed",
            sourceCode = "marketing",
            relationshipStatus = "Prospect",
        });
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(malformed)).GetProperty("code").GetString()
            .Should().Be("crm_concurrency_token_invalid");

        using var conflictingRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/customers/{id}")
        {
            Content = JsonContent.Create(new
            {
                rowVersion,
                type = "Individual",
                name = "Conflicting",
                sourceCode = "marketing",
                relationshipStatus = "Prospect",
            }),
        };
        conflictingRequest.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAAAA=\"");
        using var conflicting = await Client.SendAsync(conflictingRequest);
        conflicting.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Contact_UpsertAndPrimaryPromotion()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Multi contact " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Original", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var newContact = await Client.PostAsJsonAsync($"/api/customers/{id}/contacts", new
        {
            fullName = "Anh Backup",
            phone = "0900" + Random.Shared.Next(100000, 999999),
            isPrimary = true,
        });
        newContact.EnsureSuccessStatusCode();
        (await ReadJsonAsync(newContact)).GetProperty("isPrimary").GetBoolean().Should().BeTrue();

        var detail = await Client.GetAsync($"/api/customers/{id}");
        detail.EnsureSuccessStatusCode();
        var contacts = (await ReadJsonAsync(detail)).GetProperty("contacts").EnumerateArray().ToList();
        contacts.Count.Should().Be(2);
        contacts.Count(c => c.GetProperty("isPrimary").GetBoolean()).Should().Be(1);
        contacts.First(c => c.GetProperty("isPrimary").GetBoolean())
            .GetProperty("fullName").GetString().Should().Be("Anh Backup");
    }

    [Fact]
    public async Task Contact_DeletingLastOne_IsRejected()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Delete-last " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Only", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(created);
        var id = body.GetProperty("id").GetInt32();
        var contactId = body.GetProperty("contacts")[0].GetProperty("id").GetInt32();

        var res = await Client.DeleteAsync($"/api/customers/{id}/contacts/{contactId}");
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_SalesUser_CannotDeleteOtherOwnersCustomer_ReturnsNotFound()
    {
        // SECURITY regression guard — Sales must never wipe another user's
        // record just by knowing the id. Endpoint returns 404 (not 403) so
        // callers cannot even infer whether the row exists.
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Manager-owned " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "N", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Manager can still see it.
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync($"/api/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithDownstreamAggregates_RemovesCompleteCustomerTree()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Aggregate delete " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Owner", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var customerId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();
        var ids = await WithDbAsync(async db =>
        {
            var opportunity = new Opportunity { Name = "Owned opportunity", CustomerId = customerId };
            db.Opportunities.Add(opportunity);
            await db.SaveChangesAsync();
            var quote = new Quote
            {
                Code = UniqueSlug("QT-CUSTOMER-DELETE"),
                OpportunityId = opportunity.Id,
                AreaSqm = 1,
                UnitPricePerSqm = 1,
                Subtotal = 1,
                GrandTotal = 1,
            };
            var contract = new Contract
            {
                ContractNumber = UniqueSlug("HD-CUSTOMER-DELETE"),
                CustomerId = customerId,
                OpportunityId = opportunity.Id,
            };
            var tender = new Tender
            {
                Code = UniqueSlug("TD-CUSTOMER-DELETE"),
                Name = "Owned tender",
                CustomerId = customerId,
                SubmissionDeadline = DateTime.UtcNow.AddDays(1),
            };
            var project = new DesignProject
            {
                ProjectCode = UniqueSlug("DP-CUSTOMER-DELETE"),
                Name = "Owned project",
                CustomerId = customerId,
                Contract = contract,
            };
            db.AddRange(quote, tender, project);
            await db.SaveChangesAsync();
            return (
                OpportunityId: opportunity.Id,
                QuoteId: quote.Id,
                ContractId: contract.Id,
                TenderId: tender.Id,
                ProjectId: project.Id);
        });

        (await Client.DeleteAsync($"/api/customers/{customerId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await WithDbAsync(async db =>
        {
            (await db.Customers.AnyAsync(row => row.Id == customerId)).Should().BeFalse();
            (await db.Opportunities.AnyAsync(row => row.Id == ids.OpportunityId)).Should().BeFalse();
            (await db.Quotes.AnyAsync(row => row.Id == ids.QuoteId)).Should().BeFalse();
            (await db.Contracts.AnyAsync(row => row.Id == ids.ContractId)).Should().BeFalse();
            (await db.Tenders.AnyAsync(row => row.Id == ids.TenderId)).Should().BeFalse();
            (await db.DesignProjects.AnyAsync(row => row.Id == ids.ProjectId)).Should().BeFalse();
        });
    }

    [Fact]
    public async Task Activity_AsOwner_PersistsEntry()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Activity-owner " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "X", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var id = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var res = await Client.PostAsJsonAsync($"/api/customers/{id}/activities", new
        {
            type = "Meeting",
            content = "Onsite Q1 review",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var detail = await Client.GetAsync($"/api/customers/{id}");
        (await ReadJsonAsync(detail)).GetProperty("activities").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Documents_AsOwner_CanUploadListAndDelete()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));
        var customerId = await CreateIndividualCustomerAsync("Document owner");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("customer document"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "brief.pdf");
        form.Add(new StringContent("Contract brief"), "label");

        var upload = await Client.PostAsync($"/api/customers/{customerId}/documents", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploaded = await ReadJsonAsync(upload);
        var documentId = uploaded.GetProperty("id").GetInt32();
        var filePath = uploaded.GetProperty("filePath").GetString();
        filePath.Should().StartWith($"/files/customers/{customerId}/");
        (await Client.GetAsync(filePath)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await Client.GetAsync($"/api/customers/{customerId}/documents");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(list)).EnumerateArray()
            .Should().ContainSingle(document => document.GetProperty("id").GetInt32() == documentId);

        var content = await Client.GetAsync($"/api/customers/{customerId}/documents/{documentId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("customer document");

        Client.DefaultRequestHeaders.Authorization = null;
        (await Client.GetAsync($"/api/customers/{customerId}/documents/{documentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.DeleteAsync($"/api/customers/{customerId}/documents/{documentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadJsonAsync(await Client.GetAsync($"/api/customers/{customerId}/documents")))
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Documents_AsSale_CannotAccessAnotherOwnersCustomer()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateIndividualCustomerAsync("Manager document owner");
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALE"));

        (await Client.GetAsync($"/api/customers/{customerId}/documents"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> CreateIndividualCustomerAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = name + " " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new
            {
                fullName = "Owner",
                phone = "0911" + Random.Shared.Next(100000, 999999),
            },
        });
        response.EnsureSuccessStatusCode();
        return (await ReadJsonAsync(response)).GetProperty("id").GetInt32();
    }
}
