using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        (await Client.GetAsync($"/api/customers/{id}/deletion-impact"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = new string('a', 64),
                confirmation = $"CUSTOMER-{id}",
                rowVersion = Convert.ToBase64String(BitConverter.GetBytes(1L)),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithDbAsync(db => db.Customers.AnyAsync(item => item.Id == id))).Should().BeTrue();
        (await WithDbAsync(db => db.HardDeleteOperations.AnyAsync(item =>
            item.ResourceType == "Customer" && item.ResourceId == id.ToString()))).Should().BeFalse();

        // Manager can still see it.
        Client.DefaultRequestHeaders.Authorization = null;
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        (await Client.GetAsync($"/api/customers/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithoutAuthOrManagePermission_IsRejected()
    {
        var unauthenticated = await Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/customers/1")
            {
                Content = JsonContent.Create(new { }),
            });
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var forbidden = await Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "/api/customers/1")
            {
                Content = JsonContent.Create(new { }),
            });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WithoutDownstreamRoots_RemovesCustomerAggregate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var created = await Client.PostAsJsonAsync("/api/customers", new
        {
            type = "Individual",
            name = "Durable delete " + Guid.NewGuid().ToString("N")[..6],
            sourceCode = "marketing",
            primaryContact = new { fullName = "Owner", phone = "0911" + Random.Shared.Next(100000, 999999) },
        });
        created.EnsureSuccessStatusCode();
        var customer = await ReadJsonAsync(created);
        var customerId = customer.GetProperty("id").GetInt32();
        using var documentForm = new MultipartFormDataContent();
        var documentContent = new ByteArrayContent(Encoding.UTF8.GetBytes("owned customer document"));
        documentContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        documentForm.Add(documentContent, "file", "owned.pdf");
        var upload = await Client.PostAsync($"/api/customers/{customerId}/documents", documentForm);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var uploadedDocument = await ReadJsonAsync(upload);
        var documentId = uploadedDocument.GetProperty("id").GetInt32();
        var documentPath = uploadedDocument.GetProperty("filePath").GetString()!;
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var documentFullPath = Path.Combine(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            documentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(documentFullPath).Should().BeTrue();
        var linked = await WithDbAsync(async db =>
        {
            var actorUserId = await db.Users.Select(item => item.Id).FirstAsync();
            var otherCustomer = new Customer
            {
                Type = CustomerType.Company,
                Name = "Project owner " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "marketing",
                RelationshipStatus = CustomerRelationshipStatus.Prospect,
            };
            db.Customers.Add(otherCustomer);
            await db.SaveChangesAsync();
            var operationalProject = new OperationalProject
            {
                Code = UniqueSlug("OP-CUSTOMER-METADATA"),
                Name = "Metadata host project",
                CustomerId = otherCustomer.Id,
            };
            var lead = new Lead
            {
                Name = "Converted lead",
                Phone = "0911222333",
                SourceCode = "marketing",
                Status = LeadStatus.Converted,
                ConvertedAt = DateTime.UtcNow,
                ConvertedCustomerId = customerId,
            };
            db.AddRange(operationalProject, lead);
            await db.SaveChangesAsync();
            var projectDocument = new ProjectDocument
            {
                OperationalProjectId = operationalProject.Id,
                CustomerId = customerId,
                LocalPath = $"/files/projects/{Guid.NewGuid():N}.pdf",
                OriginalFileName = "metadata.pdf",
                ContentType = "application/pdf",
                Sha256 = new string('a', 64),
            };
            var activity = new CustomerActivity
            {
                CustomerId = customerId,
                Type = CustomerActivityType.Note,
                Content = "Owned activity",
                CreatedByUserId = actorUserId,
            };
            var translation = new EntityTranslation
            {
                EntityType = "Customer",
                EntityId = customerId,
                FieldName = "Name",
                LanguageCode = "en",
                Value = "Translated customer",
            };
            db.AddRange(projectDocument, activity, translation);
            await db.SaveChangesAsync();
            return (LeadId: lead.Id, ProjectDocumentId: projectDocument.Id,
                ActivityId: activity.Id, TranslationId: translation.Id);
        });
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/customers/{customerId}/deletion-impact"));

        impact.GetProperty("canDelete").GetBoolean().Should().BeTrue();
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                rowVersion = customer.GetProperty("rowVersion").GetString(),
            }),
        });

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await WithDbAsync(async db =>
        {
            (await db.Customers.AnyAsync(item => item.Id == customerId)).Should().BeFalse();
            (await db.CustomerContacts.AnyAsync(item => item.CustomerId == customerId)).Should().BeFalse();
            (await db.CustomerDocuments.AnyAsync(item => item.Id == documentId)).Should().BeFalse();
            (await db.CustomerActivities.AnyAsync(item => item.Id == linked.ActivityId)).Should().BeFalse();
            (await db.EntityTranslations.AnyAsync(item => item.Id == linked.TranslationId)).Should().BeFalse();
            (await db.Leads.SingleAsync(item => item.Id == linked.LeadId))
                .ConvertedCustomerId.Should().BeNull();
            (await db.ProjectDocuments.SingleAsync(item => item.Id == linked.ProjectDocumentId))
                .CustomerId.Should().BeNull();
            (await db.AuditLogs.AnyAsync(item => item.Action == "customer.delete" &&
                item.ResourceId == customerId.ToString() && item.Status == "success")).Should().BeTrue();
        });
            File.Exists(documentFullPath).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_MissingOrStaleRowVersion_IsRejectedWithoutCreatingOperation()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateIndividualCustomerAsync("Concurrency delete");
        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/customers/{customerId}/deletion-impact"));
        var requiredConfirmation = impact.GetProperty("requiredConfirmation").GetString();
        var planToken = impact.GetProperty("planToken").GetString();
        var currentCustomer = await ReadJsonAsync(await Client.GetAsync($"/api/customers/{customerId}"));
        var currentRowVersion = currentCustomer.GetProperty("rowVersion").GetString();

        var missing = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
        {
            Content = JsonContent.Create(new { planToken, confirmation = requiredConfirmation }),
        });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var wrongConfirmation = await Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
            {
                Content = JsonContent.Create(new
                {
                    planToken,
                    confirmation = $"{requiredConfirmation} ",
                    rowVersion = currentRowVersion,
                }),
            });
        wrongConfirmation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stale = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
        {
            Content = JsonContent.Create(new
            {
                planToken,
                confirmation = requiredConfirmation,
                rowVersion = Convert.ToBase64String(BitConverter.GetBytes(long.MaxValue)),
            }),
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Client.PostAsJsonAsync($"/api/customers/{customerId}/activities", new
        {
            type = "Note",
            content = "Changes the deletion plan",
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        var stalePlan = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
        {
            Content = JsonContent.Create(new
            {
                planToken,
                confirmation = requiredConfirmation,
                rowVersion = currentRowVersion,
            }),
        });
        stalePlan.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await WithDbAsync(db => db.Customers.AnyAsync(item => item.Id == customerId))).Should().BeTrue();
        (await WithDbAsync(db => db.HardDeleteOperations.AnyAsync(item =>
            item.ResourceType == "Customer" && item.ResourceId == customerId.ToString()))).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WhenProjectDocumentSharesManagedFile_IsBlockedAndPreservesFile()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "SALES_MANAGER"));
        var customerId = await CreateIndividualCustomerAsync("Shared file delete");
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("shared customer file"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", "shared.pdf");
        var upload = await Client.PostAsync($"/api/customers/{customerId}/documents", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var document = await ReadJsonAsync(upload);
        var documentPath = document.GetProperty("filePath").GetString()!;
        var environment = Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var fullPath = Path.Combine(
            environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
            documentPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var projectDocumentId = await WithDbAsync(async db =>
        {
            var otherCustomer = new Customer
            {
                Type = CustomerType.Company,
                Name = "Shared file project owner " + Guid.NewGuid().ToString("N")[..6],
                SourceCode = "marketing",
            };
            db.Customers.Add(otherCustomer);
            await db.SaveChangesAsync();
            var project = new OperationalProject
            {
                Code = UniqueSlug("OP-SHARED-CUSTOMER-FILE"),
                Name = "Shared file host",
                CustomerId = otherCustomer.Id,
            };
            db.OperationalProjects.Add(project);
            await db.SaveChangesAsync();
            var projectDocument = new ProjectDocument
            {
                OperationalProjectId = project.Id,
                CustomerId = customerId,
                LocalPath = $"/{documentPath}",
                OriginalFileName = "shared.pdf",
                ContentType = "application/pdf",
                Sha256 = new string('b', 64),
            };
            db.ProjectDocuments.Add(projectDocument);
            await db.SaveChangesAsync();
            return projectDocument.Id;
        });

        try
        {
            var customer = await ReadJsonAsync(await Client.GetAsync($"/api/customers/{customerId}"));
            var impact = await ReadJsonAsync(
                await Client.GetAsync($"/api/customers/{customerId}/deletion-impact"));
            impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
            impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
                item.GetProperty("key").GetString() == "customer.fileBlockers" &&
                item.GetProperty("examples").EnumerateArray().Any(example =>
                    example.GetString() == $"shared-project-document:{documentPath}"));
            var delete = await Client.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
                {
                    Content = JsonContent.Create(new
                    {
                        planToken = impact.GetProperty("planToken").GetString(),
                        confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                        rowVersion = customer.GetProperty("rowVersion").GetString(),
                    }),
                });
            delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            File.Exists(fullPath).Should().BeTrue();
            (await WithDbAsync(db => db.Customers.AnyAsync(item => item.Id == customerId))).Should().BeTrue();
            (await WithDbAsync(db => db.ProjectDocuments.AnyAsync(item => item.Id == projectDocumentId)))
                .Should().BeTrue();
        }
        finally
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }

    [Fact]
    public async Task Delete_WithDownstreamAggregates_IsBlockedAndPreservesCompleteCustomerGraph()
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
            var operationalProject = new OperationalProject
            {
                Code = UniqueSlug("OP-CUSTOMER-DELETE"),
                Name = "Owned operational project",
                CustomerId = customerId,
            };
            db.AddRange(quote, tender, project, operationalProject);
            await db.SaveChangesAsync();
            return (
                OpportunityId: opportunity.Id,
                QuoteId: quote.Id,
                ContractId: contract.Id,
                TenderId: tender.Id,
                ProjectId: project.Id,
                OperationalProjectId: operationalProject.Id);
        });

        var impact = await ReadJsonAsync(
            await Client.GetAsync($"/api/customers/{customerId}/deletion-impact"));
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "customer.opportunities" &&
            item.GetProperty("action").GetString() == "Block");
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "customer.contracts" &&
            item.GetProperty("action").GetString() == "Block");
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "customer.tenders" &&
            item.GetProperty("action").GetString() == "Block");
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "customer.designProjects" &&
            item.GetProperty("action").GetString() == "Block");
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "customer.operationalProjects" &&
            item.GetProperty("action").GetString() == "Block");
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/customers/{customerId}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                rowVersion = (await ReadJsonAsync(await Client.GetAsync($"/api/customers/{customerId}")))
                    .GetProperty("rowVersion").GetString(),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await WithDbAsync(async db =>
        {
            (await db.Customers.AnyAsync(row => row.Id == customerId)).Should().BeTrue();
            (await db.Opportunities.AnyAsync(row => row.Id == ids.OpportunityId)).Should().BeTrue();
            (await db.Quotes.AnyAsync(row => row.Id == ids.QuoteId)).Should().BeTrue();
            (await db.Contracts.AnyAsync(row => row.Id == ids.ContractId)).Should().BeTrue();
            (await db.Tenders.AnyAsync(row => row.Id == ids.TenderId)).Should().BeTrue();
            (await db.DesignProjects.AnyAsync(row => row.Id == ids.ProjectId)).Should().BeTrue();
            (await db.OperationalProjects.AnyAsync(row => row.Id == ids.OperationalProjectId)).Should().BeTrue();
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
