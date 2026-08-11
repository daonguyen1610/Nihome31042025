using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NihomeBackend.Constants;
using NihomeBackend.Models;
using NihomeBackend.Models.DTOs.Requests;

namespace NihomeBackend.IntegrationTests.Controllers;

public class VendorsControllerTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/procurement/vendors";

    public VendorsControllerTests(NihomeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task List_without_authentication_returns_unauthorized()
    {
        (await Client.GetAsync(BaseUrl)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_as_warehouse_returns_forbidden()
    {
        await AuthenticateAsAsync("WAREHOUSE");

        (await Client.GetAsync(BaseUrl)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pm_can_list_create_update_and_use_all_owner_scope()
    {
        await AuthenticateAsAsync("PM");
        var pmId = await UserIdForRoleAsync("PM");
        var qsId = await UserIdForRoleAsync("QS");
        var created = await CreateVendorAsync(pmId, "pm");

        var reassignedRequest = ValidVendorRequest(qsId, "pm-updated");
        reassignedRequest.VendorCode = created.code;
        var update = await Client.PutAsJsonAsync($"{BaseUrl}/{created.id}", reassignedRequest);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("ownerUserId").GetInt32().Should().Be(qsId);

        var list = await Client.GetAsync($"{BaseUrl}?search={created.code}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(list);
        body.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == created.id);

        var owners = await Client.GetAsync($"{BaseUrl}/owner-options");
        owners.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(owners)).EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == qsId);
    }

    [Fact]
    public async Task Qs_can_list_evaluate_and_export_but_cannot_create()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var qsId = await UserIdForRoleAsync("QS");
        var vendor = await CreateVendorAsync(qsId, "qs-access");
        var projectId = await CreateProjectAsync("qs-access");

        await AuthenticateAsAsync("QS");
        (await Client.GetAsync(BaseUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync($"{BaseUrl}/export?search={vendor.code}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var projects = await Client.GetAsync($"{BaseUrl}/project-options");
        projects.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(projects)).EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt32() == projectId);

        var evaluation = await Client.PostAsJsonAsync($"{BaseUrl}/{vendor.id}/evaluations", Evaluation(projectId));
        evaluation.StatusCode.Should().Be(HttpStatusCode.Created);

        var forbiddenCreate = await Client.PostAsJsonAsync(BaseUrl, ValidVendorRequest(qsId, "qs-forbidden"));
        forbiddenCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Super_admin_create_get_update_round_trip()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var created = await CreateVendorAsync(ownerId, "roundtrip");

        var get = await Client.GetAsync($"{BaseUrl}/{created.id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(get)).GetProperty("vendorCode").GetString().Should().Be(created.code);

        var request = ValidVendorRequest(ownerId, "roundtrip-updated");
        request.VendorCode = created.code;
        request.CompanyName = "Updated " + request.CompanyName;
        request.IsActive = false;
        var update = await Client.PutAsJsonAsync($"{BaseUrl}/{created.id}", request);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(update);
        body.GetProperty("companyName").GetString().Should().Be(request.CompanyName);
        body.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Create_duplicate_and_invalid_payload_return_bad_request()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var created = await CreateVendorAsync(ownerId, "duplicate");
        var duplicate = ValidVendorRequest(ownerId, "duplicate-other");
        duplicate.VendorCode = created.code.ToLowerInvariant();

        (await Client.PostAsJsonAsync(BaseUrl, duplicate)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var invalid = ValidVendorRequest(ownerId, "invalid");
        invalid.Email = "not-an-email";
        (await Client.PostAsJsonAsync(BaseUrl, invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_unknown_vendor_returns_not_found()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");

        (await Client.GetAsync($"{BaseUrl}/999999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Evaluation_create_update_delete_and_duplicate_contract()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var vendor = await CreateVendorAsync(ownerId, "evaluation");
        var projectId = await CreateProjectAsync("evaluation");

        var create = await Client.PostAsJsonAsync($"{BaseUrl}/{vendor.id}/evaluations", Evaluation(projectId));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var evaluationId = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        (await Client.PostAsJsonAsync($"{BaseUrl}/{vendor.id}/evaluations", Evaluation(projectId)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var update = await Client.PutAsJsonAsync(
            $"{BaseUrl}/{vendor.id}/evaluations/{evaluationId}", Evaluation(projectId, 10));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(update)).GetProperty("averageScore").GetDecimal().Should().Be(10m);

        (await Client.DeleteAsync($"{BaseUrl}/{vendor.id}/evaluations/{evaluationId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Document_upload_download_delete_and_invalid_extension_contract()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var vendor = await CreateVendorAsync(ownerId, "document");
        var payload = Encoding.UTF8.GetBytes("vendor integration document");

        using var uploadContent = DocumentForm(payload, "capability.pdf");
        var upload = await Client.PostAsync($"{BaseUrl}/{vendor.id}/documents", uploadContent);
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = (await ReadJsonAsync(upload)).GetProperty("id").GetInt32();

        var download = await Client.GetAsync($"{BaseUrl}/{vendor.id}/documents/{documentId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(payload);

        using var invalidContent = DocumentForm(payload, "malware.exe");
        (await Client.PostAsync($"{BaseUrl}/{vendor.id}/documents", invalidContent))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Client.DeleteAsync($"{BaseUrl}/{vendor.id}/documents/{documentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Export_respects_search_filter()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var target = await CreateVendorAsync(ownerId, "export-target");
        await CreateVendorAsync(ownerId, "export-noise");

        var export = await Client.GetAsync($"{BaseUrl}/export?search={target.code}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = (await ReadJsonAsync(export)).EnumerateArray().ToList();

        rows.Should().ContainSingle();
        rows[0].GetProperty("vendorCode").GetString().Should().Be(target.code);
    }

    [Fact]
    public async Task History_returns_deterministically_seeded_shape()
    {
        await AuthenticateAsAsync("SUPER_ADMIN");
        var ownerId = await UserIdForRoleAsync("PM");
        var vendor = await CreateVendorAsync(ownerId, "history");
        var action = $"vendor.test.{Guid.NewGuid():N}";
        await WithDbAsync(async db =>
        {
            db.AuditLogs.Add(new AuditLog
            {
                AuditId = Guid.NewGuid().ToString("N"),
                ResourceType = EntityTypes.Vendor,
                ResourceId = vendor.id.ToString(),
                Action = action,
                Message = "Deterministic vendor history",
                ActorUserId = ownerId,
                OldValueJson = "{\"companyName\":\"Old\"}",
                NewValueJson = "{\"companyName\":\"New\"}",
                CreatedAt = DateTime.UtcNow.AddMinutes(1),
            });
            await db.SaveChangesAsync();
        });

        var response = await Client.GetAsync($"{BaseUrl}/{vendor.id}/history");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var row = (await ReadJsonAsync(response)).EnumerateArray()
            .Single(item => item.GetProperty("action").GetString() == action);

        row.GetProperty("oldValueJson").GetString().Should().Contain("Old");
        row.GetProperty("newValueJson").GetString().Should().Contain("New");
        row.GetProperty("actorUserId").GetInt32().Should().Be(ownerId);
    }

    private Task AuthenticateAsAsync(string role) =>
        AuthTestHelper.AuthenticateAsync(Client, client => AuthTestHelper.LoginAsRoleAsync(client, role));

    private async Task<(int id, string code)> CreateVendorAsync(int ownerUserId, string marker)
    {
        var request = ValidVendorRequest(ownerUserId, marker);
        var response = await Client.PostAsJsonAsync(BaseUrl, request);
        response.EnsureSuccessStatusCode();
        var body = await ReadJsonAsync(response);
        return (body.GetProperty("id").GetInt32(), body.GetProperty("vendorCode").GetString()!);
    }

    private static CreateVendorRequest ValidVendorRequest(int ownerUserId, string marker)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        return new CreateVendorRequest
        {
            VendorCode = $"V-{marker}-{unique}"[..Math.Min(50, marker.Length + 13)],
            CompanyName = $"Vendor {marker} {unique}",
            VendorType = VendorType.Supplier,
            TaxCode = $"T-{unique}",
            Phone = "0901234567",
            Email = $"{unique}@vendor.test",
            ContactPerson = "Vendor Contact",
            ServiceGroupCode = "mep",
            OwnerUserId = ownerUserId,
            IsActive = true,
        };
    }

    private async Task<int> UserIdForRoleAsync(string role)
    {
        var phone = role switch
        {
            "PM" => TestDataSeeder.BusinessRolePhonesByCode["PM"],
            "QS" => TestDataSeeder.BusinessRolePhonesByCode["QS"],
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };
        return await WithDbAsync(db => db.Users
            .Where(user => user.PhoneNumber == phone)
            .Select(user => user.Id)
            .SingleAsync());
    }

    private async Task<int> CreateProjectAsync(string marker)
    {
        return await WithDbAsync(async db =>
        {
            var customer = new Customer
            {
                Name = $"Vendor Project Customer {Guid.NewGuid():N}",
                Type = CustomerType.Company,
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var project = new DesignProject
            {
                ProjectCode = $"DP-{Guid.NewGuid():N}"[..20],
                Name = $"Vendor Project {marker} {Guid.NewGuid():N}",
                CustomerId = customer.Id,
            };
            db.DesignProjects.Add(project);
            await db.SaveChangesAsync();
            return project.Id;
        });
    }

    private static UpsertVendorEvaluationRequest Evaluation(int projectId, byte score = 8) => new()
    {
        ProjectId = projectId,
        ScoreQuality = score,
        ScoreSchedule = score,
        ScoreCost = score,
        ScoreSafety = score,
        Comment = "Integration evaluation",
    };

    private static MultipartFormDataContent DocumentForm(byte[] payload, string fileName)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(((int)VendorDocumentType.Capability).ToString()), "documentType");
        var file = new ByteArrayContent(payload);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        return content;
    }
}
