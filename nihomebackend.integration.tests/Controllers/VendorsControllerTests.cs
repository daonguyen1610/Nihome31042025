using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NihomeBackend.Models;
using NihomeBackend.Services;

namespace NihomeBackend.IntegrationTests.Controllers;

public class VendorsControllerTests : IntegrationTestBase
{
    public VendorsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_WithoutAuth_ReturnsUnauthorized()
    {
        (await Client.GetAsync("/api/vendors")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Warehouse_CanViewButCannotCreate()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await Client.GetAsync("/api/vendors")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = "DENIED",
            companyName = "Denied Vendor",
            vendorType = "Supplier",
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync("/api/vendors/2147483647")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_InvalidPayload_ReturnsBadRequest()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));

        var response = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = " ",
            companyName = " ",
            email = "not-an-email",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_MissingVendor_ReturnsNotFound()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));

        (await Client.GetAsync("/api/vendors/2147483647")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CrudRoundTrip_FiltersAndRejectsDuplicateCode()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var code = $"VEN-{suffix}";

        var created = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = code,
            companyName = $"Vendor {suffix}",
            vendorType = "Supplier",
            taxCode = $"TAX-{suffix}",
            phone = "0901234567",
            email = $"vendor-{suffix}@example.com",
            contactPerson = "Nguyen Van A",
            tradeCategory = "Electrical",
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdBody = await ReadJsonAsync(created);
        var id = createdBody.GetProperty("id").GetInt32();
        createdBody.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var createdByName = createdBody.GetProperty("createdByName").GetString();
        createdByName.Should().NotBeNullOrWhiteSpace();

        var detailBeforeUpdate = await Client.GetAsync($"/api/vendors/{id}");
        detailBeforeUpdate.EnsureSuccessStatusCode();
        (await ReadJsonAsync(detailBeforeUpdate)).GetProperty("createdByName").GetString().Should().Be(createdByName);

        var filtered = await Client.GetAsync($"/api/vendors?search={code}&vendorType=Supplier&isActive=true");
        filtered.EnsureSuccessStatusCode();
        var items = (await ReadJsonAsync(filtered)).GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("id").GetInt32().Should().Be(id);

        var updated = await Client.PutAsJsonAsync($"/api/vendors/{id}", new
        {
            vendorCode = code,
            companyName = $"Vendor Updated {suffix}",
            vendorType = "Both",
            email = $"vendor-{suffix}@example.com",
            isActive = false,
            rowVersion = createdBody.GetProperty("rowVersion").GetString(),
        });
        updated.EnsureSuccessStatusCode();
        var updatedBody = await ReadJsonAsync(updated);
        updatedBody.GetProperty("isActive").GetBoolean().Should().BeFalse();
        updatedBody.GetProperty("createdByName").GetString().Should().Be(createdByName);

        var duplicate = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = code.ToLowerInvariant(),
            companyName = "Duplicate Vendor",
            vendorType = "SubContractor",
            phone = "0901234567",
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var impact = await Client.GetAsync($"/api/vendors/{id}/deletion-impact");
        impact.EnsureSuccessStatusCode();
        var impactBody = await ReadJsonAsync(impact);
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/vendors/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impactBody.GetProperty("planToken").GetString(),
                confirmation = impactBody.GetProperty("requiredConfirmation").GetString(),
                rowVersion = updatedBody.GetProperty("rowVersion").GetString(),
            }),
        });
        delete.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Accepted);
        (await Client.GetAsync($"/api/vendors/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CapabilityFile_IsReadableOnlyAfterVendorReferencesUpload()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        using var uploadForm = FileForm("vendor capability", "vendor.pdf");
        var upload = await Client.PostAsync("/api/business-documents/vendors", uploadForm);
        upload.EnsureSuccessStatusCode();
        var uploadBody = await ReadJsonAsync(upload);
        var path = uploadBody.GetProperty("path").GetString()!;
        var claimToken = uploadBody.GetProperty("claimToken").GetGuid();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"DOC-{suffix}",
            companyName = $"Document vendor {suffix}",
            vendorType = "Supplier",
            phone = "0901234567",
            capabilityFileUrl = path,
            capabilityUploadToken = claimToken,
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(create);
        var id = created.GetProperty("id").GetInt32();

        var content = await Client.GetAsync($"/api/vendors/{id}/capability-file/content");
        content.StatusCode.Should().Be(HttpStatusCode.OK);
        (await content.Content.ReadAsStringAsync()).Should().Be("vendor capability");
        (await Client.GetAsync("/api/vendors/2147483647/capability-file/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/vendors/{id}/deletion-impact"));
        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/vendors/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                rowVersion = created.GetProperty("rowVersion").GetString(),
            }),
        });
        delete.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Accepted);
        (await Client.GetAsync($"/api/vendors/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.GetAsync($"/api/vendors/{id}/capability-file/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_VendorReferencedByDownstreamContract_IsRejectedWithoutMutation()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var vendorId = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            var customer = new Customer
            {
                Name = "Contract customer",
                Type = CustomerType.Individual,
                SourceCode = "marketing",
            };
            var vendor = new Vendor
            {
                VendorCode = $"LINK-{Guid.NewGuid():N}"[..20],
                CompanyName = "Linked vendor",
                VendorType = VendorType.SubContractor,
                CreatedByUserId = userId,
            };
            db.AddRange(customer, vendor);
            await db.SaveChangesAsync();
            db.Contracts.Add(new Contract
            {
                ContractNumber = $"HD-LINK-{Guid.NewGuid():N}"[..30],
                CustomerId = customer.Id,
                Direction = ContractDirection.Downstream,
                Type = ContractType.Subcontract,
                VendorId = vendor.Id,
            });
            await db.SaveChangesAsync();
            return vendor.Id;
        });

        var response = await Client.GetAsync($"/api/vendors/{vendorId}/deletion-impact");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var impact = await ReadJsonAsync(response);
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        impact.GetProperty("items").EnumerateArray().Should().Contain(item =>
            item.GetProperty("key").GetString() == "vendor.contracts" &&
            item.GetProperty("action").GetString() == "Block");
        (await WithDbAsync(db => db.Vendors.AnyAsync(vendor => vendor.Id == vendorId)))
            .Should().BeTrue();
        (await WithDbAsync(db => db.Contracts.AnyAsync(contract => contract.VendorId == vendorId)))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("abcd", "vendor@example.com")]
    [InlineData("0901234567", "invalid@email")]
    [InlineData(null, null)]
    public async Task Create_InvalidContact_IsRejected(string? phone, string? email)
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var response = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"CONTACT-{Guid.NewGuid():N}"[..20],
            companyName = $"Contact vendor {Guid.NewGuid():N}",
            vendorType = "Supplier",
            phone,
            email,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_DuplicateCompanyName_IsRejectedCaseInsensitively()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var company = $"Unique Company {Guid.NewGuid():N}";
        var first = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"COMP-A-{Guid.NewGuid():N}"[..20],
            companyName = company,
            vendorType = "Supplier",
            phone = "0901234567",
        });
        first.EnsureSuccessStatusCode();
        var second = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"COMP-B-{Guid.NewGuid():N}"[..20],
            companyName = company.ToUpperInvariant(),
            vendorType = "Supplier",
            phone = "0901234567",
        });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Detail_IncludesContractsRatingsAndHistory()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var vendorId = await WithDbAsync(async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            var customer = new Customer { Name = "Vendor detail customer", Type = CustomerType.Individual, SourceCode = "marketing" };
            var vendor = new Vendor { VendorCode = $"DETAIL-{Guid.NewGuid():N}"[..20], CompanyName = $"Detail vendor {Guid.NewGuid():N}", VendorType = VendorType.Supplier, Phone = "0901234567", CreatedByUserId = userId, UpdatedByUserId = userId };
            db.AddRange(customer, vendor);
            await db.SaveChangesAsync();
            var project = new OperationalProject { Code = $"PJ-{Guid.NewGuid():N}"[..20], Name = "Vendor project", CustomerId = customer.Id };
            db.Add(project);
            await db.SaveChangesAsync();
            var contract = new Contract { ContractNumber = $"HD-{Guid.NewGuid():N}"[..20], CustomerId = customer.Id, OperationalProjectId = project.Id, Direction = ContractDirection.Downstream, Type = ContractType.Supply, VendorId = vendor.Id };
            db.Add(contract);
            await db.SaveChangesAsync();
            db.VendorRatings.Add(new VendorRating { OperationalProjectId = project.Id, ContractId = contract.Id, VendorId = vendor.Id, VersionNumber = 1, ProcurementOwnerUserId = userId, PreparedByUserId = userId, OverallScore = 8 });
            db.PaymentRequests.Add(new PaymentRequest { Code = $"PAY-{Guid.NewGuid():N}"[..20], ContractId = contract.Id, VendorId = vendor.Id, SupplierInvoiceNumber = $"INV-{Guid.NewGuid():N}"[..20], InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow), InvoiceAmount = 100, ReceivedAt = DateTime.UtcNow, AssignedAccountantUserId = userId, CreatedByUserId = userId });
            db.AuditLogs.Add(new AuditLog { AuditId = Guid.NewGuid().ToString(), CreatedAt = DateTime.UtcNow, Action = "vendor.update", ResourceType = "Vendor", ResourceId = vendor.Id.ToString(), Message = "Vendor updated.", ActorUserId = userId });
            await db.SaveChangesAsync();
            return vendor.Id;
        });

        var response = await Client.GetAsync($"/api/vendors/{vendorId}");
        response.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(response);
        body.GetProperty("contracts").GetArrayLength().Should().Be(1);
        body.GetProperty("ratings").GetArrayLength().Should().Be(1);
        body.GetProperty("history").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("updatedByName").GetString().Should().NotBeNullOrWhiteSpace();

        var impactResponse = await Client.GetAsync($"/api/vendors/{vendorId}/deletion-impact");
        impactResponse.EnsureSuccessStatusCode();
        var impact = await ReadJsonAsync(impactResponse);
        impact.GetProperty("canDelete").GetBoolean().Should().BeFalse();
        var keys = impact.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("key").GetString()).ToList();
        keys.Should().Contain(["vendor.contracts", "vendor.ratings", "vendor.paymentRequests"]);

        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "WAREHOUSE"));
        var scopedBody = await ReadJsonAsync(await Client.GetAsync($"/api/vendors/{vendorId}"));
        scopedBody.GetProperty("contracts").GetArrayLength().Should().Be(0);
        scopedBody.GetProperty("ratings").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Delete_WhenVendorChangesAfterPreview_ReturnsConflictWithoutMutation()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"STALE-{suffix}",
            companyName = $"Stale vendor {suffix}",
            vendorType = "Supplier",
            phone = "0901234567",
        });
        create.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync(create);
        var id = created.GetProperty("id").GetInt32();
        var impact = await ReadJsonAsync(await Client.GetAsync($"/api/vendors/{id}/deletion-impact"));

        await WithDbAsync(async db =>
        {
            var vendor = await db.Vendors.SingleAsync(item => item.Id == id);
            vendor.CompanyName += " changed";
            await db.SaveChangesAsync();
        });

        var delete = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/vendors/{id}")
        {
            Content = JsonContent.Create(new
            {
                planToken = impact.GetProperty("planToken").GetString(),
                confirmation = impact.GetProperty("requiredConfirmation").GetString(),
                rowVersion = created.GetProperty("rowVersion").GetString(),
            }),
        });
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await WithDbAsync(db => db.Vendors.AnyAsync(item => item.Id == id))).Should().BeTrue();
    }

    [Fact]
    public async Task DiscardVendorDocument_RejectsReferencedFile()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        using var uploadForm = FileForm("vendor capability", "owned.pdf");
        var upload = await Client.PostAsync("/api/business-documents/vendors", uploadForm);
        upload.EnsureSuccessStatusCode();
        var uploadBody = await ReadJsonAsync(upload);
        var path = uploadBody.GetProperty("path").GetString()!;
        var claimToken = uploadBody.GetProperty("claimToken").GetGuid();
        var create = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"OWNED-{Guid.NewGuid():N}"[..20],
            companyName = $"Owned vendor {Guid.NewGuid():N}",
            vendorType = "Supplier",
            phone = "0901234567",
            capabilityFileUrl = path,
            capabilityUploadToken = claimToken,
        });
        create.EnsureSuccessStatusCode();

        var discard = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/business-documents/vendors")
        {
            Content = JsonContent.Create(new { claimToken }),
        });
        discard.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Client.GetAsync($"/api/vendors/{(await ReadJsonAsync(create)).GetProperty("id").GetInt32()}/capability-file/content"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DiscardVendorDocument_UnreferencedFile_IsIdempotent()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        using var uploadForm = FileForm("unassigned capability", "unassigned.pdf");
        var upload = await Client.PostAsync("/api/business-documents/vendors", uploadForm);
        upload.EnsureSuccessStatusCode();
        var uploadBody = await ReadJsonAsync(upload);
        var claimToken = uploadBody.GetProperty("claimToken").GetGuid();

        async Task<HttpResponseMessage> Discard() => await Client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete,
            "/api/business-documents/vendors")
        {
            Content = JsonContent.Create(new { claimToken }),
        });

        (await Discard()).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Discard()).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Create_ReusedCapabilityUploadClaim_IsRejectedWithoutSecondVendor()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        using var uploadForm = FileForm("single owner", "single-owner.pdf");
        var uploadBody = await ReadJsonAsync(await Client.PostAsync("/api/business-documents/vendors", uploadForm));
        var path = uploadBody.GetProperty("path").GetString()!;
        var claimToken = uploadBody.GetProperty("claimToken").GetGuid();
        var firstCompany = $"Claim owner {Guid.NewGuid():N}";
        var first = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"CLAIM-A-{Guid.NewGuid():N}"[..20],
            companyName = firstCompany,
            vendorType = "Supplier",
            phone = "0901234567",
            capabilityFileUrl = path,
            capabilityUploadToken = claimToken,
        });
        first.EnsureSuccessStatusCode();

        var secondCompany = $"Claim reuse {Guid.NewGuid():N}";
        var second = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"CLAIM-B-{Guid.NewGuid():N}"[..20],
            companyName = secondCompany,
            vendorType = "Supplier",
            phone = "0901234567",
            capabilityFileUrl = path,
            capabilityUploadToken = claimToken,
        });
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await WithDbAsync(db => db.Vendors.AnyAsync(item => item.CompanyName == secondCompany))).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupExpiredUploads_RemovesOnlyUnclaimedDocuments()
    {
        await AuthTestHelper.AuthenticateAsync(Client, c => AuthTestHelper.LoginAsRoleAsync(c, "ADMIN"));
        using var pendingForm = FileForm("pending", "pending.pdf");
        var pending = await ReadJsonAsync(await Client.PostAsync("/api/business-documents/vendors", pendingForm));
        var pendingToken = pending.GetProperty("claimToken").GetGuid();
        var pendingPath = pending.GetProperty("path").GetString()!;

        using var claimedForm = FileForm("claimed", "claimed.pdf");
        var claimed = await ReadJsonAsync(await Client.PostAsync("/api/business-documents/vendors", claimedForm));
        var claimedToken = claimed.GetProperty("claimToken").GetGuid();
        var claimedPath = claimed.GetProperty("path").GetString()!;
        var create = await Client.PostAsJsonAsync("/api/vendors", new
        {
            vendorCode = $"CLEAN-{Guid.NewGuid():N}"[..20],
            companyName = $"Cleanup vendor {Guid.NewGuid():N}",
            vendorType = "Supplier",
            phone = "0901234567",
            capabilityFileUrl = claimedPath,
            capabilityUploadToken = claimedToken,
        });
        create.EnsureSuccessStatusCode();
        await WithDbAsync(async db =>
        {
            (await db.VendorDocumentUploads.SingleAsync(item => item.Token == pendingToken)).CreatedAt = DateTime.UtcNow.AddDays(-2);
            (await db.VendorDocumentUploads.SingleAsync(item => item.Token == claimedToken)).CreatedAt = DateTime.UtcNow.AddDays(-2);
            await db.SaveChangesAsync();
        });

        using var scope = Factory.Services.CreateScope();
        var removed = await scope.ServiceProvider.GetRequiredService<IVendorDocumentUploadCleanupService>()
            .CleanupAsync(DateTime.UtcNow.AddDays(-1));

        removed.Should().Be(1);
        (await WithDbAsync(db => db.VendorDocumentUploads.AnyAsync(item => item.Token == pendingToken))).Should().BeFalse();
        (await WithDbAsync(db => db.VendorDocumentUploads.AnyAsync(item => item.Token == claimedToken))).Should().BeTrue();
        scope.ServiceProvider.GetRequiredService<IBusinessDocumentStorageService>()
            .GetContent(BusinessDocumentArea.Vendors, Path.GetFileName(pendingPath)).Should().BeNull();
        scope.ServiceProvider.GetRequiredService<IBusinessDocumentStorageService>()
            .GetContent(BusinessDocumentArea.Vendors, Path.GetFileName(claimedPath)).Should().NotBeNull();
    }

    private static MultipartFormDataContent FileForm(string content, string fileName)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }
}